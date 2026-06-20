using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Communication;
using DARAK.Api.DTOs.Financial;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Helpers;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ConversationService(
    ApplicationDbContext dbContext,
    IConversationAdvisoryService advisoryService,
    IActivityTimelineService activityTimelineService,
    ICompoundAccessService? compoundAccessService = null,
    IResidentFinancialHealthService? financialHealthService = null)
    : IConversationService
{
    private const int MaximumMessageLength = 4000;
    private const int MaximumReasonLength = 1000;

    public async Task<ServiceResult<ConversationResponse>> CreateConversationAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreateRequest(request);
        if (validation is not null)
        {
            return ServiceResult<ConversationResponse>.BadRequest(validation);
        }

        var adminScopeValidation = await ValidateAdminCreateCompoundScopeAsync(request, cancellationToken);
        if (adminScopeValidation is not null)
        {
            return ServiceResult<ConversationResponse>.Forbidden(adminScopeValidation);
        }

        var residentProfile = await dbContext.ResidentProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                resident => resident.Id == request.ResidentProfileId
                    && resident.CompoundId == request.CompoundId
                    && resident.IsActive,
                cancellationToken);

        if (residentProfile is null)
        {
            return ServiceResult<ConversationResponse>.NotFound("Resident profile was not found in the selected compound.");
        }

        if (request.PropertyUnitId.HasValue)
        {
            var propertyUnitValidation = await ValidateConversationPropertyUnitAccessAsync(
                request.CompoundId,
                request.ResidentProfileId,
                request.PropertyUnitId.Value,
                cancellationToken);
            if (propertyUnitValidation is not null)
            {
                return propertyUnitValidation;
            }
        }

        var linkedEntityValidation = await ValidateConversationLinkedEntityAsync(
            request.CompoundId,
            request.ResidentProfileId,
            residentProfile.UserId,
            request.LinkedEntityType,
            request.LinkedEntityId,
            cancellationToken);
        if (linkedEntityValidation is not null)
        {
            return linkedEntityValidation;
        }

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            CompoundId = request.CompoundId,
            ResidentProfileId = request.ResidentProfileId,
            PropertyUnitId = request.PropertyUnitId,
            Topic = request.Topic,
            IssueType = request.IssueType,
            Priority = request.PriorityOverride ?? advisoryService.GetDefaultPriority(request.IssueType),
            Status = ConversationStatus.PendingAdminReply,
            LinkedEntityType = request.LinkedEntityType,
            LinkedEntityId = request.LinkedEntityId,
            CreatedAtUtc = now,
            LastMessageAtUtc = now,
            LastResidentMessageAtUtc = now
        };

        conversation.Messages.Add(new ConversationMessage
        {
            SenderUserId = request.CreatedByUserId,
            MessageType = ConversationMessageType.ResidentMessage,
            Visibility = ConversationMessageVisibility.ResidentVisible,
            Body = request.InitialMessage.Trim(),
            CreatedAtUtc = now
        });

        conversation.Messages.Add(new ConversationMessage
        {
            SenderUserId = null,
            MessageType = ConversationMessageType.SystemMessage,
            Visibility = ConversationMessageVisibility.ResidentVisible,
            Body = string.IsNullOrWhiteSpace(request.OpeningSystemMessage)
                ? "Conversation opened."
                : request.OpeningSystemMessage.Trim(),
            CreatedAtUtc = now.AddMilliseconds(1)
        });

        dbContext.Conversations.Add(conversation);
        await SaveConversationChangesAsync(cancellationToken);

        await activityTimelineService.RecordAsync(
            new RecordActivityEventRequest(
                conversation.CompoundId,
                conversation.ResidentProfileId,
                conversation.PropertyUnitId,
                request.CreatedByUserId,
                ActivityEventType.ConversationOpened,
                "Conversation opened",
                $"Conversation opened for {conversation.Topic} / {conversation.IssueType}.",
                ActivityEntityType.Conversation,
                conversation.Id),
            cancellationToken);

        dbContext.ChangeTracker.Clear();
        var savedConversation = await LoadConversationAsync(conversation.Id, includeMessages: true, cancellationToken);
        return ServiceResult<ConversationResponse>.Success(ToResponse(savedConversation!, includeInternalMessages: true));
    }

    public async Task<ServiceResult<ConversationResponse>> GetConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (conversationId == Guid.Empty)
        {
            return ServiceResult<ConversationResponse>.BadRequest("Conversation id is required.");
        }

        var conversation = await LoadConversationAsync(conversationId, includeMessages: true, cancellationToken);

        return conversation is null
            ? ServiceResult<ConversationResponse>.NotFound("Conversation was not found.")
            : ServiceResult<ConversationResponse>.Success(ToResponse(conversation, includeInternalMessages: true));
    }

    public async Task<ServiceResult<ResidentConversationResponse>> OpenResidentConversationAsync(
        Guid? currentUserId,
        ResidentOpenConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentConversationResponse>.BadRequest("Current user is invalid.");
        }

        var residentProfile = await GetCurrentResidentProfileQuery(currentUserId.Value)
            .OrderByDescending(resident => resident.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (residentProfile is null)
        {
            return ServiceResult<ResidentConversationResponse>.NotFound("Resident profile was not found for the current user.");
        }

        Guid? propertyUnitId = request.PropertyUnitId;
        if (propertyUnitId.HasValue)
        {
            var ownsUnit = await dbContext.OccupancyRecords
                .AsNoTracking()
                .AnyAsync(record =>
                    record.ResidentProfileId == residentProfile.Id
                    && record.CompoundId == residentProfile.CompoundId
                    && record.PropertyUnitId == propertyUnitId.Value
                    && record.OccupancyStatus == OccupancyStatus.Active,
                    cancellationToken);

            if (!ownsUnit)
            {
                return ServiceResult<ResidentConversationResponse>.Forbidden("Current resident cannot open a conversation for this unit.");
            }
        }
        else
        {
            propertyUnitId = await dbContext.OccupancyRecords
                .AsNoTracking()
                .Where(record =>
                    record.ResidentProfileId == residentProfile.Id
                    && record.CompoundId == residentProfile.CompoundId
                    && record.OccupancyStatus == OccupancyStatus.Active)
                .OrderByDescending(record => record.StartDate)
                .Select(record => (Guid?)record.PropertyUnitId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var linkedEntityValidation = await ValidateResidentLinkedEntityAccessAsync(
            currentUserId.Value,
            residentProfile.Id,
            residentProfile.CompoundId,
            request.LinkedEntityType,
            request.LinkedEntityId,
            cancellationToken);
        if (linkedEntityValidation is not null)
        {
            return linkedEntityValidation;
        }

        var result = await CreateConversationAsync(
            new CreateConversationRequest(
                residentProfile.CompoundId,
                residentProfile.Id,
                propertyUnitId,
                request.Topic,
                request.IssueType,
                request.LinkedEntityType,
                request.LinkedEntityId,
                request.InitialMessage,
                currentUserId),
            cancellationToken);

        return result.IsSuccess
            ? ServiceResult<ResidentConversationResponse>.Success(ToResidentResponse(result.Value!))
            : ToResidentConversationResult(result);
    }

    private async Task<ServiceResult<ConversationResponse>?> ValidateConversationPropertyUnitAccessAsync(
        Guid compoundId,
        Guid residentProfileId,
        Guid propertyUnitId,
        CancellationToken cancellationToken)
    {
        var hasActiveResidentOccupancy = await (
            from record in dbContext.OccupancyRecords.AsNoTracking()
            join unit in dbContext.PropertyUnits.AsNoTracking() on record.PropertyUnitId equals unit.Id
            where record.ResidentProfileId == residentProfileId
                && record.CompoundId == compoundId
                && record.PropertyUnitId == propertyUnitId
                && record.OccupancyStatus == OccupancyStatus.Active
                && unit.CompoundId == compoundId
                && unit.IsActive
            select record.Id)
            .AnyAsync(cancellationToken);

        return hasActiveResidentOccupancy
            ? null
            : ServiceResult<ConversationResponse>.NotFound("Property unit was not found for the selected resident.");
    }

    private async Task<ServiceResult<ConversationResponse>?> ValidateConversationLinkedEntityAsync(
        Guid compoundId,
        Guid residentProfileId,
        Guid residentUserId,
        ConversationLinkedEntityType linkedEntityType,
        Guid? linkedEntityId,
        CancellationToken cancellationToken)
    {
        if (linkedEntityType == ConversationLinkedEntityType.None)
        {
            return null;
        }

        if (!linkedEntityId.HasValue)
        {
            return ServiceResult<ConversationResponse>.BadRequest("Linked entity id is required when linked entity type is provided.");
        }

        var entityId = linkedEntityId.Value;
        var canAccess = linkedEntityType switch
        {
            ConversationLinkedEntityType.UtilityBill => await dbContext.UtilityBills
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.Payment => await dbContext.Payments
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.MaintenanceRequest => await dbContext.MaintenanceRequests
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.Complaint => await dbContext.Complaints
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.VisitorPass => await dbContext.VisitorPasses
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.ViolationFine => await dbContext.ViolationFines
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.Document => await dbContext.DocumentFiles
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && !entity.IsDeleted
                    && (entity.OwnerUserId == residentUserId
                        || (entity.RelatedEntityType == nameof(ResidentProfile)
                            && entity.RelatedEntityId == residentProfileId)),
                    cancellationToken),

            ConversationLinkedEntityType.RentContract => await dbContext.RentContracts
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.PropertyUnit => await (
                from record in dbContext.OccupancyRecords.AsNoTracking()
                join unit in dbContext.PropertyUnits.AsNoTracking() on record.PropertyUnitId equals unit.Id
                where record.PropertyUnitId == entityId
                    && record.CompoundId == compoundId
                    && record.ResidentProfileId == residentProfileId
                    && record.OccupancyStatus == OccupancyStatus.Active
                    && unit.CompoundId == compoundId
                    && unit.IsActive
                select record.Id)
                .AnyAsync(cancellationToken),

            _ => false
        };

        return canAccess
            ? null
            : ServiceResult<ConversationResponse>.NotFound("Linked entity was not found for the selected resident.");
    }

    public async Task<ServiceResult<PagedResult<ResidentConversationResponse>>> SearchResidentConversationsAsync(
        Guid? currentUserId,
        ConversationSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<PagedResult<ResidentConversationResponse>>.BadRequest("Current user is invalid.");
        }

        var residentProfileIds = await GetCurrentResidentProfileQuery(currentUserId.Value)
            .Select(resident => resident.Id)
            .ToArrayAsync(cancellationToken);

        if (residentProfileIds.Length == 0)
        {
            return ServiceResult<PagedResult<ResidentConversationResponse>>.Success(
                new PagedResult<ResidentConversationResponse>([], query.PageNumber, query.PageSize, 0));
        }

        var conversations = ApplyResidentConversationFilters(
            dbContext.Conversations
                .AsNoTracking()
                .Where(conversation => residentProfileIds.Contains(conversation.ResidentProfileId)),
            query);

        return ServiceResult<PagedResult<ResidentConversationResponse>>.Success(
            await ToResidentPagedResultAsync(conversations, query, cancellationToken));
    }

    public async Task<ServiceResult<ResidentConversationResponse>> GetResidentConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentConversationResponse>.BadRequest("Current user is invalid.");
        }

        var conversation = await LoadConversationAsync(conversationId, includeMessages: true, cancellationToken);
        if (conversation is null)
        {
            return ServiceResult<ResidentConversationResponse>.NotFound("Conversation was not found.");
        }

        if (!await IsConversationOwnedByCurrentResidentAsync(conversation.ResidentProfileId, currentUserId.Value, cancellationToken))
        {
            return ServiceResult<ResidentConversationResponse>.NotFound("Conversation was not found.");
        }

        return ServiceResult<ResidentConversationResponse>.Success(ToResidentResponse(conversation));
    }

    public async Task<ServiceResult<ResidentConversationResponse>> AddResidentMessageAsync(
        Guid? currentUserId,
        Guid conversationId,
        SendConversationMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentConversationResponse>.BadRequest("Current user is invalid.");
        }

        var bodyValidation = ValidateMessageBody(request.Body);
        if (bodyValidation is not null)
        {
            return ServiceResult<ResidentConversationResponse>.BadRequest(bodyValidation);
        }

        var conversation = await LoadConversationForUpdateAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ServiceResult<ResidentConversationResponse>.NotFound("Conversation was not found.");
        }

        if (!await IsConversationOwnedByCurrentResidentAsync(conversation.ResidentProfileId, currentUserId.Value, cancellationToken))
        {
            return ServiceResult<ResidentConversationResponse>.NotFound("Conversation was not found.");
        }

        if (conversation.Status is ConversationStatus.Resolved or ConversationStatus.Closed)
        {
            return ServiceResult<ResidentConversationResponse>.Conflict("Resolved or closed conversations must be reopened before the resident can send a new message.");
        }

        var now = DateTime.UtcNow;
        AddConversationMessage(
            conversation.Id,
            currentUserId,
            ConversationMessageType.ResidentMessage,
            ConversationMessageVisibility.ResidentVisible,
            request.Body.Trim(),
            now);
        conversation.Status = ConversationStatus.PendingAdminReply;
        conversation.LastMessageAtUtc = now;
        conversation.LastResidentMessageAtUtc = now;
        conversation.UpdatedAtUtc = now;

        await SaveConversationChangesAsync(cancellationToken);

        await RecordConversationActivityAsync(
            conversation,
            currentUserId,
            ActivityEventType.ConversationMessageSent,
            "Resident message sent",
            "Resident sent a message in the support conversation.",
            cancellationToken);

        return await ReloadResidentConversationResultAsync(conversation.Id, cancellationToken);
    }

    public async Task<ServiceResult<ResidentConversationResponse>> ReopenResidentConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        ReopenConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentConversationResponse>.BadRequest("Current user is invalid.");
        }

        var reasonValidation = ValidateReason(request.Reason, required: true);
        if (reasonValidation is not null)
        {
            return ServiceResult<ResidentConversationResponse>.BadRequest(reasonValidation);
        }

        var conversation = await LoadConversationForUpdateAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ServiceResult<ResidentConversationResponse>.NotFound("Conversation was not found.");
        }

        if (!await IsConversationOwnedByCurrentResidentAsync(conversation.ResidentProfileId, currentUserId.Value, cancellationToken))
        {
            return ServiceResult<ResidentConversationResponse>.NotFound("Conversation was not found.");
        }

        if (conversation.Status is not (ConversationStatus.Resolved or ConversationStatus.Closed))
        {
            return ServiceResult<ResidentConversationResponse>.Conflict("Only resolved or closed conversations can be reopened.");
        }

        var now = DateTime.UtcNow;
        conversation.Status = ConversationStatus.Reopened;
        conversation.ReopenCount += 1;
        conversation.LastReopenReason = request.Reason.Trim();
        conversation.ReopenedAtUtc = now;
        conversation.ReopenedByResidentId = conversation.ResidentProfileId;
        conversation.LastMessageAtUtc = now;
        conversation.LastResidentMessageAtUtc = now;
        conversation.UpdatedAtUtc = now;
        conversation.ClosedAtUtc = null;
        conversation.ResolvedAtUtc = null;

        if (conversation.ReopenCount > 1 && conversation.EscalationLevel == ConversationEscalationLevel.None)
        {
            conversation.EscalationLevel = ConversationEscalationLevel.NeedsAttention;
            conversation.EscalatedAtUtc = now;
            conversation.EscalationReason = "Conversation reopened more than once.";
        }

        AddConversationMessage(
            conversation.Id,
            currentUserId,
            ConversationMessageType.SystemMessage,
            ConversationMessageVisibility.ResidentVisible,
            $"Conversation reopened by resident. Reason: {request.Reason.Trim()}",
            now);

        await SaveConversationChangesAsync(cancellationToken);

        await RecordConversationActivityAsync(
            conversation,
            currentUserId,
            ActivityEventType.ConversationReopened,
            "Conversation reopened",
            $"Resident reopened the conversation. Reason: {request.Reason.Trim()}",
            cancellationToken);

        return await ReloadResidentConversationResultAsync(conversation.Id, cancellationToken);
    }

    public async Task<ServiceResult<ResidentBillDisputeResponse>> OpenResidentBillDisputeAsync(
        Guid? currentUserId,
        Guid billId,
        ResidentBillDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ResidentBillDisputeResponse>.BadRequest("Current user is invalid.");
        }

        if (billId == Guid.Empty)
        {
            return ServiceResult<ResidentBillDisputeResponse>.BadRequest("Utility bill id is required.");
        }

        var issueValidation = ValidateBillDisputeIssueType(request.IssueType);
        if (issueValidation is not null)
        {
            return ServiceResult<ResidentBillDisputeResponse>.BadRequest(issueValidation);
        }

        var messageValidation = ValidateMessageBody(request.Message, "Dispute message");
        if (messageValidation is not null)
        {
            return ServiceResult<ResidentBillDisputeResponse>.BadRequest(messageValidation);
        }

        var residentProfileIds = await GetCurrentResidentProfileQuery(currentUserId.Value)
            .Select(resident => resident.Id)
            .ToArrayAsync(cancellationToken);

        if (residentProfileIds.Length == 0)
        {
            return ServiceResult<ResidentBillDisputeResponse>.NotFound("Utility bill was not found.");
        }

        var bill = await dbContext.UtilityBills
            .AsNoTracking()
            .Include(utilityBill => utilityBill.PropertyUnit)
            .Where(utilityBill => utilityBill.Id == billId)
            .Where(utilityBill => utilityBill.ResidentProfileId.HasValue
                && residentProfileIds.Contains(utilityBill.ResidentProfileId.Value))
            .FirstOrDefaultAsync(cancellationToken);

        if (bill is null)
        {
            return ServiceResult<ResidentBillDisputeResponse>.NotFound("Utility bill was not found.");
        }

        if (bill.ResidentProfileId is null)
        {
            return ServiceResult<ResidentBillDisputeResponse>.NotFound("Utility bill was not found.");
        }

        if (bill.BillStatus == BillStatus.Cancelled)
        {
            return ServiceResult<ResidentBillDisputeResponse>.Conflict("Cancelled utility bills cannot be disputed.");
        }

        var existingDispute = await dbContext.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.ResidentProfileId == bill.ResidentProfileId.Value
                && conversation.LinkedEntityType == ConversationLinkedEntityType.UtilityBill
                && conversation.LinkedEntityId == bill.Id
                && conversation.Topic == ConversationTopic.Billing
                && conversation.Status != ConversationStatus.Resolved
                && conversation.Status != ConversationStatus.Closed)
            .OrderByDescending(conversation => conversation.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingDispute is not null)
        {
            return ServiceResult<ResidentBillDisputeResponse>.Success(
                ToBillDisputeResponse(existingDispute, bill, createdNew: false));
        }

        var createResult = await CreateConversationAsync(
            new CreateConversationRequest(
                bill.CompoundId,
                bill.ResidentProfileId.Value,
                bill.PropertyUnitId,
                ConversationTopic.Billing,
                request.IssueType,
                ConversationLinkedEntityType.UtilityBill,
                bill.Id,
                request.Message,
                currentUserId,
                GetBillDisputePriority(bill, request.IssueType),
                "Conversation opened from bill objection."),
            cancellationToken);

        if (!createResult.IsSuccess)
        {
            return createResult.Status switch
            {
                ServiceResultStatus.NotFound => ServiceResult<ResidentBillDisputeResponse>.NotFound(createResult.Message ?? "Conversation could not be created."),
                ServiceResultStatus.Forbidden => ServiceResult<ResidentBillDisputeResponse>.Forbidden(createResult.Message ?? "Conversation could not be created."),
                ServiceResultStatus.Conflict => ServiceResult<ResidentBillDisputeResponse>.Conflict(createResult.Message ?? "Conversation could not be created."),
                _ => ServiceResult<ResidentBillDisputeResponse>.BadRequest(createResult.Message ?? "Conversation could not be created.")
            };
        }

        await activityTimelineService.RecordAsync(
            new RecordActivityEventRequest(
                bill.CompoundId,
                bill.ResidentProfileId,
                bill.PropertyUnitId,
                currentUserId,
                ActivityEventType.BillDisputeOpened,
                "Bill dispute opened",
                $"Resident opened a billing dispute for bill {bill.BillNumber}.",
                ActivityEntityType.UtilityBill,
                bill.Id),
            cancellationToken);

        return ServiceResult<ResidentBillDisputeResponse>.Success(
            ToBillDisputeResponse(createResult.Value!, bill, createdNew: true));
    }

    public async Task<ServiceResult<PagedResult<ConversationResponse>>> SearchAdminConversationsAsync(
        Guid? currentUserId,
        ConversationSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<PagedResult<ConversationResponse>>.BadRequest("Current user is invalid.");
        }

        var scope = await GetAdminScopeAsync(cancellationToken);
        var conversations = ApplyConversationFilters(
            dbContext.Conversations
                .AsNoTracking()
                .ApplyCompoundAccess(scope, conversation => conversation.CompoundId),
            query,
            includeInternalMessagesInSearch: true);

        return ServiceResult<PagedResult<ConversationResponse>>.Success(
            await ToPagedResultAsync(conversations, query, includeInternalMessages: true, cancellationToken));
    }

    public async Task<ServiceResult<AdminConversationDetailsResponse>> GetAdminConversationDetailsAsync(
        Guid? currentUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<AdminConversationDetailsResponse>.BadRequest("Current user is invalid.");
        }

        var conversation = await LoadConversationAsync(conversationId, includeMessages: true, cancellationToken);
        if (conversation is null)
        {
            return ServiceResult<AdminConversationDetailsResponse>.NotFound("Conversation was not found.");
        }

        if (!await CanAdminAccessConversationAsync(conversation, cancellationToken))
        {
            return ServiceResult<AdminConversationDetailsResponse>.NotFound("Conversation was not found.");
        }

        var details = new AdminConversationDetailsResponse(
            ToResponse(conversation, includeInternalMessages: true),
            advisoryService.GetAdvisoryFlags(conversation.IssueType, conversation.LinkedEntityType),
            await BuildResidentContextAsync(currentUserId, conversation.ResidentProfileId, cancellationToken));

        return ServiceResult<AdminConversationDetailsResponse>.Success(details);
    }

    public async Task<ServiceResult<SupportDashboardResponse>> GetSupportDashboardAsync(
        Guid? currentUserId,
        SupportDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<SupportDashboardResponse>.BadRequest("Current user is invalid.");
        }

        if (query.CompoundId == Guid.Empty)
        {
            return ServiceResult<SupportDashboardResponse>.BadRequest("Compound id is invalid.");
        }

        var scope = await GetAdminScopeAsync(cancellationToken);
        var scopedConversations = dbContext.Conversations
            .AsNoTracking()
            .ApplyCompoundAccess(scope, conversation => conversation.CompoundId);

        if (query.CompoundId.HasValue)
        {
            if (!scope.CanAccess(query.CompoundId.Value))
            {
                return ServiceResult<SupportDashboardResponse>.Forbidden("Current user cannot access this compound.");
            }

            scopedConversations = scopedConversations
                .Where(conversation => conversation.CompoundId == query.CompoundId.Value);
        }

        var openConversations = scopedConversations
            .Where(conversation => conversation.Status != ConversationStatus.Resolved
                && conversation.Status != ConversationStatus.Closed);

        var openConversationsCount = await openConversations.CountAsync(cancellationToken);
        var urgentConversationsCount = await openConversations
            .CountAsync(conversation => conversation.Priority == ConversationPriority.Urgent, cancellationToken);
        var unassignedConversationsCount = await openConversations
            .CountAsync(conversation => conversation.AssignedToUserId == null, cancellationToken);
        var escalatedConversationsCount = await openConversations
            .CountAsync(conversation => conversation.EscalationLevel != ConversationEscalationLevel.None, cancellationToken);
        var reopenedConversationsCount = await openConversations
            .CountAsync(conversation => conversation.ReopenCount > 0 || conversation.Status == ConversationStatus.Reopened, cancellationToken);
        var billingDisputesCount = await openConversations
            .CountAsync(conversation => conversation.Topic == ConversationTopic.Billing
                && conversation.LinkedEntityType == ConversationLinkedEntityType.UtilityBill, cancellationToken);
        var assignedToMeCount = await openConversations
            .CountAsync(conversation => conversation.AssignedToUserId == currentUserId.Value, cancellationToken);

        var oldestOpenConversation = await openConversations
            .OrderBy(conversation => conversation.CreatedAtUtc)
            .ThenBy(conversation => conversation.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var openConversationRows = await openConversations
            .Include(conversation => conversation.ResidentProfile)
            .OrderByDescending(conversation => conversation.LastMessageAtUtc)
            .ToListAsync(cancellationToken);

        var highRiskResidents = openConversationRows
            .GroupBy(conversation => new
            {
                conversation.ResidentProfileId,
                conversation.ResidentProfile.FullName
            })
            .Select(group => BuildHighRiskResident(group.Key.ResidentProfileId, group.Key.FullName, group))
            .Where(resident => resident.RiskReasons.Count > 0)
            .OrderByDescending(resident => resident.EscalatedConversationsCount)
            .ThenByDescending(resident => resident.UrgentConversationsCount)
            .ThenByDescending(resident => resident.ReopenedConversationsCount)
            .ThenByDescending(resident => resident.OpenConversationsCount)
            .Take(10)
            .ToArray();

        var response = new SupportDashboardResponse(
            openConversationsCount,
            urgentConversationsCount,
            unassignedConversationsCount,
            escalatedConversationsCount,
            reopenedConversationsCount,
            billingDisputesCount,
            assignedToMeCount,
            oldestOpenConversation is null ? null : ToSummaryResponse(oldestOpenConversation),
            highRiskResidents);

        return ServiceResult<SupportDashboardResponse>.Success(response);
    }

    public async Task<ServiceResult<ConversationResponse>> AddAdminReplyAsync(
        Guid? currentUserId,
        Guid conversationId,
        SendConversationMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ConversationResponse>.BadRequest("Current user is invalid.");
        }

        var bodyValidation = ValidateMessageBody(request.Body);
        if (bodyValidation is not null)
        {
            return ServiceResult<ConversationResponse>.BadRequest(bodyValidation);
        }

        var conversation = await LoadConversationForUpdateAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        if (!await CanAdminAccessConversationAsync(conversation, cancellationToken))
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        if (conversation.Status == ConversationStatus.Closed)
        {
            return ServiceResult<ConversationResponse>.Conflict("Closed conversations cannot receive admin replies.");
        }

        var now = DateTime.UtcNow;
        AddConversationMessage(
            conversation.Id,
            currentUserId,
            ConversationMessageType.AdminMessage,
            ConversationMessageVisibility.ResidentVisible,
            request.Body.Trim(),
            now);
        conversation.Status = ConversationStatus.PendingResidentReply;
        conversation.LastMessageAtUtc = now;
        conversation.LastAdminMessageAtUtc = now;
        conversation.UpdatedAtUtc = now;

        await SaveConversationChangesAsync(cancellationToken);

        await RecordConversationActivityAsync(
            conversation,
            currentUserId,
            ActivityEventType.ConversationMessageSent,
            "Admin reply sent",
            "Admin replied to the support conversation.",
            cancellationToken);

        return await ReloadConversationResultAsync(conversation.Id, includeInternalMessages: true, cancellationToken);
    }

    public async Task<ServiceResult<ConversationResponse>> AssignConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        AssignConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ConversationResponse>.BadRequest("Current user is invalid.");
        }

        if (request.AssignedToUserId == Guid.Empty)
        {
            return ServiceResult<ConversationResponse>.BadRequest("Assigned user id is required.");
        }

        var reasonValidation = ValidateReason(request.Reason, required: false, maxLength: 500);
        if (reasonValidation is not null)
        {
            return ServiceResult<ConversationResponse>.BadRequest(reasonValidation);
        }

        var conversation = await LoadConversationForUpdateAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        if (!await CanAdminAccessConversationAsync(conversation, cancellationToken))
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        var assigneeValidation = await ValidateAssigneeAsync(
            request.AssignedToUserId,
            conversation.CompoundId,
            cancellationToken);
        if (assigneeValidation is not null)
        {
            return assigneeValidation;
        }

        var previousAssignee = conversation.AssignedToUserId;
        var now = DateTime.UtcNow;
        conversation.AssignedToUserId = request.AssignedToUserId;
        conversation.AssignedByUserId = currentUserId;
        conversation.AssignedAtUtc = now;
        conversation.LastAssignmentReason = TrimOrNull(request.Reason);
        conversation.UpdatedAtUtc = now;

        var systemBody = previousAssignee.HasValue
            ? $"Conversation transferred from user {previousAssignee.Value} to user {request.AssignedToUserId}."
            : $"Conversation assigned to user {request.AssignedToUserId}.";

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            systemBody += $" Reason: {request.Reason.Trim()}";
        }

        AddConversationMessage(
            conversation.Id,
            currentUserId,
            ConversationMessageType.SystemMessage,
            ConversationMessageVisibility.InternalOnly,
            systemBody,
            now);
        conversation.LastMessageAtUtc = now;

        await SaveConversationChangesAsync(cancellationToken);

        await RecordConversationActivityAsync(
            conversation,
            currentUserId,
            ActivityEventType.ConversationAssigned,
            previousAssignee.HasValue ? "Conversation transferred" : "Conversation assigned",
            systemBody,
            cancellationToken);

        return await ReloadConversationResultAsync(conversation.Id, includeInternalMessages: true, cancellationToken);
    }

    public async Task<ServiceResult<ConversationResponse>> ChangePriorityAsync(
        Guid? currentUserId,
        Guid conversationId,
        ChangeConversationPriorityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ConversationResponse>.BadRequest("Current user is invalid.");
        }

        var reasonValidation = ValidateReason(request.Reason, required: false, maxLength: 500);
        if (reasonValidation is not null)
        {
            return ServiceResult<ConversationResponse>.BadRequest(reasonValidation);
        }

        var conversation = await LoadConversationForUpdateAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        if (!await CanAdminAccessConversationAsync(conversation, cancellationToken))
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        if (conversation.Priority == request.Priority)
        {
            return await ReloadConversationResultAsync(conversation.Id, includeInternalMessages: true, cancellationToken);
        }

        var oldPriority = conversation.Priority;
        var now = DateTime.UtcNow;
        conversation.Priority = request.Priority;
        conversation.UpdatedAtUtc = now;
        conversation.LastMessageAtUtc = now;

        var systemBody = $"Priority changed from {oldPriority} to {request.Priority}.";
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            systemBody += $" Reason: {request.Reason.Trim()}";
        }

        AddConversationMessage(
            conversation.Id,
            currentUserId,
            ConversationMessageType.SystemMessage,
            ConversationMessageVisibility.InternalOnly,
            systemBody,
            now);

        await SaveConversationChangesAsync(cancellationToken);

        await RecordConversationActivityAsync(
            conversation,
            currentUserId,
            ActivityEventType.ConversationPriorityChanged,
            "Conversation priority changed",
            systemBody,
            cancellationToken);

        return await ReloadConversationResultAsync(conversation.Id, includeInternalMessages: true, cancellationToken);
    }

    public async Task<ServiceResult<ConversationResponse>> EscalateConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        EscalateConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ConversationResponse>.BadRequest("Current user is invalid.");
        }

        if (request.EscalationLevel == ConversationEscalationLevel.None)
        {
            return ServiceResult<ConversationResponse>.BadRequest("Escalation level must be greater than None.");
        }

        var reasonValidation = ValidateReason(request.Reason, required: true, maxLength: 500);
        if (reasonValidation is not null)
        {
            return ServiceResult<ConversationResponse>.BadRequest(reasonValidation);
        }

        var conversation = await LoadConversationForUpdateAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        if (!await CanAdminAccessConversationAsync(conversation, cancellationToken))
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        var now = DateTime.UtcNow;
        var oldLevel = conversation.EscalationLevel;
        conversation.EscalationLevel = request.EscalationLevel;
        conversation.EscalatedAtUtc = now;
        conversation.EscalatedByUserId = currentUserId;
        conversation.EscalationReason = request.Reason.Trim();
        conversation.LastMessageAtUtc = now;
        conversation.UpdatedAtUtc = now;

        var systemBody = $"Escalation level changed from {oldLevel} to {request.EscalationLevel}. Reason: {request.Reason.Trim()}";
        AddConversationMessage(
            conversation.Id,
            currentUserId,
            ConversationMessageType.SystemMessage,
            ConversationMessageVisibility.InternalOnly,
            systemBody,
            now);

        await SaveConversationChangesAsync(cancellationToken);

        await RecordConversationActivityAsync(
            conversation,
            currentUserId,
            ActivityEventType.ConversationEscalated,
            "Conversation escalated",
            systemBody,
            cancellationToken);

        return await ReloadConversationResultAsync(conversation.Id, includeInternalMessages: true, cancellationToken);
    }

    public async Task<ServiceResult<ConversationResponse>> AddInternalNoteAsync(
        Guid? currentUserId,
        Guid conversationId,
        AddInternalNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ConversationResponse>.BadRequest("Current user is invalid.");
        }

        var bodyValidation = ValidateMessageBody(request.Body);
        if (bodyValidation is not null)
        {
            return ServiceResult<ConversationResponse>.BadRequest(bodyValidation);
        }

        var conversation = await LoadConversationForUpdateAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        if (!await CanAdminAccessConversationAsync(conversation, cancellationToken))
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        var now = DateTime.UtcNow;
        AddConversationMessage(
            conversation.Id,
            currentUserId,
            ConversationMessageType.InternalNote,
            ConversationMessageVisibility.InternalOnly,
            request.Body.Trim(),
            now);

        AddConversationMessage(
            conversation.Id,
            currentUserId,
            ConversationMessageType.SystemMessage,
            ConversationMessageVisibility.InternalOnly,
            "Internal note added.",
            now.AddMilliseconds(1));

        conversation.LastMessageAtUtc = now;
        conversation.UpdatedAtUtc = now;

        await SaveConversationChangesAsync(cancellationToken);

        await RecordConversationActivityAsync(
            conversation,
            currentUserId,
            ActivityEventType.ConversationMessageSent,
            "Internal note added",
            "Admin added an internal note to the conversation.",
            cancellationToken);

        return await ReloadConversationResultAsync(conversation.Id, includeInternalMessages: true, cancellationToken);
    }

    public Task<ServiceResult<ConversationResponse>> ResolveConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        CompleteConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        return CompleteConversationAsync(
            currentUserId,
            conversationId,
            request,
            ConversationStatus.Resolved,
            ActivityEventType.ConversationResolved,
            "Conversation resolved",
            cancellationToken);
    }

    public Task<ServiceResult<ConversationResponse>> CloseConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        CompleteConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        return CompleteConversationAsync(
            currentUserId,
            conversationId,
            request,
            ConversationStatus.Closed,
            ActivityEventType.ConversationClosed,
            "Conversation closed",
            cancellationToken);
    }

    private async Task<ServiceResult<ConversationResponse>> CompleteConversationAsync(
        Guid? currentUserId,
        Guid conversationId,
        CompleteConversationRequest request,
        ConversationStatus targetStatus,
        ActivityEventType eventType,
        string title,
        CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<ConversationResponse>.BadRequest("Current user is invalid.");
        }

        var reasonValidation = ValidateReason(request.Reason, required: false);
        if (reasonValidation is not null)
        {
            return ServiceResult<ConversationResponse>.BadRequest(reasonValidation);
        }

        var conversation = await LoadConversationForUpdateAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        if (!await CanAdminAccessConversationAsync(conversation, cancellationToken))
        {
            return ServiceResult<ConversationResponse>.NotFound("Conversation was not found.");
        }

        if (conversation.Status == targetStatus)
        {
            return await ReloadConversationResultAsync(conversation.Id, includeInternalMessages: true, cancellationToken);
        }

        var now = DateTime.UtcNow;
        conversation.Status = targetStatus;
        conversation.UpdatedAtUtc = now;
        conversation.LastMessageAtUtc = now;
        if (targetStatus == ConversationStatus.Resolved)
        {
            conversation.ResolvedAtUtc = now;
            conversation.ClosedAtUtc = null;
        }
        else
        {
            conversation.ClosedAtUtc = now;
        }

        var systemBody = targetStatus == ConversationStatus.Resolved
            ? "Conversation resolved."
            : "Conversation closed.";
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            systemBody += $" Reason: {request.Reason.Trim()}";
        }

        AddConversationMessage(
            conversation.Id,
            currentUserId,
            ConversationMessageType.SystemMessage,
            ConversationMessageVisibility.ResidentVisible,
            systemBody,
            now);

        await SaveConversationChangesAsync(cancellationToken);

        await RecordConversationActivityAsync(
            conversation,
            currentUserId,
            eventType,
            title,
            systemBody,
            cancellationToken);

        return await ReloadConversationResultAsync(conversation.Id, includeInternalMessages: true, cancellationToken);
    }

    private ConversationMessage AddConversationMessage(
        Guid conversationId,
        Guid? senderUserId,
        ConversationMessageType messageType,
        ConversationMessageVisibility visibility,
        string body,
        DateTime createdAtUtc)
    {
        var message = new ConversationMessage
        {
            ConversationId = conversationId,
            SenderUserId = senderUserId,
            MessageType = messageType,
            Visibility = visibility,
            Body = body,
            CreatedAtUtc = createdAtUtc
        };

        dbContext.ConversationMessages.Add(message);
        return message;
    }

    private async Task<ServiceResult<ConversationResponse>> ReloadConversationResultAsync(
        Guid conversationId,
        bool includeInternalMessages,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var conversation = await LoadConversationAsync(conversationId, includeMessages: true, cancellationToken);
        return conversation is null
            ? ServiceResult<ConversationResponse>.NotFound("Conversation was not found.")
            : ServiceResult<ConversationResponse>.Success(ToResponse(conversation, includeInternalMessages));
    }

    private async Task<ServiceResult<ResidentConversationResponse>> ReloadResidentConversationResultAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var conversation = await LoadConversationAsync(conversationId, includeMessages: true, cancellationToken);
        return conversation is null
            ? ServiceResult<ResidentConversationResponse>.NotFound("Conversation was not found.")
            : ServiceResult<ResidentConversationResponse>.Success(ToResidentResponse(conversation));
    }

    private static ServiceResult<ResidentConversationResponse> ToResidentConversationResult(
        ServiceResult<ConversationResponse> result)
    {
        return result.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<ResidentConversationResponse>.NotFound(result.Message ?? "Conversation was not found."),
            ServiceResultStatus.BadRequest => result.Errors is null
                ? ServiceResult<ResidentConversationResponse>.BadRequest(result.Message ?? "Request is invalid.")
                : ServiceResult<ResidentConversationResponse>.BadRequest(result.Message ?? "Request is invalid.", result.Errors),
            ServiceResultStatus.Conflict => ServiceResult<ResidentConversationResponse>.Conflict(result.Message ?? "Conversation request conflicts with the current state."),
            ServiceResultStatus.Forbidden => ServiceResult<ResidentConversationResponse>.Forbidden(result.Message ?? "Current user cannot access this conversation."),
            _ => ServiceResult<ResidentConversationResponse>.BadRequest(result.Message ?? "Conversation request failed.")
        };
    }


    private async Task SaveConversationChangesAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private async Task<Conversation?> LoadConversationForUpdateAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (conversationId == Guid.Empty)
        {
            return null;
        }

        return await dbContext.Conversations
            .SingleOrDefaultAsync(conversation => conversation.Id == conversationId, cancellationToken);
    }

    private async Task<Conversation?> LoadConversationAsync(
        Guid conversationId,
        bool includeMessages,
        CancellationToken cancellationToken)
    {
        if (conversationId == Guid.Empty)
        {
            return null;
        }

        dbContext.ChangeTracker.Clear();

        var query = dbContext.Conversations.AsNoTracking();
        if (includeMessages)
        {
            query = query.Include(conversation => conversation.Messages);
        }

        return await query.SingleOrDefaultAsync(conversation => conversation.Id == conversationId, cancellationToken);
    }

    private IQueryable<ResidentProfile> GetCurrentResidentProfileQuery(Guid currentUserId)
    {
        return dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(resident => resident.UserId == currentUserId && resident.IsActive);
    }

    private async Task<bool> IsConversationOwnedByCurrentResidentAsync(
        Guid residentProfileId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ResidentProfiles
            .AsNoTracking()
            .AnyAsync(resident =>
                resident.Id == residentProfileId
                && resident.UserId == currentUserId
                && resident.IsActive,
                cancellationToken);
    }

    private async Task<CompoundAccessScope> GetAdminScopeAsync(CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            ? new CompoundAccessScope(true, true, [])
            : await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
    }

    private async Task<bool> CanAdminAccessConversationAsync(
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null)
        {
            return true;
        }

        return await compoundAccessService.CanCurrentUserAccessCompoundAsync(
            conversation.CompoundId,
            cancellationToken);
    }

    private static IQueryable<Conversation> ApplyResidentConversationFilters(
        IQueryable<Conversation> conversations,
        ConversationSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.Status.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.Priority == query.Priority.Value);
        }

        if (query.Topic.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.Topic == query.Topic.Value);
        }

        if (query.IssueType.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.IssueType == query.IssueType.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim();
            conversations = conversations.Where(conversation =>
                conversation.Messages.Any(message =>
                    message.Visibility == ConversationMessageVisibility.ResidentVisible
                    && message.Body.Contains(searchTerm)));
        }

        return conversations;
    }

    private static IQueryable<Conversation> ApplyConversationFilters(
        IQueryable<Conversation> conversations,
        ConversationSearchQuery query,
        bool includeInternalMessagesInSearch)
    {
        if (query.CompoundId.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.CompoundId == query.CompoundId.Value);
        }

        if (query.ResidentProfileId.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.ResidentProfileId == query.ResidentProfileId.Value);
        }

        if (query.PropertyUnitId.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.PropertyUnitId == query.PropertyUnitId.Value);
        }

        if (query.Status.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.Priority == query.Priority.Value);
        }

        if (query.Topic.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.Topic == query.Topic.Value);
        }

        if (query.IssueType.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.IssueType == query.IssueType.Value);
        }

        if (query.EscalationLevel.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.EscalationLevel == query.EscalationLevel.Value);
        }

        if (query.AssignedToUserId.HasValue)
        {
            conversations = conversations.Where(conversation => conversation.AssignedToUserId == query.AssignedToUserId.Value);
        }

        if (query.IsUnassigned.HasValue)
        {
            conversations = query.IsUnassigned.Value
                ? conversations.Where(conversation => conversation.AssignedToUserId == null)
                : conversations.Where(conversation => conversation.AssignedToUserId != null);
        }

        if (query.IsEscalated.HasValue)
        {
            conversations = query.IsEscalated.Value
                ? conversations.Where(conversation => conversation.EscalationLevel != ConversationEscalationLevel.None)
                : conversations.Where(conversation => conversation.EscalationLevel == ConversationEscalationLevel.None);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim();
            conversations = conversations.Where(conversation =>
                conversation.Messages.Any(message =>
                    (includeInternalMessagesInSearch
                        || message.Visibility == ConversationMessageVisibility.ResidentVisible)
                    && message.Body.Contains(searchTerm)));
        }

        return conversations;
    }

    private static async Task<PagedResult<ConversationResponse>> ToPagedResultAsync(
        IQueryable<Conversation> conversations,
        ConversationSearchQuery query,
        bool includeInternalMessages,
        CancellationToken cancellationToken)
    {
        var totalCount = await conversations.CountAsync(cancellationToken);

        var items = await conversations
            .Include(conversation => conversation.Messages)
            .OrderByDescending(conversation => conversation.LastMessageAtUtc)
            .ThenByDescending(conversation => conversation.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PagedResult<ConversationResponse>(
            items.Select(item => ToResponse(item, includeInternalMessages)).ToArray(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    private static async Task<PagedResult<ResidentConversationResponse>> ToResidentPagedResultAsync(
        IQueryable<Conversation> conversations,
        ConversationSearchQuery query,
        CancellationToken cancellationToken)
    {
        var totalCount = await conversations.CountAsync(cancellationToken);

        var items = await conversations
            .Include(conversation => conversation.Messages)
            .OrderByDescending(conversation => conversation.LastMessageAtUtc)
            .ThenByDescending(conversation => conversation.CreatedAtUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PagedResult<ResidentConversationResponse>(
            items.Select(ToResidentResponse).ToArray(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    private async Task<ConversationResidentContextPanelResponse> BuildResidentContextAsync(
        Guid? currentUserId,
        Guid residentProfileId,
        CancellationToken cancellationToken)
    {
        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .SingleAsync(profile => profile.Id == residentProfileId, cancellationToken);

        var occupancy = await dbContext.OccupancyRecords
            .AsNoTracking()
            .Include(record => record.PropertyUnit)
            .Where(record =>
                record.ResidentProfileId == residentProfileId
                && record.OccupancyStatus == OccupancyStatus.Active)
            .OrderByDescending(record => record.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        var openConversationsCount = await dbContext.Conversations
            .AsNoTracking()
            .CountAsync(conversation =>
                conversation.ResidentProfileId == residentProfileId
                && conversation.Status != ConversationStatus.Resolved
                && conversation.Status != ConversationStatus.Closed,
                cancellationToken);

        var recentActivity = await dbContext.ActivityEvents
            .AsNoTracking()
            .Where(activityEvent =>
                activityEvent.ResidentProfileId == residentProfileId
                || (activityEvent.EntityType == ActivityEntityType.ResidentProfile
                    && activityEvent.EntityId == residentProfileId))
            .OrderByDescending(activityEvent => activityEvent.CreatedAtUtc)
            .ThenByDescending(activityEvent => activityEvent.Id)
            .Take(5)
            .Select(activityEvent => new ActivityEventResponse(
                activityEvent.Id,
                activityEvent.CompoundId,
                activityEvent.ResidentProfileId,
                activityEvent.PropertyUnitId,
                activityEvent.ActorUserId,
                activityEvent.EventType,
                activityEvent.Title,
                activityEvent.Description,
                activityEvent.EntityType,
                activityEvent.EntityId,
                activityEvent.CreatedAtUtc,
                activityEvent.MetadataJson))
            .ToArrayAsync(cancellationToken);

        ResidentFinancialHealthResponse? financialHealth = null;
        if (financialHealthService is not null)
        {
            var healthResult = await financialHealthService.GetAdminResidentFinancialHealthAsync(
                currentUserId,
                residentProfileId,
                cancellationToken);

            if (healthResult.IsSuccess)
            {
                financialHealth = healthResult.Value;
            }
        }

        return new ConversationResidentContextPanelResponse(
            resident.Id,
            resident.FullName,
            occupancy?.PropertyUnitId,
            occupancy?.PropertyUnit.UnitNumber,
            occupancy?.OccupancyType.ToString(),
            financialHealth?.TotalOutstandingAmount ?? 0m,
            financialHealth?.OverdueAmount ?? 0m,
            financialHealth?.LastPaymentDate,
            openConversationsCount,
            financialHealth?.Status ?? ResidentFinancialHealthStatus.Healthy,
            financialHealth?.RiskReasons ?? [],
            recentActivity);
    }

    private async Task RecordConversationActivityAsync(
        Conversation conversation,
        Guid? actorUserId,
        ActivityEventType eventType,
        string title,
        string description,
        CancellationToken cancellationToken)
    {
        await activityTimelineService.RecordAsync(
            new RecordActivityEventRequest(
                conversation.CompoundId,
                conversation.ResidentProfileId,
                conversation.PropertyUnitId,
                actorUserId,
                eventType,
                title,
                description,
                ActivityEntityType.Conversation,
                conversation.Id),
            cancellationToken);
    }

    private ConversationPriority GetBillDisputePriority(UtilityBill bill, ConversationIssueType issueType)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (bill.BillStatus == BillStatus.Overdue || bill.DueDate < today)
        {
            return ConversationPriority.High;
        }

        if (issueType == ConversationIssueType.BillingHighAmount
            && CalculateRemainingBillAmount(bill) >= 500_000m)
        {
            return ConversationPriority.High;
        }

        return advisoryService.GetDefaultPriority(issueType);
    }

    private static string? ValidateBillDisputeIssueType(ConversationIssueType issueType)
    {
        return issueType is ConversationIssueType.BillingHighAmount or ConversationIssueType.BillingMeterReadingIssue
            ? null
            : "Bill disputes support only BillingHighAmount or BillingMeterReadingIssue issue types.";
    }

    private async Task<ServiceResult<ResidentConversationResponse>?> ValidateResidentLinkedEntityAccessAsync(
        Guid currentUserId,
        Guid residentProfileId,
        Guid compoundId,
        ConversationLinkedEntityType linkedEntityType,
        Guid? linkedEntityId,
        CancellationToken cancellationToken)
    {
        if (linkedEntityType == ConversationLinkedEntityType.None)
        {
            return null;
        }

        if (!linkedEntityId.HasValue)
        {
            return ServiceResult<ResidentConversationResponse>.BadRequest("Linked entity id is required when linked entity type is provided.");
        }

        var entityId = linkedEntityId.Value;
        var canAccess = linkedEntityType switch
        {
            ConversationLinkedEntityType.UtilityBill => await dbContext.UtilityBills
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.Payment => await dbContext.Payments
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.MaintenanceRequest => await dbContext.MaintenanceRequests
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.Complaint => await dbContext.Complaints
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.VisitorPass => await dbContext.VisitorPasses
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.ViolationFine => await dbContext.ViolationFines
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.Document => await dbContext.DocumentFiles
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && !entity.IsDeleted
                    && (entity.OwnerUserId == currentUserId
                        || (entity.RelatedEntityType == nameof(ResidentProfile)
                            && entity.RelatedEntityId == residentProfileId)),
                    cancellationToken),

            ConversationLinkedEntityType.RentContract => await dbContext.RentContracts
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.Id == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId,
                    cancellationToken),

            ConversationLinkedEntityType.PropertyUnit => await dbContext.OccupancyRecords
                .AsNoTracking()
                .AnyAsync(entity =>
                    entity.PropertyUnitId == entityId
                    && entity.CompoundId == compoundId
                    && entity.ResidentProfileId == residentProfileId
                    && entity.OccupancyStatus == OccupancyStatus.Active,
                    cancellationToken),

            _ => false
        };

        return canAccess
            ? null
            : ServiceResult<ResidentConversationResponse>.NotFound("Linked entity was not found for the current resident.");
    }

    private async Task<ServiceResult<ConversationResponse>?> ValidateAssigneeAsync(
        Guid assignedToUserId,
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == assignedToUserId, cancellationToken);
        if (!userExists)
        {
            return ServiceResult<ConversationResponse>.NotFound("Assigned user was not found.");
        }

        var isResidentUser = await dbContext.ResidentProfiles
            .AsNoTracking()
            .AnyAsync(resident =>
                resident.UserId == assignedToUserId
                && resident.IsActive,
                cancellationToken);
        if (isResidentUser)
        {
            return ServiceResult<ConversationResponse>.BadRequest("Conversations can only be assigned to internal staff users.");
        }

        var isSuperAdmin = await dbContext.UserRoles
            .AsNoTracking()
            .Join(
                dbContext.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => new { userRole.UserId, RoleName = role.Name })
            .AnyAsync(role =>
                role.UserId == assignedToUserId
                && role.RoleName == nameof(UserRole.SuperAdmin),
                cancellationToken);
        if (isSuperAdmin)
        {
            return null;
        }

        var hasStaffAssignment = await dbContext.UserCompoundAssignments
            .AsNoTracking()
            .AnyAsync(assignment =>
                assignment.UserId == assignedToUserId
                && assignment.CompoundId == compoundId
                && assignment.IsActive
                && assignment.Role != UserRole.Resident,
                cancellationToken);

        return hasStaffAssignment
            ? null
            : ServiceResult<ConversationResponse>.Forbidden("Assigned user must have active staff access to this compound.");
    }

    private static ResidentBillDisputeResponse ToBillDisputeResponse(
        Conversation conversation,
        UtilityBill bill,
        bool createdNew)
    {
        return new ResidentBillDisputeResponse(
            conversation.Id,
            conversation.Status,
            conversation.Priority,
            conversation.IssueType,
            bill.Id,
            bill.BillNumber,
            bill.TotalAmount,
            bill.PaidAmount,
            CalculateRemainingBillAmount(bill),
            bill.DueDate,
            createdNew,
            conversation.CreatedAtUtc);
    }

    private static ResidentBillDisputeResponse ToBillDisputeResponse(
        ConversationResponse conversation,
        UtilityBill bill,
        bool createdNew)
    {
        return new ResidentBillDisputeResponse(
            conversation.Id,
            conversation.Status,
            conversation.Priority,
            conversation.IssueType,
            bill.Id,
            bill.BillNumber,
            bill.TotalAmount,
            bill.PaidAmount,
            CalculateRemainingBillAmount(bill),
            bill.DueDate,
            createdNew,
            conversation.CreatedAtUtc);
    }

    private static decimal CalculateRemainingBillAmount(UtilityBill bill)
    {
        return bill.BillStatus == BillStatus.Cancelled
            ? 0m
            : Math.Max(0m, bill.TotalAmount - bill.PaidAmount);
    }

    private async Task<string?> ValidateAdminCreateCompoundScopeAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        if (compoundAccessService is null || !request.CreatedByUserId.HasValue)
        {
            return null;
        }

        var actorHasManagementRole = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == request.CreatedByUserId.Value
                && role.Name != RoleNames.Resident
            select role.Id)
            .AnyAsync(cancellationToken);

        if (!actorHasManagementRole)
        {
            return null;
        }

        return await compoundAccessService.CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken)
            ? null
            : "Current user cannot open a conversation in this compound.";
    }

    private static string? ValidateCreateRequest(CreateConversationRequest request)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return "Compound id is required.";
        }

        if (request.ResidentProfileId == Guid.Empty)
        {
            return "Resident profile id is required.";
        }

        var messageValidation = ValidateMessageBody(request.InitialMessage, "Initial message");
        if (messageValidation is not null)
        {
            return messageValidation;
        }

        if (request.LinkedEntityType == ConversationLinkedEntityType.None && request.LinkedEntityId.HasValue)
        {
            return "Linked entity id cannot be provided when linked entity type is None.";
        }

        if (request.LinkedEntityType != ConversationLinkedEntityType.None && !request.LinkedEntityId.HasValue)
        {
            return "Linked entity id is required when linked entity type is provided.";
        }

        return null;
    }

    private static string? ValidateMessageBody(string body, string fieldName = "Message")
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"{fieldName} is required.";
        }

        if (body.Trim().Length > MaximumMessageLength)
        {
            return $"{fieldName} cannot exceed {MaximumMessageLength} characters.";
        }

        return null;
    }

    private static string? ValidateReason(string? reason, bool required, int maxLength = MaximumReasonLength)
    {
        if (required && string.IsNullOrWhiteSpace(reason))
        {
            return "Reason is required.";
        }

        if (!string.IsNullOrWhiteSpace(reason) && reason.Trim().Length > maxLength)
        {
            return $"Reason cannot exceed {maxLength} characters.";
        }

        return null;
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static ResidentConversationResponse ToResidentResponse(Conversation conversation)
    {
        var messages = conversation.Messages
            .Where(message => message.Visibility == ConversationMessageVisibility.ResidentVisible)
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => new ResidentConversationMessageResponse(
                message.Id,
                message.ConversationId,
                message.SenderUserId,
                message.MessageType,
                message.Body,
                message.CreatedAtUtc))
            .ToArray();

        return new ResidentConversationResponse(
            conversation.Id,
            conversation.CompoundId,
            conversation.ResidentProfileId,
            conversation.PropertyUnitId,
            conversation.Status,
            conversation.Priority,
            conversation.Topic,
            conversation.IssueType,
            conversation.LinkedEntityType,
            conversation.LinkedEntityId,
            conversation.ReopenCount,
            conversation.LastReopenReason,
            conversation.CreatedAtUtc,
            conversation.LastMessageAtUtc,
            messages);
    }

    private static ResidentConversationResponse ToResidentResponse(ConversationResponse response)
    {
        var messages = response.Messages
            .Where(message => message.Visibility == ConversationMessageVisibility.ResidentVisible)
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => new ResidentConversationMessageResponse(
                message.Id,
                message.ConversationId,
                message.SenderUserId,
                message.MessageType,
                message.Body,
                message.CreatedAtUtc))
            .ToArray();

        return new ResidentConversationResponse(
            response.Id,
            response.CompoundId,
            response.ResidentProfileId,
            response.PropertyUnitId,
            response.Status,
            response.Priority,
            response.Topic,
            response.IssueType,
            response.LinkedEntityType,
            response.LinkedEntityId,
            response.ReopenCount,
            response.LastReopenReason,
            response.CreatedAtUtc,
            response.LastMessageAtUtc,
            messages);
    }


    private static ConversationSummaryResponse ToSummaryResponse(Conversation conversation)
    {
        return new ConversationSummaryResponse(
            conversation.Id,
            conversation.CompoundId,
            conversation.ResidentProfileId,
            conversation.PropertyUnitId,
            conversation.Status,
            conversation.Priority,
            conversation.Topic,
            conversation.IssueType,
            conversation.EscalationLevel,
            conversation.AssignedToUserId,
            conversation.ReopenCount,
            conversation.CreatedAtUtc,
            conversation.LastMessageAtUtc);
    }

    private static HighRiskResidentOpenConversationResponse BuildHighRiskResident(
        Guid residentProfileId,
        string residentName,
        IEnumerable<Conversation> conversations)
    {
        var conversationList = conversations.ToArray();
        var urgentCount = conversationList.Count(conversation => conversation.Priority == ConversationPriority.Urgent);
        var escalatedCount = conversationList.Count(conversation => conversation.EscalationLevel != ConversationEscalationLevel.None);
        var reopenedCount = conversationList.Count(conversation => conversation.ReopenCount > 0 || conversation.Status == ConversationStatus.Reopened);
        var billingDisputeCount = conversationList.Count(conversation =>
            conversation.Topic == ConversationTopic.Billing
            && conversation.LinkedEntityType == ConversationLinkedEntityType.UtilityBill);

        var riskReasons = new List<string>();
        if (urgentCount > 0)
        {
            riskReasons.Add($"{urgentCount} urgent open conversation(s).");
        }

        if (escalatedCount > 0)
        {
            riskReasons.Add($"{escalatedCount} escalated conversation(s).");
        }

        if (reopenedCount > 0)
        {
            riskReasons.Add($"{reopenedCount} reopened conversation(s).");
        }

        if (billingDisputeCount > 0)
        {
            riskReasons.Add($"{billingDisputeCount} open billing dispute(s).");
        }

        if (conversationList.Length >= 3)
        {
            riskReasons.Add($"{conversationList.Length} open support conversation(s).");
        }

        return new HighRiskResidentOpenConversationResponse(
            residentProfileId,
            residentName,
            conversationList.Length,
            urgentCount,
            escalatedCount,
            reopenedCount,
            billingDisputeCount,
            riskReasons);
    }

    private static ConversationResponse ToResponse(Conversation conversation, bool includeInternalMessages)
    {
        var messages = conversation.Messages
            .Where(message => includeInternalMessages || message.Visibility == ConversationMessageVisibility.ResidentVisible)
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => new ConversationMessageResponse(
                message.Id,
                message.ConversationId,
                message.SenderUserId,
                message.MessageType,
                message.Visibility,
                message.Body,
                message.CreatedAtUtc))
            .ToArray();

        return new ConversationResponse(
            conversation.Id,
            conversation.CompoundId,
            conversation.ResidentProfileId,
            conversation.PropertyUnitId,
            conversation.Status,
            conversation.Priority,
            conversation.Topic,
            conversation.IssueType,
            conversation.LinkedEntityType,
            conversation.LinkedEntityId,
            conversation.AssignedToUserId,
            conversation.AssignedByUserId,
            conversation.AssignedAtUtc,
            conversation.LastAssignmentReason,
            conversation.EscalationLevel,
            conversation.EscalatedAtUtc,
            conversation.EscalationReason,
            conversation.ReopenCount,
            conversation.LastReopenReason,
            conversation.CreatedAtUtc,
            conversation.LastMessageAtUtc,
            messages);
    }
}
