using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class OperationsTenantIsolationTests
{
    [Fact]
    public async Task AssignWorkOrderToStaffAsync_RejectsStaffFromDifferentCompound()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "WO-ST-A");
        var denied = await AddCompoundAsync(dbContext, "WO-ST-D");
        var workOrder = await AddWorkOrderAsync(dbContext, allowed.Id, "Allowed work order");
        var staff = new StaffMember
        {
            CompoundId = denied.Id,
            FullName = "Cross Compound Technician",
            PhoneNumber = "07700001000",
            StaffType = StaffType.MaintenanceTechnician,
            Status = StaffStatus.Active
        };
        dbContext.StaffMembers.Add(staff);
        await dbContext.SaveChangesAsync();
        var service = new OperationsService(dbContext, new FakeCompoundAccessService([allowed.Id]));

        var result = await service.AssignWorkOrderToStaffAsync(
            workOrder.Id,
            Guid.NewGuid(),
            new AssignWorkOrderToStaffRequest { StaffMemberId = staff.Id });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("work order compound");
    }

    [Fact]
    public async Task AssignWorkOrderToVendorAsync_RejectsVendorFromDifferentCompound()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "WO-VD-A");
        var denied = await AddCompoundAsync(dbContext, "WO-VD-D");
        var workOrder = await AddWorkOrderAsync(dbContext, allowed.Id, "Allowed vendor work order");
        var vendor = new ServiceVendor
        {
            CompoundId = denied.Id,
            Name = "Cross Compound Vendor",
            PhoneNumber = "07700001001",
            ServiceType = VendorServiceType.Maintenance,
            Status = VendorStatus.Active
        };
        dbContext.ServiceVendors.Add(vendor);
        await dbContext.SaveChangesAsync();
        var service = new OperationsService(dbContext, new FakeCompoundAccessService([allowed.Id]));

        var result = await service.AssignWorkOrderToVendorAsync(
            workOrder.Id,
            Guid.NewGuid(),
            new AssignWorkOrderToVendorRequest { VendorId = vendor.Id });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("work order compound");
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada",
            Address = "Baghdad"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<WorkOrder> AddWorkOrderAsync(ApplicationDbContext dbContext, Guid compoundId, string title)
    {
        var workOrder = new WorkOrder
        {
            CompoundId = compoundId,
            Title = title,
            Description = title,
            Priority = WorkOrderPriority.Normal,
            Status = WorkOrderStatus.New,
            SourceType = WorkOrderSourceType.Manual
        };

        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();
        return workOrder;
    }
}
