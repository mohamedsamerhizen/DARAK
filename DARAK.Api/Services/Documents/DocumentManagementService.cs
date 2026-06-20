using DARAK.Api.Data;
using DARAK.Api.DTOs.Audit;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Documents;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class DocumentManagementService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService,
    IAuditLogService auditLogService)
    : IDocumentManagementService
{
    private const int ExpiringSoonWindowDays = 30;
    private const int MaxTitleLength = 150;
    private const int MaxDescriptionLength = 1000;

    public async Task<ServiceResult<DocumentManagementDashboardResponse>> GetDashboardAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        if (compoundId.HasValue && !await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId.Value, cancellationToken))
        {
            return ServiceResult<DocumentManagementDashboardResponse>.NotFound("Document dashboard was not found.");
        }

        var documents = await ApplyDocumentScopeAsync(
            dbContext.DocumentFiles.AsNoTracking().Where(document => !document.IsDeleted),
            cancellationToken);
        var requirements = await ApplyRequirementScopeAsync(
            dbContext.DocumentRequirements.AsNoTracking().Where(requirement => requirement.IsActive),
            cancellationToken);

        if (compoundId.HasValue)
        {
            documents = documents.Where(document => document.CompoundId == compoundId.Value);
            requirements = requirements.Where(requirement => requirement.CompoundId == compoundId.Value);
        }

        var now = DateTime.UtcNow;
        var expiringSoon = now.AddDays(ExpiringSoonWindowDays);
        var activeRequirementCount = await requirements.CountAsync(cancellationToken);
        var mandatoryRequirementCount = await requirements.CountAsync(requirement => requirement.IsMandatory, cancellationToken);
        var missingMandatoryDocumentCount = await CountMissingMandatoryDocumentsAsync(requirements, documents, cancellationToken);

        var response = new DocumentManagementDashboardResponse(
            compoundId,
            await documents.CountAsync(cancellationToken),
            await documents.CountAsync(document => document.ApprovalStatus == DocumentApprovalStatus.PendingReview, cancellationToken),
            await documents.CountAsync(document => document.ApprovalStatus == DocumentApprovalStatus.Approved, cancellationToken),
            await documents.CountAsync(document => document.ApprovalStatus == DocumentApprovalStatus.Rejected, cancellationToken),
            await documents.CountAsync(document => document.ExpiresAtUtc.HasValue && document.ExpiresAtUtc < now, cancellationToken),
            await documents.CountAsync(document => document.ExpiresAtUtc.HasValue && document.ExpiresAtUtc >= now && document.ExpiresAtUtc <= expiringSoon, cancellationToken),
            activeRequirementCount,
            mandatoryRequirementCount,
            missingMandatoryDocumentCount);

        return ServiceResult<DocumentManagementDashboardResponse>.Success(response);
    }


    public async Task<ServiceResult<DocumentComplianceReportResponse>> GetComplianceReportAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        if (compoundId.HasValue && !await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId.Value, cancellationToken))
        {
            return ServiceResult<DocumentComplianceReportResponse>.NotFound("Document compliance report was not found.");
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return ServiceResult<DocumentComplianceReportResponse>.Forbidden("Current user cannot access document compliance reports.");
        }

        var requirementsQuery = dbContext.DocumentRequirements
            .AsNoTracking()
            .Where(requirement => requirement.IsActive && requirement.IsMandatory);

        var residentsQuery = dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(resident => resident.IsActive);

        if (compoundId.HasValue)
        {
            requirementsQuery = requirementsQuery.Where(requirement => requirement.CompoundId == compoundId.Value);
            residentsQuery = residentsQuery.Where(resident => resident.CompoundId == compoundId.Value);
        }
        else if (!scope.IsSuperAdmin)
        {
            requirementsQuery = scope.AllowedCompoundIds.Length == 0
                ? requirementsQuery.Where(_ => false)
                : requirementsQuery.Where(requirement => scope.AllowedCompoundIds.Contains(requirement.CompoundId));
            residentsQuery = scope.AllowedCompoundIds.Length == 0
                ? residentsQuery.Where(_ => false)
                : residentsQuery.Where(resident => scope.AllowedCompoundIds.Contains(resident.CompoundId));
        }

        var requirements = await requirementsQuery
            .OrderBy(requirement => requirement.CompoundId)
            .ThenBy(requirement => requirement.Title)
            .Select(requirement => new ComplianceRequirement(
                requirement.Id,
                requirement.CompoundId,
                requirement.Category,
                requirement.AppliesTo,
                requirement.Title,
                requirement.RequiresApproval))
            .ToArrayAsync(cancellationToken);

        var residents = await residentsQuery
            .OrderBy(resident => resident.FullName)
            .Select(resident => new ComplianceResident(
                resident.Id,
                resident.CompoundId,
                resident.UserId,
                resident.FullName))
            .ToArrayAsync(cancellationToken);

        var residentUserIds = residents
            .Select(resident => resident.UserId)
            .Distinct()
            .ToArray();

        var documentsQuery = dbContext.DocumentFiles
            .AsNoTracking()
            .Where(document =>
                !document.IsDeleted
                && document.OwnerUserId.HasValue
                && residentUserIds.Contains(document.OwnerUserId.Value));

        if (compoundId.HasValue)
        {
            documentsQuery = documentsQuery.Where(document => document.CompoundId == compoundId.Value);
        }
        else if (!scope.IsSuperAdmin)
        {
            documentsQuery = scope.AllowedCompoundIds.Length == 0
                ? documentsQuery.Where(_ => false)
                : documentsQuery.Where(document => scope.AllowedCompoundIds.Contains(document.CompoundId));
        }

        var documents = await documentsQuery
            .Select(document => new ComplianceDocument(
                document.Id,
                document.CompoundId,
                document.OwnerUserId!.Value,
                document.Category,
                document.ApprovalStatus,
                document.ExpiresAtUtc,
                document.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var expiringSoon = now.AddDays(ExpiringSoonWindowDays);
        var residentResponses = new List<DocumentComplianceResidentResponse>();

        foreach (var resident in residents)
        {
            var residentRequirements = requirements
                .Where(requirement => requirement.CompoundId == resident.CompoundId)
                .ToArray();
            var residentDocuments = documents
                .Where(document => document.CompoundId == resident.CompoundId && document.OwnerUserId == resident.UserId)
                .ToArray();
            var gaps = new List<DocumentComplianceGapResponse>();

            foreach (var requirement in residentRequirements)
            {
                var document = residentDocuments
                    .Where(item => item.Category == requirement.Category)
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .FirstOrDefault();
                var gapReason = GetComplianceGapReason(requirement, document, now);
                if (gapReason is null)
                {
                    continue;
                }

                gaps.Add(new DocumentComplianceGapResponse(
                    requirement.Id,
                    requirement.Title,
                    requirement.Category,
                    requirement.AppliesTo,
                    gapReason,
                    document?.Id,
                    document?.ApprovalStatus,
                    document?.ExpiresAtUtc));
            }

            var expiredDocumentCount = residentDocuments.Count(document => document.ExpiresAtUtc.HasValue && document.ExpiresAtUtc < now);
            var expiringSoonDocumentCount = residentDocuments.Count(document =>
                document.ExpiresAtUtc.HasValue
                && document.ExpiresAtUtc >= now
                && document.ExpiresAtUtc <= expiringSoon);

            residentResponses.Add(new DocumentComplianceResidentResponse(
                resident.Id,
                resident.CompoundId,
                resident.FullName,
                residentRequirements.Length,
                residentRequirements.Length - gaps.Count,
                gaps.Count,
                expiredDocumentCount,
                expiringSoonDocumentCount,
                gaps.Count == 0,
                gaps));
        }

        var response = new DocumentComplianceReportResponse(
            compoundId,
            residentResponses.Count,
            residentResponses.Count(item => item.IsCompliant),
            residentResponses.Count(item => !item.IsCompliant),
            requirements.Length,
            residentResponses.Sum(item => item.MissingMandatoryDocumentCount),
            residentResponses.Sum(item => item.ExpiredDocumentCount),
            residentResponses.Sum(item => item.ExpiringSoonDocumentCount),
            residentResponses);

        return ServiceResult<DocumentComplianceReportResponse>.Success(response);
    }

    public async Task<ServiceResult<PagedResult<DocumentRequirementResponse>>> SearchRequirementsAsync(
        DocumentRequirementSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var requirements = ApplyRequirementFilters(
            await ApplyRequirementScopeAsync(dbContext.DocumentRequirements.AsNoTracking(), cancellationToken),
            query);

        var totalCount = await requirements.CountAsync(cancellationToken);
        var items = await requirements
            .OrderBy(requirement => requirement.Title)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(requirement => ToRequirementResponse(requirement))
            .ToArrayAsync(cancellationToken);

        return ServiceResult<PagedResult<DocumentRequirementResponse>>.Success(
            new PagedResult<DocumentRequirementResponse>(items, query.PageNumber, query.PageSize, totalCount));
    }

    public async Task<ServiceResult<DocumentRequirementResponse>> GetRequirementAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var requirement = await (await ApplyRequirementScopeAsync(dbContext.DocumentRequirements.AsNoTracking(), cancellationToken))
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (requirement is null)
        {
            return ServiceResult<DocumentRequirementResponse>.NotFound("Document requirement was not found.");
        }

        return ServiceResult<DocumentRequirementResponse>.Success(ToRequirementResponse(requirement));
    }

    public async Task<ServiceResult<DocumentRequirementResponse>> CreateRequirementAsync(
        Guid? currentUserId,
        CreateDocumentRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<DocumentRequirementResponse>.Forbidden("Authentication is required.");
        }

        var validation = await ValidateRequirementRequestAsync(request.CompoundId, request.Category, request.AppliesTo, request.Title, request.Description, request.ValidityDays, cancellationToken);
        if (validation is not null)
        {
            return ToResult<DocumentRequirementResponse>(validation);
        }

        if (!await compoundAccessService.CanCurrentUserAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<DocumentRequirementResponse>.NotFound("Compound was not found.");
        }

        var requirement = new DocumentRequirement
        {
            CompoundId = request.CompoundId,
            Category = request.Category,
            AppliesTo = request.AppliesTo,
            Title = request.Title.Trim(),
            Description = TrimOrNull(request.Description),
            IsMandatory = request.IsMandatory,
            ValidityDays = request.ValidityDays,
            RequiresApproval = request.RequiresApproval,
            CreatedByUserId = currentUserId.Value,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.DocumentRequirements.Add(requirement);
        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            CompoundId: requirement.CompoundId,
            ResidentProfileId: null,
            ActorUserId: currentUserId.Value,
            ActorRole: null,
            ActionType: AuditActionType.DocumentRequirementCreated,
            EntityType: AuditEntityType.DocumentRequirement,
            EntityId: requirement.Id,
            Severity: AuditSeverity.Low,
            SourceModule: "Documents",
            Description: "Document requirement created."), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<DocumentRequirementResponse>.Success(ToRequirementResponse(requirement));
    }

    public async Task<ServiceResult<DocumentRequirementResponse>> UpdateRequirementAsync(
        Guid id,
        UpdateDocumentRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        var requirement = await dbContext.DocumentRequirements
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (requirement is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(requirement.CompoundId, cancellationToken))
        {
            return ServiceResult<DocumentRequirementResponse>.NotFound("Document requirement was not found.");
        }

        var validation = await ValidateRequirementRequestAsync(requirement.CompoundId, request.Category, request.AppliesTo, request.Title, request.Description, request.ValidityDays, cancellationToken);
        if (validation is not null)
        {
            return ToResult<DocumentRequirementResponse>(validation);
        }

        requirement.Category = request.Category;
        requirement.AppliesTo = request.AppliesTo;
        requirement.Title = request.Title.Trim();
        requirement.Description = TrimOrNull(request.Description);
        requirement.IsMandatory = request.IsMandatory;
        requirement.ValidityDays = request.ValidityDays;
        requirement.RequiresApproval = request.RequiresApproval;
        requirement.IsActive = request.IsActive;
        requirement.UpdatedAtUtc = DateTime.UtcNow;
        requirement.DeactivatedAtUtc = request.IsActive ? null : DateTime.UtcNow;

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            CompoundId: requirement.CompoundId,
            ResidentProfileId: null,
            ActorUserId: null,
            ActorRole: null,
            ActionType: AuditActionType.DocumentRequirementUpdated,
            EntityType: AuditEntityType.DocumentRequirement,
            EntityId: requirement.Id,
            Severity: AuditSeverity.Low,
            SourceModule: "Documents",
            Description: "Document requirement updated."), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<DocumentRequirementResponse>.Success(ToRequirementResponse(requirement));
    }

    public async Task<ServiceResult<DocumentRequirementResponse>> DeactivateRequirementAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var requirement = await dbContext.DocumentRequirements
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (requirement is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(requirement.CompoundId, cancellationToken))
        {
            return ServiceResult<DocumentRequirementResponse>.NotFound("Document requirement was not found.");
        }

        requirement.IsActive = false;
        requirement.UpdatedAtUtc = DateTime.UtcNow;
        requirement.DeactivatedAtUtc = DateTime.UtcNow;

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            CompoundId: requirement.CompoundId,
            ResidentProfileId: null,
            ActorUserId: null,
            ActorRole: null,
            ActionType: AuditActionType.DocumentRequirementDeactivated,
            EntityType: AuditEntityType.DocumentRequirement,
            EntityId: requirement.Id,
            Severity: AuditSeverity.Low,
            SourceModule: "Documents",
            Description: "Document requirement deactivated."), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<DocumentRequirementResponse>.Success(ToRequirementResponse(requirement));
    }

    public async Task<ServiceResult<DocumentFileResponse>> ApproveDocumentAsync(
        Guid? currentUserId,
        Guid documentId,
        ReviewDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ReviewDocumentAsync(currentUserId, documentId, DocumentApprovalStatus.Approved, request, cancellationToken);
    }

    public async Task<ServiceResult<DocumentFileResponse>> RejectDocumentAsync(
        Guid? currentUserId,
        Guid documentId,
        ReviewDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<DocumentFileResponse>.BadRequest("Rejection reason is required.");
        }

        return await ReviewDocumentAsync(currentUserId, documentId, DocumentApprovalStatus.Rejected, request, cancellationToken);
    }

    public async Task<ServiceResult<ResidentDocumentChecklistResponse>> GetResidentChecklistAsync(
        Guid residentProfileId,
        CancellationToken cancellationToken = default)
    {
        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == residentProfileId && item.IsActive, cancellationToken);
        if (resident is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(resident.CompoundId, cancellationToken))
        {
            return ServiceResult<ResidentDocumentChecklistResponse>.NotFound("Resident document checklist was not found.");
        }

        var requirements = await dbContext.DocumentRequirements
            .AsNoTracking()
            .Where(requirement => requirement.CompoundId == resident.CompoundId && requirement.IsActive)
            .OrderByDescending(requirement => requirement.IsMandatory)
            .ThenBy(requirement => requirement.Title)
            .ToListAsync(cancellationToken);

        var documents = await dbContext.DocumentFiles
            .AsNoTracking()
            .Where(document =>
                !document.IsDeleted
                && document.CompoundId == resident.CompoundId
                && document.OwnerUserId == resident.UserId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var items = requirements.Select(requirement =>
        {
            var document = documents
                .Where(item => item.Category == requirement.Category)
                .OrderByDescending(item => item.CreatedAtUtc)
                .FirstOrDefault();
            var isSatisfied = document is not null
                && document.ApprovalStatus != DocumentApprovalStatus.Rejected
                && (!requirement.RequiresApproval || document.ApprovalStatus == DocumentApprovalStatus.Approved)
                && (!document.ExpiresAtUtc.HasValue || document.ExpiresAtUtc.Value >= now);

            return new ResidentDocumentChecklistItemResponse(
                requirement.Id,
                requirement.Title,
                requirement.Category,
                requirement.AppliesTo,
                requirement.IsMandatory,
                requirement.ValidityDays,
                requirement.RequiresApproval,
                isSatisfied,
                document?.Id,
                document?.ApprovalStatus,
                document?.ExpiresAtUtc);
        }).ToArray();

        return ServiceResult<ResidentDocumentChecklistResponse>.Success(
            new ResidentDocumentChecklistResponse(resident.Id, resident.CompoundId, resident.FullName, items));
    }

    private async Task<ServiceResult<DocumentFileResponse>> ReviewDocumentAsync(
        Guid? currentUserId,
        Guid documentId,
        DocumentApprovalStatus status,
        ReviewDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!currentUserId.HasValue)
        {
            return ServiceResult<DocumentFileResponse>.Forbidden("Authentication is required.");
        }

        var document = await dbContext.DocumentFiles
            .FirstOrDefaultAsync(item => item.Id == documentId && !item.IsDeleted, cancellationToken);
        if (document is null || !await compoundAccessService.CanCurrentUserAccessCompoundAsync(document.CompoundId, cancellationToken))
        {
            return ServiceResult<DocumentFileResponse>.NotFound("Document was not found.");
        }

        if (document.ApprovalStatus == status)
        {
            return ServiceResult<DocumentFileResponse>.Conflict("Document already has the requested review status.");
        }

        document.ApprovalStatus = status;
        document.ReviewedByUserId = currentUserId.Value;
        document.ReviewedAtUtc = DateTime.UtcNow;
        document.ReviewReason = TrimOrNull(request.Reason);
        document.UpdatedAtUtc = DateTime.UtcNow;

        await auditLogService.AppendEntryAsync(new AuditLogRecord(
            CompoundId: document.CompoundId,
            ResidentProfileId: null,
            ActorUserId: currentUserId.Value,
            ActorRole: null,
            ActionType: status == DocumentApprovalStatus.Approved ? AuditActionType.DocumentApproved : AuditActionType.DocumentRejected,
            EntityType: AuditEntityType.Document,
            EntityId: document.Id,
            Severity: status == DocumentApprovalStatus.Approved ? AuditSeverity.Low : AuditSeverity.Medium,
            SourceModule: "Documents",
            Description: status == DocumentApprovalStatus.Approved ? "Document approved." : "Document rejected.",
            Reason: TrimOrNull(request.Reason)), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<DocumentFileResponse>.Success(ToDocumentFileResponse(document));
    }

    private async Task<ValidationFailure?> ValidateRequirementRequestAsync(
        Guid compoundId,
        DocumentCategory category,
        DocumentRequirementAppliesTo appliesTo,
        string title,
        string? description,
        int? validityDays,
        CancellationToken cancellationToken)
    {
        if (compoundId == Guid.Empty)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Compound id is required.");
        }

        if (!Enum.IsDefined(category))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Document category is invalid.");
        }

        if (!Enum.IsDefined(appliesTo))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Document requirement applies-to value is invalid.");
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > MaxTitleLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Document requirement title is required and cannot exceed 150 characters.");
        }

        if (TrimOrNull(description)?.Length > MaxDescriptionLength)
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Document requirement description is too long.");
        }

        if (validityDays.HasValue && (validityDays <= 0 || validityDays > 3650))
        {
            return new ValidationFailure(ServiceResultStatus.BadRequest, "Document requirement validity days must be between 1 and 3650.");
        }

        if (!await dbContext.Compounds.AsNoTracking().AnyAsync(compound => compound.Id == compoundId && compound.IsActive, cancellationToken))
        {
            return new ValidationFailure(ServiceResultStatus.NotFound, "Compound was not found.");
        }

        return null;
    }

    private async Task<int> CountMissingMandatoryDocumentsAsync(
        IQueryable<DocumentRequirement> requirements,
        IQueryable<DocumentFile> documents,
        CancellationToken cancellationToken)
    {
        var mandatoryRequirements = await requirements
            .Where(requirement => requirement.IsMandatory)
            .Select(requirement => new { requirement.CompoundId, requirement.Category, requirement.RequiresApproval })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var missing = 0;
        foreach (var requirement in mandatoryRequirements)
        {
            var hasDocument = await documents.AnyAsync(document =>
                document.CompoundId == requirement.CompoundId
                && document.Category == requirement.Category
                && document.ApprovalStatus != DocumentApprovalStatus.Rejected
                && (!requirement.RequiresApproval || document.ApprovalStatus == DocumentApprovalStatus.Approved)
                && (!document.ExpiresAtUtc.HasValue || document.ExpiresAtUtc >= now), cancellationToken);
            if (!hasDocument)
            {
                missing++;
            }
        }

        return missing;
    }

    private async Task<IQueryable<DocumentRequirement>> ApplyRequirementScopeAsync(
        IQueryable<DocumentRequirement> requirements,
        CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return requirements.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return requirements;
        }

        return scope.AllowedCompoundIds.Length == 0
            ? requirements.Where(_ => false)
            : requirements.Where(requirement => scope.AllowedCompoundIds.Contains(requirement.CompoundId));
    }

    private async Task<IQueryable<DocumentFile>> ApplyDocumentScopeAsync(
        IQueryable<DocumentFile> documents,
        CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return documents.Where(_ => false);
        }

        if (scope.IsSuperAdmin)
        {
            return documents;
        }

        return scope.AllowedCompoundIds.Length == 0
            ? documents.Where(_ => false)
            : documents.Where(document => scope.AllowedCompoundIds.Contains(document.CompoundId));
    }

    private static IQueryable<DocumentRequirement> ApplyRequirementFilters(
        IQueryable<DocumentRequirement> requirements,
        DocumentRequirementSearchQuery query)
    {
        if (query.CompoundId.HasValue)
        {
            requirements = requirements.Where(requirement => requirement.CompoundId == query.CompoundId.Value);
        }

        if (query.Category.HasValue)
        {
            requirements = requirements.Where(requirement => requirement.Category == query.Category.Value);
        }

        if (query.AppliesTo.HasValue)
        {
            requirements = requirements.Where(requirement => requirement.AppliesTo == query.AppliesTo.Value);
        }

        if (query.IsMandatory.HasValue)
        {
            requirements = requirements.Where(requirement => requirement.IsMandatory == query.IsMandatory.Value);
        }

        if (query.IsActive.HasValue)
        {
            requirements = requirements.Where(requirement => requirement.IsActive == query.IsActive.Value);
        }

        var searchTerm = TrimOrNull(query.SearchTerm);
        if (searchTerm is not null)
        {
            requirements = requirements.Where(requirement => requirement.Title.Contains(searchTerm)
                || (requirement.Description != null && requirement.Description.Contains(searchTerm)));
        }

        return requirements;
    }

    private static DocumentRequirementResponse ToRequirementResponse(DocumentRequirement requirement)
    {
        return new DocumentRequirementResponse(
            requirement.Id,
            requirement.CompoundId,
            requirement.Category,
            requirement.AppliesTo,
            requirement.Title,
            requirement.Description,
            requirement.IsMandatory,
            requirement.ValidityDays,
            requirement.RequiresApproval,
            requirement.IsActive,
            requirement.CreatedByUserId,
            requirement.CreatedAtUtc,
            requirement.UpdatedAtUtc,
            requirement.DeactivatedAtUtc);
    }

    private static DocumentFileResponse ToDocumentFileResponse(DocumentFile document)
    {
        return new DocumentFileResponse(
            document.Id,
            document.OriginalFileName,
            document.ContentType,
            document.Extension,
            document.SizeInBytes,
            document.Category,
            document.Visibility,
            document.RelatedEntityType,
            document.RelatedEntityId,
            document.UploadedByUserId,
            document.OwnerUserId,
            document.CompoundId,
            document.PropertyUnitId,
            document.Description,
            document.ApprovalStatus,
            document.ReviewedByUserId,
            document.ReviewedAtUtc,
            document.ReviewReason,
            document.ExpiresAtUtc,
            document.VersionNumber,
            document.RootDocumentFileId,
            document.PreviousVersionDocumentFileId,
            document.CreatedAtUtc,
            document.UpdatedAtUtc);
    }

    private static ServiceResult<T> ToResult<T>(ValidationFailure validation)
    {
        return validation.Status switch
        {
            ServiceResultStatus.NotFound => ServiceResult<T>.NotFound(validation.Message),
            ServiceResultStatus.Forbidden => ServiceResult<T>.Forbidden(validation.Message),
            ServiceResultStatus.Conflict => ServiceResult<T>.Conflict(validation.Message),
            _ => ServiceResult<T>.BadRequest(validation.Message)
        };
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }


    private static string? GetComplianceGapReason(
        ComplianceRequirement requirement,
        ComplianceDocument? document,
        DateTime now)
    {
        if (document is null)
        {
            return "Missing";
        }

        if (document.ExpiresAtUtc.HasValue && document.ExpiresAtUtc < now)
        {
            return "Expired";
        }

        if (document.ApprovalStatus == DocumentApprovalStatus.Rejected)
        {
            return "Rejected";
        }

        if (requirement.RequiresApproval && document.ApprovalStatus == DocumentApprovalStatus.PendingReview)
        {
            return "PendingReview";
        }

        if (requirement.RequiresApproval && document.ApprovalStatus != DocumentApprovalStatus.Approved)
        {
            return "ApprovalRequired";
        }

        return null;
    }

    private sealed record ComplianceRequirement(
        Guid Id,
        Guid CompoundId,
        DocumentCategory Category,
        DocumentRequirementAppliesTo AppliesTo,
        string Title,
        bool RequiresApproval);

    private sealed record ComplianceResident(
        Guid Id,
        Guid CompoundId,
        Guid UserId,
        string FullName);

    private sealed record ComplianceDocument(
        Guid Id,
        Guid CompoundId,
        Guid OwnerUserId,
        DocumentCategory Category,
        DocumentApprovalStatus ApprovalStatus,
        DateTime? ExpiresAtUtc,
        DateTime CreatedAtUtc);

    private sealed record ValidationFailure(ServiceResultStatus Status, string Message);
}
