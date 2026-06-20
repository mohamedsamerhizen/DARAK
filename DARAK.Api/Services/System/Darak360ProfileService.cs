using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.System;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class Darak360ProfileService(
    ApplicationDbContext dbContext,
    ICompoundAccessService compoundAccessService)
    : IDarak360ProfileService
{
    public async Task<ServiceResult<Resident360ProfileResponse>> GetResident360ProfileAsync(
        Guid residentId,
        CancellationToken cancellationToken = default)
    {
        if (residentId == Guid.Empty)
        {
            return ServiceResult<Resident360ProfileResponse>.BadRequest("Resident id is required.");
        }

        var resident = await dbContext.ResidentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == residentId, cancellationToken);

        if (resident is null)
        {
            return ServiceResult<Resident360ProfileResponse>.NotFound("Resident profile was not found.");
        }

        var access = await ValidateCompoundAccessAsync(resident.CompoundId, cancellationToken);
        if (access is not null)
        {
            return ServiceResult<Resident360ProfileResponse>.Forbidden(access);
        }

        var occupancies = await dbContext.OccupancyRecords
            .AsNoTracking()
            .Include(item => item.PropertyUnit)
            .Where(item => item.ResidentProfileId == resident.Id)
            .OrderByDescending(item => item.OccupancyStatus == OccupancyStatus.Active)
            .ThenByDescending(item => item.StartDate)
            .ToArrayAsync(cancellationToken);

        var currentOccupancy = occupancies.FirstOrDefault(item => item.OccupancyStatus == OccupancyStatus.Active);
        var currentUnit = currentOccupancy is null
            ? null
            : new Darak360CurrentUnitResponse(
                currentOccupancy.PropertyUnitId,
                currentOccupancy.PropertyUnit.UnitNumber,
                currentOccupancy.PropertyUnit.UnitStatus.ToString(),
                currentOccupancy.PropertyUnit.PropertyType.ToString(),
                currentOccupancy.OccupancyType.ToString(),
                currentOccupancy.StartDate,
                currentOccupancy.ContractNumber);

        var unitId = currentOccupancy?.PropertyUnitId;
        var financial = await BuildResidentFinancialSnapshotAsync(resident.Id, cancellationToken);
        var operations = await BuildResidentOperationsSnapshotAsync(resident.Id, unitId, cancellationToken);
        var legal = await BuildResidentLegalRiskSnapshotAsync(resident.Id, cancellationToken);
        var communication = await BuildResidentCommunicationSnapshotAsync(resident, cancellationToken);
        var signals = BuildResidentSignals(financial, operations, legal, communication, currentUnit);
        var actions = BuildResidentActions(signals);

        var response = new Resident360ProfileResponse(
            resident.Id,
            resident.CompoundId,
            resident.FullName,
            resident.IsActive,
            currentUnit,
            financial,
            operations,
            legal,
            communication,
            signals,
            actions,
            DateTime.UtcNow);

        return ServiceResult<Resident360ProfileResponse>.Success(response);
    }

    public async Task<ServiceResult<Unit360ProfileResponse>> GetUnit360ProfileAsync(
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        if (unitId == Guid.Empty)
        {
            return ServiceResult<Unit360ProfileResponse>.BadRequest("Unit id is required.");
        }

        var unit = await dbContext.PropertyUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == unitId, cancellationToken);

        if (unit is null)
        {
            return ServiceResult<Unit360ProfileResponse>.NotFound("Property unit was not found.");
        }

        var access = await ValidateCompoundAccessAsync(unit.CompoundId, cancellationToken);
        if (access is not null)
        {
            return ServiceResult<Unit360ProfileResponse>.Forbidden(access);
        }

        var occupancies = await dbContext.OccupancyRecords
            .AsNoTracking()
            .Include(item => item.ResidentProfile)
            .Where(item => item.PropertyUnitId == unit.Id)
            .OrderByDescending(item => item.OccupancyStatus == OccupancyStatus.Active)
            .ThenByDescending(item => item.StartDate)
            .ToArrayAsync(cancellationToken);

        var currentOccupancy = occupancies.FirstOrDefault(item => item.OccupancyStatus == OccupancyStatus.Active);
        var currentResident = currentOccupancy is null
            ? null
            : new Darak360CurrentResidentResponse(
                currentOccupancy.ResidentProfileId,
                currentOccupancy.ResidentProfile.FullName,
                currentOccupancy.ResidentProfile.PhoneNumber,
                currentOccupancy.OccupancyType.ToString(),
                currentOccupancy.StartDate,
                currentOccupancy.ContractNumber);

        var financial = await BuildUnitFinancialSnapshotAsync(unit.Id, cancellationToken);
        var operations = await BuildUnitOperationsSnapshotAsync(unit.Id, cancellationToken);
        var lifecycle = await BuildUnitLifecycleSnapshotAsync(unit.Id, occupancies.Length, cancellationToken);
        var signals = BuildUnitSignals(financial, operations, lifecycle, currentResident, unit.UnitStatus.ToString());
        var actions = BuildUnitActions(signals);

        var response = new Unit360ProfileResponse(
            unit.Id,
            unit.CompoundId,
            unit.UnitNumber,
            unit.UnitStatus.ToString(),
            unit.PropertyType.ToString(),
            currentResident,
            financial,
            operations,
            lifecycle,
            signals,
            actions,
            DateTime.UtcNow);

        return ServiceResult<Unit360ProfileResponse>.Success(response);
    }

    public async Task<ServiceResult<Compound360OverviewResponse>> GetCompound360OverviewAsync(
        Guid compoundId,
        CancellationToken cancellationToken = default)
    {
        if (compoundId == Guid.Empty)
        {
            return ServiceResult<Compound360OverviewResponse>.BadRequest("Compound id is required.");
        }

        var compound = await dbContext.Compounds
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == compoundId, cancellationToken);

        if (compound is null)
        {
            return ServiceResult<Compound360OverviewResponse>.NotFound("Compound was not found.");
        }

        var access = await ValidateCompoundAccessAsync(compound.Id, cancellationToken);
        if (access is not null)
        {
            return ServiceResult<Compound360OverviewResponse>.Forbidden(access);
        }

        var inventory = await BuildCompoundInventorySnapshotAsync(compound.Id, cancellationToken);
        var financial = await BuildCompoundFinancialSnapshotAsync(compound.Id, cancellationToken);
        var operations = await BuildCompoundOperationsSnapshotAsync(compound.Id, cancellationToken);
        var legal = await BuildCompoundLegalRiskSnapshotAsync(compound.Id, cancellationToken);
        var communication = await BuildCompoundCommunicationSnapshotAsync(compound.Id, cancellationToken);
        var signals = BuildCompoundSignals(inventory, financial, operations, legal, communication);
        var actions = BuildCompoundActions(signals);

        var response = new Compound360OverviewResponse(
            compound.Id,
            compound.Name,
            compound.Code,
            inventory,
            financial,
            operations,
            legal,
            communication,
            signals,
            actions,
            DateTime.UtcNow);

        return ServiceResult<Compound360OverviewResponse>.Success(response);
    }

    private async Task<string?> ValidateCompoundAccessAsync(Guid compoundId, CancellationToken cancellationToken)
    {
        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsAuthenticated)
        {
            return "Authenticated compound access is required.";
        }

        if (!scope.CanAccess(compoundId))
        {
            return "You do not have access to this compound.";
        }

        return null;
    }

    private async Task<Darak360InventorySnapshotResponse> BuildCompoundInventorySnapshotAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        var units = dbContext.PropertyUnits.AsNoTracking().Where(item => item.CompoundId == compoundId);

        return new Darak360InventorySnapshotResponse(
            await dbContext.Buildings.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            await dbContext.Floors.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            await units.CountAsync(cancellationToken),
            await units.CountAsync(item => item.UnitStatus == UnitStatus.Available, cancellationToken),
            await units.CountAsync(item => item.UnitStatus == UnitStatus.Occupied || item.UnitStatus == UnitStatus.Rented || item.UnitStatus == UnitStatus.SoldCash || item.UnitStatus == UnitStatus.SoldInstallment, cancellationToken),
            await dbContext.ResidentProfiles.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            await dbContext.OccupancyRecords.AsNoTracking().CountAsync(item => item.CompoundId == compoundId && item.OccupancyStatus == OccupancyStatus.Active, cancellationToken));
    }

    private async Task<Darak360FinancialSnapshotResponse> BuildResidentFinancialSnapshotAsync(
        Guid residentId,
        CancellationToken cancellationToken)
    {
        var bills = await dbContext.UtilityBills.AsNoTracking()
            .Where(item => item.ResidentProfileId == residentId)
            .Select(item => new FinancialAmount(item.TotalAmount, item.PaidAmount))
            .ToArrayAsync(cancellationToken);

        var rentInvoices = await dbContext.RentInvoices.AsNoTracking()
            .Where(item => item.ResidentProfileId == residentId)
            .Select(item => new FinancialAmount(item.TotalAmount, item.PaidAmount))
            .ToArrayAsync(cancellationToken);

        var payments = await dbContext.Payments.AsNoTracking()
            .Where(item => item.ResidentProfileId == residentId)
            .Select(item => item.Amount)
            .ToArrayAsync(cancellationToken);

        return BuildFinancialSnapshot(
            bills,
            rentInvoices,
            payments,
            await dbContext.FinancialDisputes.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId && item.ResolvedAtUtc == null && item.CancelledAtUtc == null, cancellationToken),
            await dbContext.CollectionCases.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId && item.ClosedAtUtc == null, cancellationToken));
    }

    private async Task<Darak360FinancialSnapshotResponse> BuildUnitFinancialSnapshotAsync(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        var bills = await dbContext.UtilityBills.AsNoTracking()
            .Where(item => item.PropertyUnitId == unitId)
            .Select(item => new FinancialAmount(item.TotalAmount, item.PaidAmount))
            .ToArrayAsync(cancellationToken);

        var rentInvoices = await dbContext.RentInvoices.AsNoTracking()
            .Where(item => item.PropertyUnitId == unitId)
            .Select(item => new FinancialAmount(item.TotalAmount, item.PaidAmount))
            .ToArrayAsync(cancellationToken);

        var payments = await dbContext.Payments.AsNoTracking()
            .Where(item => item.TargetId == unitId)
            .Select(item => item.Amount)
            .ToArrayAsync(cancellationToken);

        return BuildFinancialSnapshot(bills, rentInvoices, payments, 0, 0);
    }

    private async Task<Darak360FinancialSnapshotResponse> BuildCompoundFinancialSnapshotAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        var bills = await dbContext.UtilityBills.AsNoTracking()
            .Where(item => item.CompoundId == compoundId)
            .Select(item => new FinancialAmount(item.TotalAmount, item.PaidAmount))
            .ToArrayAsync(cancellationToken);

        var rentInvoices = await dbContext.RentInvoices.AsNoTracking()
            .Where(item => item.CompoundId == compoundId)
            .Select(item => new FinancialAmount(item.TotalAmount, item.PaidAmount))
            .ToArrayAsync(cancellationToken);

        var payments = await dbContext.Payments.AsNoTracking()
            .Where(item => item.CompoundId == compoundId)
            .Select(item => item.Amount)
            .ToArrayAsync(cancellationToken);

        return BuildFinancialSnapshot(
            bills,
            rentInvoices,
            payments,
            await dbContext.FinancialDisputes.AsNoTracking().CountAsync(item => item.CompoundId == compoundId && item.ResolvedAtUtc == null && item.CancelledAtUtc == null, cancellationToken),
            await dbContext.CollectionCases.AsNoTracking().CountAsync(item => item.CompoundId == compoundId && item.ClosedAtUtc == null, cancellationToken));
    }

    private static Darak360FinancialSnapshotResponse BuildFinancialSnapshot(
        FinancialAmount[] utilityBills,
        FinancialAmount[] rentInvoices,
        decimal[] payments,
        int openDisputes,
        int collectionCases)
    {
        var allInvoices = utilityBills.Concat(rentInvoices).ToArray();
        var totalBilled = allInvoices.Sum(item => item.TotalAmount);
        var totalPaidFromInvoices = allInvoices.Sum(item => item.PaidAmount);
        var totalPaid = Math.Max(totalPaidFromInvoices, payments.Sum());

        return new Darak360FinancialSnapshotResponse(
            utilityBills.Length,
            rentInvoices.Length,
            payments.Length,
            totalBilled,
            totalPaid,
            Math.Max(0, totalBilled - totalPaidFromInvoices),
            openDisputes,
            collectionCases);
    }

    private async Task<Darak360OperationsSnapshotResponse> BuildResidentOperationsSnapshotAsync(
        Guid residentId,
        Guid? unitId,
        CancellationToken cancellationToken)
    {
        return new Darak360OperationsSnapshotResponse(
            await dbContext.MaintenanceRequests.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId, cancellationToken),
            unitId.HasValue ? await dbContext.WorkOrders.AsNoTracking().CountAsync(item => item.PropertyUnitId == unitId.Value, cancellationToken) : 0,
            await dbContext.SupportCases.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId, cancellationToken),
            await dbContext.VisitorPasses.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId, cancellationToken),
            await dbContext.ResidentRiskFlags.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId && (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring), cancellationToken));
    }

    private async Task<Darak360UnitOperationsSnapshotResponse> BuildUnitOperationsSnapshotAsync(
        Guid unitId,
        CancellationToken cancellationToken)
    {
        return new Darak360UnitOperationsSnapshotResponse(
            await dbContext.Meters.AsNoTracking().CountAsync(item => item.PropertyUnitId == unitId, cancellationToken),
            await dbContext.MaintenanceRequests.AsNoTracking().CountAsync(item => item.PropertyUnitId == unitId, cancellationToken),
            await dbContext.WorkOrders.AsNoTracking().CountAsync(item => item.PropertyUnitId == unitId, cancellationToken),
            await dbContext.VisitorPasses.AsNoTracking().CountAsync(item => item.PropertyUnitId == unitId, cancellationToken),
            await dbContext.SupportCases.AsNoTracking().CountAsync(item => item.PropertyUnitId == unitId, cancellationToken),
            await dbContext.ResidentRiskFlags.AsNoTracking().CountAsync(item => item.PropertyUnitId == unitId && (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring), cancellationToken));
    }

    private async Task<Darak360OperationsSnapshotResponse> BuildCompoundOperationsSnapshotAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        return new Darak360OperationsSnapshotResponse(
            await dbContext.MaintenanceRequests.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            await dbContext.WorkOrders.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            await dbContext.SupportCases.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            await dbContext.VisitorPasses.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            await dbContext.ResidentRiskFlags.AsNoTracking().CountAsync(item => item.CompoundId == compoundId && (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring), cancellationToken));
    }

    private async Task<Darak360LifecycleSnapshotResponse> BuildUnitLifecycleSnapshotAsync(
        Guid unitId,
        int occupancyHistoryRecords,
        CancellationToken cancellationToken)
    {
        var readinessRecords = await dbContext.UnitReadinessRecords.AsNoTracking()
            .Where(item => item.PropertyUnitId == unitId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        var damages = await dbContext.UnitDamageLiabilities.AsNoTracking()
            .Where(item => item.PropertyUnitId == unitId && item.Status != DamageLiabilityStatus.Resolved && item.Status != DamageLiabilityStatus.Cancelled)
            .Select(item => item.EstimatedAmount)
            .ToArrayAsync(cancellationToken);

        return new Darak360LifecycleSnapshotResponse(
            occupancyHistoryRecords,
            await dbContext.ResidentLifecycleProcesses.AsNoTracking().CountAsync(item => item.PropertyUnitId == unitId && item.Status != ResidentLifecycleStatus.Completed && item.Status != ResidentLifecycleStatus.Cancelled, cancellationToken),
            readinessRecords.Length,
            readinessRecords.FirstOrDefault()?.Status.ToString(),
            damages.Length,
            damages.Sum());
    }

    private async Task<Darak360LegalRiskSnapshotResponse> BuildResidentLegalRiskSnapshotAsync(
        Guid residentId,
        CancellationToken cancellationToken)
    {
        var fines = await dbContext.ViolationFines.AsNoTracking()
            .Where(item => item.ResidentProfileId == residentId && item.PaidAmount < item.Amount && item.Status != ViolationFineStatus.Cancelled)
            .Select(item => item.Amount)
            .ToArrayAsync(cancellationToken);

        return new Darak360LegalRiskSnapshotResponse(
            await dbContext.Violations.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId, cancellationToken),
            await dbContext.ViolationFines.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId, cancellationToken),
            fines.Sum(),
            await dbContext.FinancialDisputes.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId && item.ResolvedAtUtc == null && item.CancelledAtUtc == null, cancellationToken),
            await dbContext.CollectionCases.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId && item.ClosedAtUtc == null, cancellationToken),
            await dbContext.LegalNotices.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId, cancellationToken),
            await dbContext.ResidentRiskFlags.AsNoTracking().CountAsync(item => item.ResidentProfileId == residentId && (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring), cancellationToken));
    }

    private async Task<Darak360LegalRiskSnapshotResponse> BuildCompoundLegalRiskSnapshotAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        var fines = await dbContext.ViolationFines.AsNoTracking()
            .Where(item => item.CompoundId == compoundId && item.PaidAmount < item.Amount && item.Status != ViolationFineStatus.Cancelled)
            .Select(item => item.Amount)
            .ToArrayAsync(cancellationToken);

        return new Darak360LegalRiskSnapshotResponse(
            await dbContext.Violations.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            await dbContext.ViolationFines.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            fines.Sum(),
            await dbContext.FinancialDisputes.AsNoTracking().CountAsync(item => item.CompoundId == compoundId && item.ResolvedAtUtc == null && item.CancelledAtUtc == null, cancellationToken),
            await dbContext.CollectionCases.AsNoTracking().CountAsync(item => item.CompoundId == compoundId && item.ClosedAtUtc == null, cancellationToken),
            await dbContext.LegalNotices.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            await dbContext.ResidentRiskFlags.AsNoTracking().CountAsync(item => item.CompoundId == compoundId && (item.Status == ResidentRiskFlagStatus.Active || item.Status == ResidentRiskFlagStatus.Monitoring), cancellationToken));
    }

    private async Task<Darak360CommunicationSnapshotResponse> BuildResidentCommunicationSnapshotAsync(
        ResidentProfile resident,
        CancellationToken cancellationToken)
    {
        return new Darak360CommunicationSnapshotResponse(
            await dbContext.Conversations.AsNoTracking().CountAsync(item => item.ResidentProfileId == resident.Id, cancellationToken),
            await dbContext.Conversations.AsNoTracking().CountAsync(item => item.ResidentProfileId == resident.Id && item.Status != ConversationStatus.Resolved && item.Status != ConversationStatus.Closed, cancellationToken),
            await dbContext.ResidentNotifications.AsNoTracking().CountAsync(item => item.UserId == resident.UserId, cancellationToken),
            await dbContext.ResidentNotifications.AsNoTracking().CountAsync(item => item.UserId == resident.UserId && !item.IsRead, cancellationToken),
            await dbContext.Announcements.AsNoTracking().CountAsync(item => item.CompoundId == resident.CompoundId, cancellationToken),
            await dbContext.UtilityOutages.AsNoTracking().CountAsync(item => item.CompoundId == resident.CompoundId && item.ResolvedAtUtc == null, cancellationToken));
    }

    private async Task<Darak360CommunicationSnapshotResponse> BuildCompoundCommunicationSnapshotAsync(
        Guid compoundId,
        CancellationToken cancellationToken)
    {
        var residentUserIds = dbContext.ResidentProfiles
            .AsNoTracking()
            .Where(item => item.CompoundId == compoundId)
            .Select(item => item.UserId);

        return new Darak360CommunicationSnapshotResponse(
            await dbContext.Conversations.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            await dbContext.Conversations.AsNoTracking().CountAsync(item => item.CompoundId == compoundId && item.Status != ConversationStatus.Resolved && item.Status != ConversationStatus.Closed, cancellationToken),
            await dbContext.ResidentNotifications.AsNoTracking().CountAsync(item => residentUserIds.Contains(item.UserId), cancellationToken),
            await dbContext.ResidentNotifications.AsNoTracking().CountAsync(item => residentUserIds.Contains(item.UserId) && !item.IsRead, cancellationToken),
            await dbContext.Announcements.AsNoTracking().CountAsync(item => item.CompoundId == compoundId, cancellationToken),
            await dbContext.UtilityOutages.AsNoTracking().CountAsync(item => item.CompoundId == compoundId && item.ResolvedAtUtc == null, cancellationToken));
    }

    private static Darak360SignalResponse[] BuildResidentSignals(
        Darak360FinancialSnapshotResponse financial,
        Darak360OperationsSnapshotResponse operations,
        Darak360LegalRiskSnapshotResponse legal,
        Darak360CommunicationSnapshotResponse communication,
        Darak360CurrentUnitResponse? currentUnit)
    {
        var signals = new List<Darak360SignalResponse>();

        if (currentUnit is null)
        {
            signals.Add(Signal("resident-no-unit", "High", "Resident has no active unit", "Open a move-in or occupancy review before financial or access actions."));
        }

        if (financial.OutstandingAmount > 0)
        {
            signals.Add(Signal("resident-outstanding-balance", financial.OutstandingAmount > 500000 ? "Critical" : "Medium", "Outstanding balance exists", $"Resident has {financial.OutstandingAmount:n0} IQD outstanding across utility/rent invoices."));
        }

        if (legal.OpenCollectionCases > 0 || legal.OpenDisputes > 0 || legal.ActiveRiskFlags > 0)
        {
            signals.Add(Signal("resident-risk-pressure", "High", "Resident has open risk/legal pressure", "Collections, disputes, or active risk flags require coordinated follow-up."));
        }

        if (operations.MaintenanceRequests + operations.SupportCases > 3)
        {
            signals.Add(Signal("resident-service-load", "Medium", "High service load", "Resident has repeated maintenance/support activity that may affect satisfaction."));
        }

        if (communication.UnreadNotifications > 0)
        {
            signals.Add(Signal("resident-unread-notifications", "Low", "Unread notifications", "Important resident communication may not have been acknowledged."));
        }

        return signals.ToArray();
    }

    private static Darak360SignalResponse[] BuildUnitSignals(
        Darak360FinancialSnapshotResponse financial,
        Darak360UnitOperationsSnapshotResponse operations,
        Darak360LifecycleSnapshotResponse lifecycle,
        Darak360CurrentResidentResponse? currentResident,
        string unitStatus)
    {
        var signals = new List<Darak360SignalResponse>();

        if (currentResident is null && unitStatus is not "Available")
        {
            signals.Add(Signal("unit-status-without-resident", "High", "Unit status and occupancy mismatch", "Unit is not marked available, but no active resident was found."));
        }

        if (financial.OutstandingAmount > 0)
        {
            signals.Add(Signal("unit-outstanding-balance", "Medium", "Unit has financial exposure", $"Unit has {financial.OutstandingAmount:n0} IQD outstanding across linked invoices."));
        }

        if (operations.MaintenanceRequests + operations.WorkOrders > 3)
        {
            signals.Add(Signal("unit-maintenance-load", "High", "Repeated unit maintenance", "This unit has elevated maintenance/work-order history."));
        }

        if (lifecycle.OpenDamageLiabilities > 0 || lifecycle.LatestReadinessStatus is "Blocked" or "NeedsMaintenance")
        {
            signals.Add(Signal("unit-turnover-blocker", "High", "Unit turnover blocker", "Damage liability or readiness state may block handover."));
        }

        return signals.ToArray();
    }

    private static Darak360SignalResponse[] BuildCompoundSignals(
        Darak360InventorySnapshotResponse inventory,
        Darak360FinancialSnapshotResponse financial,
        Darak360OperationsSnapshotResponse operations,
        Darak360LegalRiskSnapshotResponse legal,
        Darak360CommunicationSnapshotResponse communication)
    {
        var signals = new List<Darak360SignalResponse>();

        if (inventory.Units == 0 || inventory.Residents == 0)
        {
            signals.Add(Signal("compound-demo-gap", "Medium", "Compound needs operating data", "Add realistic demo units, residents, and occupancy records before buyer presentation."));
        }

        if (financial.OutstandingAmount > 0)
        {
            signals.Add(Signal("compound-financial-exposure", financial.OutstandingAmount > 1000000 ? "Critical" : "High", "Compound has receivables exposure", $"Outstanding amount is {financial.OutstandingAmount:n0} IQD."));
        }

        if (operations.MaintenanceRequests + operations.WorkOrders + operations.SupportCases > 10)
        {
            signals.Add(Signal("compound-operational-pressure", "High", "Operational load is elevated", "Maintenance, work orders, and support cases need command-center review."));
        }

        if (legal.OpenCollectionCases > 0 || legal.ActiveRiskFlags > 0)
        {
            signals.Add(Signal("compound-legal-risk", "High", "Legal/risk follow-up required", "Collections or active risk flags need management attention."));
        }

        if (communication.ActiveOutages > 0 || communication.UnreadNotifications > 0)
        {
            signals.Add(Signal("compound-communication-pressure", "Medium", "Communication pressure exists", "Active outages or unread notifications may affect resident experience."));
        }

        return signals.ToArray();
    }

    private static string[] BuildResidentActions(IReadOnlyCollection<Darak360SignalResponse> signals)
    {
        return BuildActions(signals, "Open resident 360 review, assign owner, and document next action.");
    }

    private static string[] BuildUnitActions(IReadOnlyCollection<Darak360SignalResponse> signals)
    {
        return BuildActions(signals, "Open unit 360 review, validate occupancy/readiness, and assign operational owner.");
    }

    private static string[] BuildCompoundActions(IReadOnlyCollection<Darak360SignalResponse> signals)
    {
        return BuildActions(signals, "Review compound 360 overview in management meeting and assign module owners.");
    }

    private static string[] BuildActions(IReadOnlyCollection<Darak360SignalResponse> signals, string fallback)
    {
        if (signals.Count == 0)
        {
            return ["No immediate 360 action required; keep monitoring the profile."];
        }

        var actions = signals
            .OrderByDescending(item => item.Severity == "Critical")
            .ThenByDescending(item => item.Severity == "High")
            .Select(item => item.SignalKey switch
            {
                "resident-outstanding-balance" or "compound-financial-exposure" or "unit-outstanding-balance" => "Assign finance owner and review outstanding balance before the next billing cycle.",
                "resident-risk-pressure" or "compound-legal-risk" => "Assign collections/legal owner and verify active disputes, notices, and risk flags.",
                "unit-turnover-blocker" => "Block handover until readiness and damage liability are reviewed.",
                "unit-maintenance-load" or "compound-operational-pressure" or "resident-service-load" => "Assign operations owner to review maintenance, support, and work-order backlog.",
                "compound-communication-pressure" or "resident-unread-notifications" => "Trigger communication follow-up and confirm acknowledgment of critical messages.",
                _ => fallback
            })
            .Distinct()
            .Take(6)
            .ToArray();

        return actions.Length == 0 ? [fallback] : actions;
    }

    private static Darak360SignalResponse Signal(string key, string severity, string title, string description)
    {
        return new Darak360SignalResponse(key, severity, title, description);
    }

    private sealed record FinancialAmount(decimal TotalAmount, decimal PaidAmount);
}
