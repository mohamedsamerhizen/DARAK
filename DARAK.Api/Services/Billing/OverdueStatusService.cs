using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Financial;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class OverdueStatusService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IOverdueStatusService
{
    public async Task<ServiceResult<ProcessOverdueStatusResponse>> ProcessAsync(
        ProcessOverdueStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CompoundId == Guid.Empty)
        {
            return ServiceResult<ProcessOverdueStatusResponse>.BadRequest("Compound id is required.");
        }

        var compoundExists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(compound => compound.Id == request.CompoundId, cancellationToken);
        if (!compoundExists)
        {
            return ServiceResult<ProcessOverdueStatusResponse>.NotFound("Compound was not found.");
        }

        if (compoundAccessService is not null
            && !await compoundAccessService.CanCurrentUserAccessCompoundAsync(
                request.CompoundId,
                cancellationToken))
        {
            return ServiceResult<ProcessOverdueStatusResponse>.Forbidden(
                "Current user cannot access this compound.");
        }

        var asOfDate = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var utilityBills = await dbContext.UtilityBills
            .Where(bill => bill.CompoundId == request.CompoundId
                && bill.BillStatus == BillStatus.Unpaid
                && bill.PaidAmount == 0m
                && bill.DueDate < asOfDate)
            .ToArrayAsync(cancellationToken);
        foreach (var bill in utilityBills)
        {
            bill.BillStatus = BillStatus.Overdue;
            bill.UpdatedAt = DateTime.UtcNow;
        }

        var rentInvoices = await dbContext.RentInvoices
            .Where(invoice => invoice.CompoundId == request.CompoundId
                && invoice.RentInvoiceStatus == RentInvoiceStatus.Unpaid
                && invoice.PaidAmount == 0m
                && invoice.DueDate < asOfDate)
            .ToArrayAsync(cancellationToken);
        foreach (var invoice in rentInvoices)
        {
            invoice.RentInvoiceStatus = RentInvoiceStatus.Overdue;
            invoice.UpdatedAt = DateTime.UtcNow;
        }

        var installments = await dbContext.InstallmentScheduleItems
            .Where(installment => installment.CompoundId == request.CompoundId
                && installment.InstallmentStatus == InstallmentStatus.Pending
                && installment.PaidAmount == 0m
                && installment.DueDate < asOfDate)
            .ToArrayAsync(cancellationToken);
        foreach (var installment in installments)
        {
            installment.InstallmentStatus = InstallmentStatus.Overdue;
            installment.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProcessOverdueStatusResponse>.Success(
            new ProcessOverdueStatusResponse(
                request.CompoundId,
                asOfDate,
                utilityBills.Length,
                rentInvoices.Length,
                installments.Length));
    }
}
