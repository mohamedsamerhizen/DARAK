using System.Text.Json;
using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Approvals;
using DARAK.Api.DTOs.Common;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ApprovalService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    UserManager<ApplicationUser> userManager,
    IAuditLogService auditLogService)
    : IApprovalService
{
    private static readonly UserRole[] RequesterRoles =
    [
        UserRole.SuperAdmin,
        UserRole.CompoundAdmin,
        UserRole.Accountant
    ];

    private static readonly UserRole[] ApproverRoles =
    [
        UserRole.SuperAdmin,
        UserRole.CompoundAdmin
    ];

    private static readonly UserRole[] ExecutorRoles =
    [
        UserRole.SuperAdmin,
        UserRole.CompoundAdmin,
        UserRole.Accountant
    ];

    public async Task<ServiceResult<ApprovalRequestResponse>> CreateRequestAsync(
        Guid? currentUserId,
        CreateApprovalRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ApprovalRequestResponse>(currentUserId, RequesterRoles);
        if (auth is not null)
        {
            return auth;
        }

        var validation = await ValidateCreateRequestAsync(request, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.CanAccess(request.CompoundId))
        {
            return ServiceResult<ApprovalRequestResponse>.Forbidden("You do not have access to this compound.");
        }

        var policy = await GetPolicyAsync(request.CompoundId, request.ActionType, cancellationToken);
        if (!policy.IsEnabled)
        {
            return ServiceResult<ApprovalRequestResponse>.BadRequest("Approval policy is disabled for this action.");
        }

        var now = DateTime.UtcNow;
        var dueAtUtc = request.DueAtUtc ?? now.AddHours(policy.ExpireAfterHours);
        if (dueAtUtc <= now)
        {
            return ServiceResult<ApprovalRequestResponse>.BadRequest("Approval deadline must be in the future.");
        }

        var approvalRequest = new ApprovalRequest
        {
            CompoundId = request.CompoundId,
            RequestedByUserId = currentUserId!.Value,
            ActionType = request.ActionType,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Status = ApprovalStatus.Pending,
            Priority = request.Priority ?? policy.DefaultPriority,
            ExecutionStatus = ApprovalExecutionStatus.NotReady,
            Reason = request.Reason.Trim(),
            RequestPayloadJson = NormalizeOptional(request.RequestPayloadJson),
            CreatedAtUtc = now,
            DueAtUtc = dueAtUtc
        };

        dbContext.ApprovalRequests.Add(approvalRequest);
        AddActivityEvent(
            approvalRequest,
            currentUserId.Value,
            ActivityEventType.ApprovalRequested,
            "Approval requested",
            $"Approval requested for {approvalRequest.ActionType}.");
        AddNotification(
            approvalRequest,
            currentUserId.Value,
            null,
            "Approval Team",
            NotificationEventType.ApprovalRequested,
            "Approval request created",
            $"A {approvalRequest.Priority} approval request was created for {approvalRequest.ActionType}.");
        await AddApprovalAuditAsync(
            approvalRequest,
            currentUserId.Value,
            AuditActionType.ApprovalRequested,
            AuditSeverity.High,
            "Approval request created.",
            approvalRequest.Reason,
            [
                new AuditLogChangeRecord(nameof(ApprovalRequest.Status), null, ApprovalStatus.Pending.ToString()),
                new AuditLogChangeRecord(nameof(ApprovalRequest.Priority), null, approvalRequest.Priority.ToString()),
                new AuditLogChangeRecord(nameof(ApprovalRequest.ActionType), null, approvalRequest.ActionType.ToString())
            ],
            cancellationToken);

        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<ApprovalRequestResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }

        return ServiceResult<ApprovalRequestResponse>.Success(ToResponse(approvalRequest));
    }

    public async Task<ServiceResult<PagedResult<ApprovalRequestResponse>>> SearchRequestsAsync(
        Guid? currentUserId,
        ApprovalSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<PagedResult<ApprovalRequestResponse>>(currentUserId, RequesterRoles);
        if (auth is not null)
        {
            return auth;
        }

        var queryValidation = ValidateSearchQuery(query);
        if (queryValidation is not null)
        {
            return ServiceResult<PagedResult<ApprovalRequestResponse>>.BadRequest(queryValidation);
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        var approvals = ApplySearchFilters(
            dbContext.ApprovalRequests
                .AsNoTracking()
                .ApplyCompoundAccess(scope, approval => approval.CompoundId),
            query);

        var totalCount = await approvals.CountAsync(cancellationToken);
        var items = await approvals
            .OrderByDescending(approval => approval.Status == ApprovalStatus.Pending)
            .ThenByDescending(approval => approval.Priority)
            .ThenByDescending(approval => approval.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(approval => ToResponse(approval))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<ApprovalRequestResponse>>.Success(
            new PagedResult<ApprovalRequestResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<ApprovalRequestDetailsResponse>> GetDetailsAsync(
        Guid? currentUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ApprovalRequestDetailsResponse>(currentUserId, RequesterRoles);
        if (auth is not null)
        {
            return auth;
        }

        if (id == Guid.Empty)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.BadRequest("Approval request id is invalid.");
        }

        var approval = await GetScopedApprovalWithDecisionsAsync(id, cancellationToken);
        if (approval is null)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.NotFound("Approval request was not found.");
        }

        await ExpireIfNeededAsync(approval, cancellationToken);

        return ServiceResult<ApprovalRequestDetailsResponse>.Success(ToDetailsResponse(approval));
    }

    public async Task<ServiceResult<ApprovalRequestDetailsResponse>> ApproveAsync(
        Guid? currentUserId,
        Guid id,
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ApprovalRequestDetailsResponse>(currentUserId, ApproverRoles);
        if (auth is not null)
        {
            return auth;
        }

        var approval = await GetScopedApprovalWithDecisionsAsync(id, cancellationToken);
        if (approval is null)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.NotFound("Approval request was not found.");
        }

        await ExpireIfNeededAsync(approval, cancellationToken);

        if (approval.Status != ApprovalStatus.Pending)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.Conflict("Only pending approval requests can be approved.");
        }

        var policy = await GetPolicyAsync(approval.CompoundId, approval.ActionType, cancellationToken);
        var policyAccess = await ValidateApprovalPolicyDecisionAccessAsync(currentUserId!.Value, policy);
        if (policyAccess is not null)
        {
            return policyAccess;
        }

        if (!policy.AllowSelfApproval && approval.RequestedByUserId == currentUserId.Value)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.Forbidden("Self-approval is not allowed for this action.");
        }

        var now = DateTime.UtcNow;
        approval.Status = ApprovalStatus.Approved;
        approval.ExecutionStatus = ApprovalExecutionStatus.ReadyForExecution;
        approval.LastDecisionByUserId = currentUserId!.Value;
        approval.DecisionReason = NormalizeOptional(request.Reason);
        approval.DecidedAtUtc = now;
        approval.UpdatedAtUtc = now;

        AddDecision(approval, currentUserId.Value, ApprovalDecisionType.Approved, request.Reason);
        AddActivityEvent(
            approval,
            currentUserId.Value,
            ActivityEventType.ApprovalApproved,
            "Approval approved",
            $"Approval request for {approval.ActionType} was approved.");
        await AddNotificationForRequesterAsync(
            approval,
            currentUserId.Value,
            NotificationEventType.ApprovalApproved,
            "Approval request approved",
            $"Your approval request for {approval.ActionType} was approved.",
            cancellationToken);
        await AddApprovalAuditAsync(
            approval,
            currentUserId.Value,
            AuditActionType.ApprovalApproved,
            AuditSeverity.Critical,
            "Approval request approved.",
            approval.DecisionReason,
            [
                new AuditLogChangeRecord(nameof(ApprovalRequest.Status), ApprovalStatus.Pending.ToString(), ApprovalStatus.Approved.ToString()),
                new AuditLogChangeRecord(nameof(ApprovalRequest.ExecutionStatus), ApprovalExecutionStatus.NotReady.ToString(), ApprovalExecutionStatus.ReadyForExecution.ToString())
            ],
            cancellationToken);

        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<ApprovalRequestDetailsResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }
        return ServiceResult<ApprovalRequestDetailsResponse>.Success(ToDetailsResponse(approval));
    }

    public async Task<ServiceResult<ApprovalRequestDetailsResponse>> RejectAsync(
        Guid? currentUserId,
        Guid id,
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ApprovalRequestDetailsResponse>(currentUserId, ApproverRoles);
        if (auth is not null)
        {
            return auth;
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.BadRequest("Rejection reason is required.");
        }

        var approval = await GetScopedApprovalWithDecisionsAsync(id, cancellationToken);
        if (approval is null)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.NotFound("Approval request was not found.");
        }

        await ExpireIfNeededAsync(approval, cancellationToken);

        if (approval.Status != ApprovalStatus.Pending)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.Conflict("Only pending approval requests can be rejected.");
        }

        var policy = await GetPolicyAsync(approval.CompoundId, approval.ActionType, cancellationToken);
        var policyAccess = await ValidateApprovalPolicyDecisionAccessAsync(currentUserId!.Value, policy);
        if (policyAccess is not null)
        {
            return policyAccess;
        }

        if (!policy.AllowSelfApproval && approval.RequestedByUserId == currentUserId.Value)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.Forbidden("Self-approval is not allowed for this action.");
        }

        var now = DateTime.UtcNow;
        approval.Status = ApprovalStatus.Rejected;
        approval.ExecutionStatus = ApprovalExecutionStatus.Cancelled;
        approval.LastDecisionByUserId = currentUserId!.Value;
        approval.DecisionReason = request.Reason.Trim();
        approval.DecidedAtUtc = now;
        approval.UpdatedAtUtc = now;

        AddDecision(approval, currentUserId.Value, ApprovalDecisionType.Rejected, request.Reason);
        AddActivityEvent(
            approval,
            currentUserId.Value,
            ActivityEventType.ApprovalRejected,
            "Approval rejected",
            $"Approval request for {approval.ActionType} was rejected.");
        await AddNotificationForRequesterAsync(
            approval,
            currentUserId.Value,
            NotificationEventType.ApprovalRejected,
            "Approval request rejected",
            $"Your approval request for {approval.ActionType} was rejected.",
            cancellationToken);
        await AddApprovalAuditAsync(
            approval,
            currentUserId.Value,
            AuditActionType.ApprovalRejected,
            AuditSeverity.High,
            "Approval request rejected.",
            approval.DecisionReason,
            [
                new AuditLogChangeRecord(nameof(ApprovalRequest.Status), ApprovalStatus.Pending.ToString(), ApprovalStatus.Rejected.ToString()),
                new AuditLogChangeRecord(nameof(ApprovalRequest.ExecutionStatus), ApprovalExecutionStatus.NotReady.ToString(), ApprovalExecutionStatus.Cancelled.ToString())
            ],
            cancellationToken);

        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<ApprovalRequestDetailsResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }
        return ServiceResult<ApprovalRequestDetailsResponse>.Success(ToDetailsResponse(approval));
    }

    public async Task<ServiceResult<ApprovalRequestDetailsResponse>> CancelAsync(
        Guid? currentUserId,
        Guid id,
        ApprovalDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ApprovalRequestDetailsResponse>(currentUserId, RequesterRoles);
        if (auth is not null)
        {
            return auth;
        }

        var approval = await GetScopedApprovalWithDecisionsAsync(id, cancellationToken);
        if (approval is null)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.NotFound("Approval request was not found.");
        }

        await ExpireIfNeededAsync(approval, cancellationToken);

        if (approval.Status != ApprovalStatus.Pending)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.Conflict("Only pending approval requests can be cancelled.");
        }

        var isRequester = approval.RequestedByUserId == currentUserId!.Value;
        var isPrivilegedAdmin = await IsInAnyRoleAsync(currentUserId.Value, ApproverRoles);
        if (!isRequester && !isPrivilegedAdmin)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.Forbidden("Only the requester or a privileged admin can cancel this approval request.");
        }

        var now = DateTime.UtcNow;
        approval.Status = ApprovalStatus.Cancelled;
        approval.ExecutionStatus = ApprovalExecutionStatus.Cancelled;
        approval.LastDecisionByUserId = currentUserId!.Value;
        approval.DecisionReason = NormalizeOptional(request.Reason);
        approval.CancelledAtUtc = now;
        approval.UpdatedAtUtc = now;

        AddDecision(approval, currentUserId.Value, ApprovalDecisionType.Cancelled, request.Reason);
        AddActivityEvent(
            approval,
            currentUserId.Value,
            ActivityEventType.ApprovalCancelled,
            "Approval cancelled",
            $"Approval request for {approval.ActionType} was cancelled.");
        await AddNotificationForRequesterAsync(
            approval,
            currentUserId.Value,
            NotificationEventType.ApprovalCancelled,
            "Approval request cancelled",
            $"Approval request for {approval.ActionType} was cancelled.",
            cancellationToken);
        await AddApprovalAuditAsync(
            approval,
            currentUserId.Value,
            AuditActionType.ApprovalCancelled,
            AuditSeverity.High,
            "Approval request cancelled.",
            approval.DecisionReason,
            [
                new AuditLogChangeRecord(nameof(ApprovalRequest.Status), ApprovalStatus.Pending.ToString(), ApprovalStatus.Cancelled.ToString()),
                new AuditLogChangeRecord(nameof(ApprovalRequest.ExecutionStatus), ApprovalExecutionStatus.NotReady.ToString(), ApprovalExecutionStatus.Cancelled.ToString())
            ],
            cancellationToken);

        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<ApprovalRequestDetailsResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }
        return ServiceResult<ApprovalRequestDetailsResponse>.Success(ToDetailsResponse(approval));
    }

    public async Task<ServiceResult<ApprovalRequestDetailsResponse>> MarkExecutedAsync(
        Guid? currentUserId,
        Guid id,
        MarkApprovalExecutedRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ApprovalRequestDetailsResponse>(currentUserId, ExecutorRoles);
        if (auth is not null)
        {
            return auth;
        }

        var approval = await GetScopedApprovalWithDecisionsAsync(id, cancellationToken);
        if (approval is null)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.NotFound("Approval request was not found.");
        }

        if (approval.Status != ApprovalStatus.Approved
            || approval.ExecutionStatus != ApprovalExecutionStatus.ReadyForExecution)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.Conflict("Approval request must be approved before it can be marked as executed.");
        }

        if (approval.RequestedByUserId == currentUserId!.Value)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.Forbidden("The requester cannot mark their own approval request as executed.");
        }

        if (approval.LastDecisionByUserId == currentUserId.Value)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.Forbidden("The approver cannot also execute the same approval request.");
        }

        var now = DateTime.UtcNow;
        approval.Status = ApprovalStatus.Executed;
        approval.ExecutionStatus = ApprovalExecutionStatus.Executed;
        approval.ExecutedAtUtc = now;
        approval.ExecutedByUserId = currentUserId!.Value;
        approval.ExecutionNotes = NormalizeOptional(request.Notes);
        approval.UpdatedAtUtc = now;

        AddDecision(approval, currentUserId.Value, ApprovalDecisionType.MarkedExecuted, request.Notes);
        AddActivityEvent(
            approval,
            currentUserId.Value,
            ActivityEventType.ApprovalExecuted,
            "Approval executed",
            $"Approval request for {approval.ActionType} was marked as executed.");
        await AddNotificationForRequesterAsync(
            approval,
            currentUserId.Value,
            NotificationEventType.ApprovalExecuted,
            "Approval request executed",
            $"Approved action {approval.ActionType} was marked as executed.",
            cancellationToken);
        await AddApprovalAuditAsync(
            approval,
            currentUserId.Value,
            AuditActionType.ApprovalExecuted,
            AuditSeverity.Critical,
            "Approval request marked as executed.",
            approval.ExecutionNotes,
            [
                new AuditLogChangeRecord(nameof(ApprovalRequest.Status), ApprovalStatus.Approved.ToString(), ApprovalStatus.Executed.ToString()),
                new AuditLogChangeRecord(nameof(ApprovalRequest.ExecutionStatus), ApprovalExecutionStatus.ReadyForExecution.ToString(), ApprovalExecutionStatus.Executed.ToString())
            ],
            cancellationToken);

        var concurrencyFailure = await SaveChangesWithConcurrencyGuardAsync<ApprovalRequestDetailsResponse>(cancellationToken);
        if (concurrencyFailure is not null)
        {
            return concurrencyFailure;
        }
        return ServiceResult<ApprovalRequestDetailsResponse>.Success(ToDetailsResponse(approval));
    }

    public async Task<ServiceResult<ApprovalDashboardResponse>> GetDashboardAsync(
        Guid? currentUserId,
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        var auth = await ValidateCurrentUserAsync<ApprovalDashboardResponse>(currentUserId, RequesterRoles);
        if (auth is not null)
        {
            return auth;
        }

        if (compoundId == Guid.Empty)
        {
            return ServiceResult<ApprovalDashboardResponse>.BadRequest("Compound id is invalid.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (compoundId.HasValue && !scope.CanAccess(compoundId.Value))
        {
            return ServiceResult<ApprovalDashboardResponse>.Forbidden("You do not have access to this compound.");
        }

        var now = DateTime.UtcNow;
        var approvals = dbContext.ApprovalRequests
            .AsNoTracking()
            .ApplyCompoundAccess(scope, approval => approval.CompoundId);

        if (compoundId.HasValue)
        {
            approvals = approvals.Where(approval => approval.CompoundId == compoundId.Value);
        }

        var pendingCount = await approvals.CountAsync(approval => approval.Status == ApprovalStatus.Pending, cancellationToken);
        var approvedCount = await approvals.CountAsync(approval => approval.Status == ApprovalStatus.Approved, cancellationToken);
        var rejectedCount = await approvals.CountAsync(approval => approval.Status == ApprovalStatus.Rejected, cancellationToken);
        var cancelledCount = await approvals.CountAsync(approval => approval.Status == ApprovalStatus.Cancelled, cancellationToken);
        var executedCount = await approvals.CountAsync(approval => approval.Status == ApprovalStatus.Executed, cancellationToken);
        var overdueCount = await approvals.CountAsync(
            approval => approval.Status == ApprovalStatus.Pending && approval.DueAtUtc.HasValue && approval.DueAtUtc < now,
            cancellationToken);
        var highPriorityPendingCount = await approvals.CountAsync(
            approval => approval.Status == ApprovalStatus.Pending && approval.Priority >= ApprovalPriority.High,
            cancellationToken);
        var oldestPendingCreatedAtUtc = await approvals
            .Where(approval => approval.Status == ApprovalStatus.Pending)
            .OrderBy(approval => approval.CreatedAtUtc)
            .Select(approval => (DateTime?)approval.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return ServiceResult<ApprovalDashboardResponse>.Success(new ApprovalDashboardResponse(
            pendingCount,
            approvedCount,
            rejectedCount,
            cancelledCount,
            executedCount,
            overdueCount,
            highPriorityPendingCount,
            oldestPendingCreatedAtUtc));
    }

    private async Task<ServiceResult<ApprovalRequestResponse>?> ValidateCreateRequestAsync(
        CreateApprovalRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return ServiceResult<ApprovalRequestResponse>.BadRequest("Compound id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<ApprovalRequestResponse>.BadRequest("Approval request reason is required.");
        }

        if (request.Reason.Length > 1000)
        {
            return ServiceResult<ApprovalRequestResponse>.BadRequest("Approval request reason cannot exceed 1000 characters.");
        }

        var payloadValidation = ValidateActionPayload(request.RequestPayloadJson);
        if (payloadValidation is not null)
        {
            return ServiceResult<ApprovalRequestResponse>.BadRequest(payloadValidation);
        }

        var actionValidation = ValidateActionEntityCompatibility(request.ActionType, request.EntityType, request.EntityId);
        if (actionValidation is not null)
        {
            return ServiceResult<ApprovalRequestResponse>.BadRequest(actionValidation);
        }

        var compoundExists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(compound => compound.Id == request.CompoundId && compound.IsActive, cancellationToken);
        if (!compoundExists)
        {
            return ServiceResult<ApprovalRequestResponse>.NotFound("Compound was not found.");
        }

        var entityValidation = await ValidateCompoundScope(request.CompoundId, request.EntityType, request.EntityId, cancellationToken);
        if (entityValidation is not null)
        {
            return ServiceResult<ApprovalRequestResponse>.BadRequest(entityValidation);
        }

        return null;
    }

    private async Task<ApprovalRequest?> GetScopedApprovalWithDecisionsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        return await dbContext.ApprovalRequests
            .ApplyCompoundAccess(scope, approval => approval.CompoundId)
            .Include(approval => approval.Decisions.OrderByDescending(decision => decision.CreatedAtUtc))
            .SingleOrDefaultAsync(approval => approval.Id == id, cancellationToken);
    }

    private async Task<ApprovalPolicySnapshot> GetPolicyAsync(
        Guid compoundId,
        ApprovalActionType actionType,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.ApprovalPolicies
            .AsNoTracking()
            .Where(item => item.ActionType == actionType && item.IsEnabled)
            .Where(item => item.CompoundId == compoundId || item.CompoundId == null)
            .OrderByDescending(item => item.CompoundId == compoundId)
            .FirstOrDefaultAsync(cancellationToken);

        return policy is null
            ? ApprovalPolicySnapshot.Default(actionType)
            : new ApprovalPolicySnapshot(
                policy.ActionType,
                policy.IsEnabled,
                policy.AllowSelfApproval,
                policy.DefaultPriority,
                policy.ExpireAfterHours <= 0 ? 72 : policy.ExpireAfterHours,
                policy.RequiredApproverRoles);
    }

    private static string? ValidateActionPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        if (payloadJson.Length > 8000)
        {
            return "Request payload cannot exceed 8000 characters.";
        }

        try
        {
            using var _ = JsonDocument.Parse(payloadJson);
            return null;
        }
        catch (JsonException)
        {
            return "Request payload must be valid JSON.";
        }
    }

    private static string? ValidateActionEntityCompatibility(
        ApprovalActionType actionType,
        ApprovalEntityType entityType,
        Guid? entityId)
    {
        if (entityType == ApprovalEntityType.None && entityId.HasValue)
        {
            return "Entity id cannot be provided when entity type is None.";
        }

        if (entityType != ApprovalEntityType.None
            && entityType != ApprovalEntityType.Other
            && !entityId.HasValue)
        {
            return "Entity id is required for the selected entity type.";
        }

        var expectedEntityType = actionType switch
        {
            ApprovalActionType.RefundPayment => ApprovalEntityType.Payment,
            ApprovalActionType.CancelPayment => ApprovalEntityType.Payment,
            ApprovalActionType.CancelHighValuePayment => ApprovalEntityType.Payment,
            ApprovalActionType.WaiveViolationFine => ApprovalEntityType.ViolationFine,
            ApprovalActionType.AdjustUtilityBill => ApprovalEntityType.UtilityBill,
            ApprovalActionType.DeleteSensitiveDocument => ApprovalEntityType.Document,
            ApprovalActionType.CloseEscalatedDispute => ApprovalEntityType.Conversation,
            ApprovalActionType.OverrideResidentFinancialState => ApprovalEntityType.ResidentProfile,
            _ => (ApprovalEntityType?)null
        };

        if (expectedEntityType.HasValue && entityType != expectedEntityType.Value)
        {
            return $"Action {actionType} requires entity type {expectedEntityType.Value}.";
        }

        if (actionType == ApprovalActionType.ManualFinancialCorrection
            && entityType is not (ApprovalEntityType.None
                or ApprovalEntityType.ResidentProfile
                or ApprovalEntityType.Payment
                or ApprovalEntityType.UtilityBill
                or ApprovalEntityType.RentInvoice))
        {
            return "Manual financial correction can only target resident, payment, utility bill, rent invoice, or no direct entity.";
        }

        return null;
    }

    private async Task<string?> ValidateCompoundScope(
        Guid compoundId,
        ApprovalEntityType entityType,
        Guid? entityId,
        CancellationToken cancellationToken)
    {
        if (!entityId.HasValue || entityType == ApprovalEntityType.None || entityType == ApprovalEntityType.Other)
        {
            return null;
        }

        var id = entityId.Value;
        var matchesCompound = entityType switch
        {
            ApprovalEntityType.UtilityBill => await dbContext.UtilityBills
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken),
            ApprovalEntityType.Payment => await dbContext.Payments
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken),
            ApprovalEntityType.ViolationFine => await dbContext.ViolationFines
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken),
            ApprovalEntityType.Document => await dbContext.DocumentFiles
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken),
            ApprovalEntityType.Conversation => await dbContext.Conversations
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken),
            ApprovalEntityType.ResidentProfile => await dbContext.ResidentProfiles
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken),
            ApprovalEntityType.PropertyUnit => await dbContext.PropertyUnits
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken),
            ApprovalEntityType.RentInvoice => await dbContext.RentInvoices
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken),
            ApprovalEntityType.RentContract => await dbContext.RentContracts
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken),
            ApprovalEntityType.WorkOrder => await dbContext.WorkOrders
                .AsNoTracking()
                .AnyAsync(entity => entity.Id == id && entity.CompoundId == compoundId, cancellationToken),
            _ => false
        };

        return matchesCompound
            ? null
            : "Approval entity must belong to the selected compound.";
    }

    private static string? ValidateSearchQuery(ApprovalSearchQuery query)
    {
        if (query.CompoundId == Guid.Empty)
        {
            return "Compound id is invalid.";
        }

        if (query.RequestedByUserId == Guid.Empty)
        {
            return "Requester user id is invalid.";
        }

        if (query.LastDecisionByUserId == Guid.Empty)
        {
            return "Decision user id is invalid.";
        }

        if (query.EntityId == Guid.Empty)
        {
            return "Entity id is invalid.";
        }

        if (query.CreatedFromUtc.HasValue
            && query.CreatedToUtc.HasValue
            && query.CreatedFromUtc.Value > query.CreatedToUtc.Value)
        {
            return "CreatedFromUtc must be before CreatedToUtc.";
        }

        return null;
    }

    private static IQueryable<ApprovalRequest> ApplySearchFilters(
        IQueryable<ApprovalRequest> approvals,
        ApprovalSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            approvals = approvals.Where(approval => approval.CompoundId == query.CompoundId.Value);
        }

        if (query.RequestedByUserId.HasValue)
        {
            approvals = approvals.Where(approval => approval.RequestedByUserId == query.RequestedByUserId.Value);
        }

        if (query.LastDecisionByUserId.HasValue)
        {
            approvals = approvals.Where(approval => approval.LastDecisionByUserId == query.LastDecisionByUserId.Value);
        }

        if (query.ActionType.HasValue)
        {
            approvals = approvals.Where(approval => approval.ActionType == query.ActionType.Value);
        }

        if (query.EntityType.HasValue)
        {
            approvals = approvals.Where(approval => approval.EntityType == query.EntityType.Value);
        }

        if (query.EntityId.HasValue)
        {
            approvals = approvals.Where(approval => approval.EntityId == query.EntityId.Value);
        }

        if (query.Status.HasValue)
        {
            approvals = approvals.Where(approval => approval.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            approvals = approvals.Where(approval => approval.Priority == query.Priority.Value);
        }

        if (query.CreatedFromUtc.HasValue)
        {
            approvals = approvals.Where(approval => approval.CreatedAtUtc >= query.CreatedFromUtc.Value);
        }

        if (query.CreatedToUtc.HasValue)
        {
            approvals = approvals.Where(approval => approval.CreatedAtUtc <= query.CreatedToUtc.Value);
        }

        if (query.IsOverdue.HasValue)
        {
            var now = DateTime.UtcNow;
            approvals = query.IsOverdue.Value
                ? approvals.Where(approval => approval.Status == ApprovalStatus.Pending
                    && approval.DueAtUtc.HasValue
                    && approval.DueAtUtc < now)
                : approvals.Where(approval => !(approval.Status == ApprovalStatus.Pending
                    && approval.DueAtUtc.HasValue
                    && approval.DueAtUtc < now));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            approvals = approvals.Where(approval =>
                approval.Reason.Contains(term)
                || (approval.DecisionReason != null && approval.DecisionReason.Contains(term))
                || (approval.ExecutionNotes != null && approval.ExecutionNotes.Contains(term)));
        }

        return approvals;
    }

    private async Task ExpireIfNeededAsync(
        ApprovalRequest approval,
        CancellationToken cancellationToken)
    {
        if (approval.Status != ApprovalStatus.Pending
            || !approval.DueAtUtc.HasValue
            || approval.DueAtUtc.Value >= DateTime.UtcNow)
        {
            return;
        }

        approval.Status = ApprovalStatus.Expired;
        approval.ExecutionStatus = ApprovalExecutionStatus.Cancelled;
        approval.UpdatedAtUtc = DateTime.UtcNow;
        AddDecision(approval, approval.RequestedByUserId, ApprovalDecisionType.Expired, "Approval request expired automatically.");
        AddActivityEvent(
            approval,
            approval.RequestedByUserId,
            ActivityEventType.ApprovalCancelled,
            "Approval expired",
            $"Approval request for {approval.ActionType} expired.");

        await dbContext.SaveChangesAsync(cancellationToken);
    }


    private async Task<ServiceResult<T>?> SaveChangesWithConcurrencyGuardAsync<T>(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<T>.Conflict("The approval request was updated by another operation. Reload and try again.");
        }
    }

    private async Task<ServiceResult<T>?> ValidateCurrentUserAsync<T>(
        Guid? currentUserId,
        IReadOnlyCollection<UserRole> allowedRoles)
    {
        if (!currentUserId.HasValue || currentUserId.Value == Guid.Empty)
        {
            return ServiceResult<T>.Forbidden("Authentication is required.");
        }

        var user = await userManager.FindByIdAsync(currentUserId.Value.ToString());
        if (user is null)
        {
            return ServiceResult<T>.Forbidden("Authenticated user was not found.");
        }

        if (!await IsInAnyRoleAsync(user, allowedRoles))
        {
            return ServiceResult<T>.Forbidden("Current user is not allowed to perform approval operations.");
        }

        return null;
    }

    private async Task<bool> IsInAnyRoleAsync(Guid userId, IReadOnlyCollection<UserRole> roles)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is not null && await IsInAnyRoleAsync(user, roles);
    }

    private async Task<ServiceResult<ApprovalRequestDetailsResponse>?> ValidateApprovalPolicyDecisionAccessAsync(
        Guid currentUserId,
        ApprovalPolicySnapshot policy)
    {
        var requiredRoles = ParseRequiredApproverRoles(policy.RequiredApproverRoles);
        if (requiredRoles.Length == 0)
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.Conflict("Approval policy does not define valid approver roles.");
        }

        if (!await IsInAnyRoleAsync(currentUserId, requiredRoles))
        {
            return ServiceResult<ApprovalRequestDetailsResponse>.Forbidden("Current user is not allowed to decide this approval request by policy.");
        }

        return null;
    }

    private static UserRole[] ParseRequiredApproverRoles(string requiredApproverRoles)
    {
        if (string.IsNullOrWhiteSpace(requiredApproverRoles))
        {
            return [];
        }

        var roles = new List<UserRole>();
        foreach (var roleName in requiredApproverRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<UserRole>(roleName, ignoreCase: true, out var role))
            {
                return [];
            }

            if (!roles.Contains(role))
            {
                roles.Add(role);
            }
        }

        return roles.ToArray();
    }

    private async Task<bool> IsInAnyRoleAsync(ApplicationUser user, IReadOnlyCollection<UserRole> roles)
    {
        foreach (var role in roles)
        {
            if (await userManager.IsInRoleAsync(user, role.ToString()))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddDecision(
        ApprovalRequest approval,
        Guid userId,
        ApprovalDecisionType decisionType,
        string? reason)
    {
        approval.Decisions.Add(new ApprovalDecision
        {
            ApprovalRequestId = approval.Id,
            DecidedByUserId = userId,
            DecisionType = decisionType,
            Reason = string.IsNullOrWhiteSpace(reason) ? decisionType.ToString() : reason.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private void AddActivityEvent(
        ApprovalRequest approval,
        Guid actorUserId,
        ActivityEventType eventType,
        string title,
        string description)
    {
        dbContext.ActivityEvents.Add(new ActivityEvent
        {
            CompoundId = approval.CompoundId,
            ActorUserId = actorUserId,
            EventType = eventType,
            Title = title,
            Description = description,
            EntityType = ActivityEntityType.ApprovalRequest,
            EntityId = approval.Id,
            CreatedAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new
            {
                approvalRequestId = approval.Id,
                approval.ActionType,
                approval.Status,
                approval.Priority
            })
        });
    }

    private async Task AddApprovalAuditAsync(
        ApprovalRequest approval,
        Guid actorUserId,
        AuditActionType actionType,
        AuditSeverity severity,
        string description,
        string? reason,
        IReadOnlyCollection<AuditLogChangeRecord> changes,
        CancellationToken cancellationToken)
    {
        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            approval.CompoundId,
            null,
            actorUserId,
            RoleNames.ApprovalDecisionManagers,
            actionType,
            AuditEntityType.ApprovalRequest,
            approval.Id,
            severity,
            "Approvals",
            description,
            reason,
            AfterValuesJson: JsonSerializer.Serialize(new
            {
                approval.Id,
                approval.ActionType,
                approval.EntityType,
                approval.EntityId,
                approval.Status,
                approval.ExecutionStatus,
                approval.Priority
            }),
            MetadataJson: JsonSerializer.Serialize(new
            {
                approval.RequestedByUserId,
                approval.LastDecisionByUserId,
                approval.ExecutedByUserId,
                approval.DueAtUtc
            }),
            Changes: changes), cancellationToken);
    }

    private async Task AddNotificationForRequesterAsync(
        ApprovalRequest approval,
        Guid actorUserId,
        NotificationEventType eventType,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var requester = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == approval.RequestedByUserId)
            .Select(user => new { user.Id, user.FullName, user.Email })
            .SingleOrDefaultAsync(cancellationToken);

        AddNotification(
            approval,
            actorUserId,
            requester?.Id,
            string.IsNullOrWhiteSpace(requester?.FullName) ? "Requester" : requester.FullName,
            eventType,
            subject,
            body);
    }

    private void AddNotification(
        ApprovalRequest approval,
        Guid actorUserId,
        Guid? recipientUserId,
        string recipientName,
        NotificationEventType eventType,
        string subject,
        string body)
    {
        dbContext.NotificationOutboxes.Add(new NotificationOutbox
        {
            CompoundId = approval.CompoundId,
            RecipientUserId = recipientUserId,
            Channel = NotificationChannel.InApp,
            EventType = eventType,
            Priority = MapPriority(approval.Priority),
            RecipientName = recipientName,
            Subject = subject,
            Body = body,
            RelatedEntityType = NotificationRelatedEntityType.ApprovalRequest,
            RelatedEntityId = approval.Id,
            MetadataJson = JsonSerializer.Serialize(new
            {
                approvalRequestId = approval.Id,
                approval.ActionType,
                approval.Status,
                actorUserId
            }),
            ScheduledAtUtc = DateTime.UtcNow,
            CreatedByUserId = actorUserId
        });
    }

    private static NotificationPriority MapPriority(ApprovalPriority priority)
    {
        return priority switch
        {
            ApprovalPriority.Low => NotificationPriority.Low,
            ApprovalPriority.High => NotificationPriority.High,
            ApprovalPriority.Critical => NotificationPriority.Urgent,
            _ => NotificationPriority.Normal
        };
    }

    private static ApprovalRequestResponse ToResponse(ApprovalRequest approval)
    {
        return new ApprovalRequestResponse(
            approval.Id,
            approval.CompoundId,
            approval.RequestedByUserId,
            approval.LastDecisionByUserId,
            approval.ActionType,
            approval.EntityType,
            approval.EntityId,
            approval.Status,
            approval.Priority,
            approval.ExecutionStatus,
            approval.Reason,
            approval.DecisionReason,
            approval.ExecutionNotes,
            approval.CreatedAtUtc,
            approval.UpdatedAtUtc,
            approval.DueAtUtc,
            approval.DecidedAtUtc,
            approval.CancelledAtUtc,
            approval.ExecutedAtUtc,
            approval.ExecutedByUserId,
            IsOverdue(approval));
    }

    private static ApprovalRequestDetailsResponse ToDetailsResponse(ApprovalRequest approval)
    {
        return new ApprovalRequestDetailsResponse(
            approval.Id,
            approval.CompoundId,
            approval.RequestedByUserId,
            approval.LastDecisionByUserId,
            approval.ActionType,
            approval.EntityType,
            approval.EntityId,
            approval.Status,
            approval.Priority,
            approval.ExecutionStatus,
            approval.Reason,
            approval.RequestPayloadJson,
            approval.DecisionReason,
            approval.ExecutionNotes,
            approval.CreatedAtUtc,
            approval.UpdatedAtUtc,
            approval.DueAtUtc,
            approval.DecidedAtUtc,
            approval.CancelledAtUtc,
            approval.ExecutedAtUtc,
            approval.ExecutedByUserId,
            IsOverdue(approval),
            approval.Decisions
                .OrderByDescending(decision => decision.CreatedAtUtc)
                .Select(ToDecisionResponse)
                .ToArray());
    }

    private static ApprovalDecisionResponse ToDecisionResponse(ApprovalDecision decision)
    {
        return new ApprovalDecisionResponse(
            decision.Id,
            decision.ApprovalRequestId,
            decision.DecidedByUserId,
            decision.DecisionType,
            decision.Reason,
            decision.CreatedAtUtc);
    }

    private static bool IsOverdue(ApprovalRequest approval)
    {
        return approval.Status == ApprovalStatus.Pending
            && approval.DueAtUtc.HasValue
            && approval.DueAtUtc.Value < DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record ApprovalPolicySnapshot(
        ApprovalActionType ActionType,
        bool IsEnabled,
        bool AllowSelfApproval,
        ApprovalPriority DefaultPriority,
        int ExpireAfterHours,
        string RequiredApproverRoles)
    {
        public static ApprovalPolicySnapshot Default(ApprovalActionType actionType)
        {
            return new ApprovalPolicySnapshot(
                actionType,
                IsEnabled: true,
                AllowSelfApproval: false,
                DefaultPriority: ApprovalPriority.Normal,
                ExpireAfterHours: 72,
                RequiredApproverRoles: "SuperAdmin,CompoundAdmin");
        }
    }
}

