using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class ProcurementInventoryServiceTests
{
    [Fact]
    public async Task CreateStockItemAsync_CreatesCompoundScopedStockItem()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "PI-1");
        var service = CreateService(dbContext, compound.Id);

        var result = await service.CreateStockItemAsync(new CreateStockItemRequest
        {
            CompoundId = compound.Id,
            Name = "Elevator contactor",
            Sku = "ELV-CON-01",
            UnitOfMeasure = "pcs",
            CurrentQuantity = 3,
            MinimumQuantity = 2,
            AverageUnitCost = 25
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Sku.Should().Be("ELV-CON-01");
        result.Value.IsLowStock.Should().BeFalse();
        dbContext.StockItems.Should().ContainSingle(item => item.CompoundId == compound.Id && item.Sku == "ELV-CON-01");
    }

    [Fact]
    public async Task CreateStockItemAsync_RejectsDuplicateSkuInsideCompound()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "PI-2");
        var service = CreateService(dbContext, compound.Id);
        var request = new CreateStockItemRequest
        {
            CompoundId = compound.Id,
            Name = "Pump filter",
            Sku = "PUMP-FLT-01",
            UnitOfMeasure = "pcs"
        };

        var first = await service.CreateStockItemAsync(request);
        var second = await service.CreateStockItemAsync(request);

        first.IsSuccess.Should().BeTrue(first.Message);
        second.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task IssueStockToWorkOrderAsync_DecreasesStockAndCreatesMaterialCost()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "PI-3");
        var service = CreateService(dbContext, compound.Id);
        var stock = await service.CreateStockItemAsync(new CreateStockItemRequest
        {
            CompoundId = compound.Id,
            Name = "Gate motor fuse",
            Sku = "GATE-FUSE-01",
            UnitOfMeasure = "pcs",
            CurrentQuantity = 10,
            MinimumQuantity = 2,
            AverageUnitCost = 5
        });
        var workOrder = await AddWorkOrderAsync(dbContext, compound.Id, "Replace gate fuse");

        var movement = await service.IssueStockToWorkOrderAsync(
            stock.Value!.Id,
            Guid.NewGuid(),
            new IssueStockToWorkOrderRequest
            {
                WorkOrderId = workOrder.Id,
                Quantity = 2,
                UnitCost = 6,
                Notes = "Used during gate repair."
            });

        movement.IsSuccess.Should().BeTrue(movement.Message);
        movement.Value!.MovementType.Should().Be(InventoryMovementType.IssuedToWorkOrder);
        movement.Value.WorkOrderId.Should().Be(workOrder.Id);
        dbContext.StockItems.Single().CurrentQuantity.Should().Be(8);
        dbContext.WorkOrderCostItems.Should().ContainSingle(item => item.WorkOrderId == workOrder.Id && item.Amount == 12);
    }

    [Fact]
    public async Task PurchaseOrderReceiveAsync_IncreasesStockAndClosesOrderWhenFullyReceived()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "PI-4");
        var vendor = await AddVendorAsync(dbContext, "Pump Parts Co");
        var service = CreateService(dbContext, compound.Id);
        var stock = await service.CreateStockItemAsync(new CreateStockItemRequest
        {
            CompoundId = compound.Id,
            Name = "Water pump belt",
            Sku = "PUMP-BELT-01",
            UnitOfMeasure = "pcs",
            CurrentQuantity = 1,
            MinimumQuantity = 2
        });
        var procurement = await service.CreateProcurementRequestAsync(
            Guid.NewGuid(),
            new CreateProcurementRequestRequest
            {
                CompoundId = compound.Id,
                Title = "Restock pump belts",
                Reason = "Low stock for pump room.",
                Items =
                [
                    new CreateProcurementRequestItemRequest
                    {
                        StockItemId = stock.Value!.Id,
                        Description = "Water pump belt",
                        Quantity = 4,
                        EstimatedUnitCost = 9
                    }
                ]
            });
        await service.ApproveProcurementRequestAsync(procurement.Value!.Id, Guid.NewGuid());
        var purchaseOrder = await service.CreatePurchaseOrderAsync(
            Guid.NewGuid(),
            new CreatePurchaseOrderRequest
            {
                CompoundId = compound.Id,
                ProcurementRequestId = procurement.Value.Id,
                VendorId = vendor.Id,
                OrderNumber = "PO-PI-4",
                Items =
                [
                    new CreatePurchaseOrderItemRequest
                    {
                        StockItemId = stock.Value.Id,
                        Description = "Water pump belt",
                        QuantityOrdered = 4,
                        UnitCost = 10
                    }
                ]
            });

        var itemId = purchaseOrder.Value!.Items.Single().Id;
        var received = await service.ReceivePurchaseOrderItemAsync(
            purchaseOrder.Value.Id,
            itemId,
            Guid.NewGuid(),
            new ReceivePurchaseOrderItemRequest { QuantityReceived = 4, Notes = "Full delivery." });

        received.IsSuccess.Should().BeTrue(received.Message);
        received.Value!.Status.Should().Be(PurchaseOrderStatus.Received);
        dbContext.StockItems.Single(item => item.Id == stock.Value.Id).CurrentQuantity.Should().Be(5);
        dbContext.InventoryMovements.Should().ContainSingle(item => item.MovementType == InventoryMovementType.ReceivedFromPurchaseOrder);
    }

    [Fact]
    public async Task SummaryAsync_RespectsAllowedCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "PI-5A");
        var denied = await AddCompoundAsync(dbContext, "PI-5B");
        var service = CreateService(dbContext, allowed.Id);
        await service.CreateStockItemAsync(new CreateStockItemRequest
        {
            CompoundId = allowed.Id,
            Name = "Allowed stock",
            Sku = "ALLOWED-01",
            CurrentQuantity = 1,
            MinimumQuantity = 5
        });
        dbContext.StockItems.Add(new StockItem
        {
            CompoundId = denied.Id,
            Name = "Denied stock",
            Sku = "DENIED-01",
            CurrentQuantity = 1,
            MinimumQuantity = 5
        });
        await dbContext.SaveChangesAsync();

        var summary = await service.GetSummaryAsync(null);

        summary.IsSuccess.Should().BeTrue(summary.Message);
        summary.Value!.ActiveStockItemCount.Should().Be(1);
        summary.Value.LowStockItemCount.Should().Be(1);
    }


    [Fact]
    public async Task Pack3SparePartsAvailabilityAndConsumption_SummarizeWorkOrderIssuedParts()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "PI-P3-1");
        var service = CreateService(dbContext, compound.Id);
        var stock = await service.CreateStockItemAsync(new CreateStockItemRequest
        {
            CompoundId = compound.Id,
            Name = "Pump bearing pack 3",
            Sku = "PUMP-BRG-P3",
            UnitOfMeasure = "pcs",
            CurrentQuantity = 2,
            MinimumQuantity = 3,
            AverageUnitCost = 11
        });
        var workOrder = await AddWorkOrderAsync(dbContext, compound.Id, "Replace pump bearing pack 3");

        var availability = await service.SearchSparePartAvailabilityAsync(new SparePartAvailabilityQuery
        {
            CompoundId = compound.Id,
            WorkOrderId = workOrder.Id,
            LowStockOnly = true,
            PageSize = 10
        });

        availability.TotalCount.Should().Be(1);
        availability.Items.Single().AvailabilityStatus.Should().Be("LowStock");
        availability.Items.Single().CanIssueToWorkOrder.Should().BeTrue();

        await service.IssueStockToWorkOrderAsync(
            stock.Value!.Id,
            Guid.NewGuid(),
            new IssueStockToWorkOrderRequest
            {
                WorkOrderId = workOrder.Id,
                Quantity = 1,
                UnitCost = 12,
                Notes = "Pack 3 material issue."
            });

        var summary = await service.GetWorkOrderSparePartConsumptionAsync(workOrder.Id);

        summary.IsSuccess.Should().BeTrue(summary.Message);
        summary.Value!.ConsumptionLineCount.Should().Be(1);
        summary.Value.TotalMaterialCost.Should().Be(12);
    }

    private static ProcurementInventoryService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new ProcurementInventoryService(dbContext, new FakeCompoundAccessService([compoundId]));
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

    private static async Task<ServiceVendor> AddVendorAsync(ApplicationDbContext dbContext, string name)
    {
        var vendor = new ServiceVendor
        {
            Name = name,
            PhoneNumber = "07700000000",
            ServiceType = VendorServiceType.Maintenance,
            Status = VendorStatus.Active
        };

        dbContext.ServiceVendors.Add(vendor);
        await dbContext.SaveChangesAsync();
        return vendor;
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
