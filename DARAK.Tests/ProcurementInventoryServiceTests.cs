using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
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
    public async Task Phase8_IssueStockToWorkOrderAsync_RejectsOverspendAndKeepsStockNonNegative()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "PI-P8-NEG");
        var service = CreateService(dbContext, compound.Id);
        var stock = await service.CreateStockItemAsync(new CreateStockItemRequest
        {
            CompoundId = compound.Id,
            Name = "Limited fuse",
            Sku = "LIMITED-FUSE-01",
            UnitOfMeasure = "pcs",
            CurrentQuantity = 1,
            MinimumQuantity = 0
        });
        var workOrder = await AddWorkOrderAsync(dbContext, compound.Id, "Use limited fuse");

        var result = await service.IssueStockToWorkOrderAsync(
            stock.Value!.Id,
            Guid.NewGuid(),
            new IssueStockToWorkOrderRequest { WorkOrderId = workOrder.Id, Quantity = 2 });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        dbContext.StockItems.Single(item => item.Id == stock.Value.Id).CurrentQuantity.Should().Be(1);
        dbContext.StockItems.Single(item => item.Id == stock.Value.Id).CurrentQuantity.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task PurchaseOrderReceiveAsync_IncreasesStockAndClosesOrderWhenFullyReceived()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "PI-4");
        var vendor = await AddVendorAsync(dbContext, compound.Id, "Pump Parts Co");
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
    public async Task InventoryIssuePurchaseOrderApproveAndReceive_WriteAuditEntries()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "PI-AUDIT");
        var vendor = await AddVendorAsync(dbContext, compound.Id, "Audit Supplier");
        var service = CreateAuditedService(dbContext, compound.Id);
        var stock = await service.CreateStockItemAsync(new CreateStockItemRequest
        {
            CompoundId = compound.Id,
            Name = "Audit bearing",
            Sku = "AUDIT-BRG-01",
            UnitOfMeasure = "pcs",
            CurrentQuantity = 5,
            MinimumQuantity = 1,
            AverageUnitCost = 10
        });
        var workOrder = await AddWorkOrderAsync(dbContext, compound.Id, "Audit work order");

        await service.IssueStockToWorkOrderAsync(
            stock.Value!.Id,
            Guid.NewGuid(),
            new IssueStockToWorkOrderRequest
            {
                WorkOrderId = workOrder.Id,
                Quantity = 1,
                UnitCost = 11,
                Notes = "Audit material issue."
            });
        var purchaseOrder = await service.CreatePurchaseOrderAsync(Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            CompoundId = compound.Id,
            VendorId = vendor.Id,
            OrderNumber = "PO-AUDIT-1",
            Status = PurchaseOrderStatus.Draft,
            Items =
            [
                new CreatePurchaseOrderItemRequest
                {
                    StockItemId = stock.Value.Id,
                    Description = "Audit bearing",
                    QuantityOrdered = 2,
                    UnitCost = 10
                }
            ]
        });
        var approved = await service.ApprovePurchaseOrderAsync(purchaseOrder.Value!.Id, Guid.NewGuid());
        await service.ReceivePurchaseOrderItemAsync(
            approved.Value!.Id,
            approved.Value.Items.Single().Id,
            Guid.NewGuid(),
            new ReceivePurchaseOrderItemRequest { QuantityReceived = 2, ReceiptReference = "GRN-AUDIT-1" });

        dbContext.AuditLogEntries.Should().Contain(item => item.ActionType == AuditActionType.InventoryIssued);
        dbContext.AuditLogEntries.Should().Contain(item => item.ActionType == AuditActionType.PurchaseOrderApproved);
        dbContext.AuditLogEntries.Should().Contain(item => item.ActionType == AuditActionType.PurchaseOrderReceived);
    }

    [Fact]
    public async Task Phase8_ReceivePurchaseOrderItemAsync_ReusesReceiptReferenceWithoutDoubleIncreasingStock()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "PI-P8-REF");
        var vendor = await AddVendorAsync(dbContext, compound.Id, "Reference Supplier");
        var service = CreateService(dbContext, compound.Id);
        var stock = await service.CreateStockItemAsync(new CreateStockItemRequest
        {
            CompoundId = compound.Id,
            Name = "Reference bearing",
            Sku = "REF-BRG-01",
            UnitOfMeasure = "pcs",
            CurrentQuantity = 0
        });
        var purchaseOrder = await service.CreatePurchaseOrderAsync(Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            CompoundId = compound.Id,
            VendorId = vendor.Id,
            OrderNumber = "PO-REF-1",
            Items =
            [
                new CreatePurchaseOrderItemRequest
                {
                    StockItemId = stock.Value!.Id,
                    Description = "Reference bearing",
                    QuantityOrdered = 4,
                    UnitCost = 10
                }
            ]
        });
        var itemId = purchaseOrder.Value!.Items.Single().Id;

        var first = await service.ReceivePurchaseOrderItemAsync(
            purchaseOrder.Value.Id,
            itemId,
            Guid.NewGuid(),
            new ReceivePurchaseOrderItemRequest { QuantityReceived = 2, ReceiptReference = "GRN-REF-1" });
        var retry = await service.ReceivePurchaseOrderItemAsync(
            purchaseOrder.Value.Id,
            itemId,
            Guid.NewGuid(),
            new ReceivePurchaseOrderItemRequest { QuantityReceived = 2, ReceiptReference = "GRN-REF-1" });

        first.IsSuccess.Should().BeTrue(first.Message);
        retry.IsSuccess.Should().BeTrue(retry.Message);
        dbContext.StockItems.Single(item => item.Id == stock.Value.Id).CurrentQuantity.Should().Be(2);
        dbContext.InventoryMovements.Count(item => item.Reference == "GRN-REF-1").Should().Be(1);
    }

    [Fact]
    public async Task Phase8_PurchaseOrderStateRules_BlockDraftReceiveAndReceivedCancel()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "PI-P8-STATE");
        var vendor = await AddVendorAsync(dbContext, compound.Id, "State Supplier");
        var service = CreateService(dbContext, compound.Id);
        var stock = await service.CreateStockItemAsync(new CreateStockItemRequest
        {
            CompoundId = compound.Id,
            Name = "State belt",
            Sku = "STATE-BELT-01",
            UnitOfMeasure = "pcs"
        });
        var draft = await service.CreatePurchaseOrderAsync(Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            CompoundId = compound.Id,
            VendorId = vendor.Id,
            OrderNumber = "PO-DRAFT-STATE",
            Status = PurchaseOrderStatus.Draft,
            Items =
            [
                new CreatePurchaseOrderItemRequest
                {
                    StockItemId = stock.Value!.Id,
                    Description = "State belt",
                    QuantityOrdered = 1,
                    UnitCost = 5
                }
            ]
        });

        var draftReceive = await service.ReceivePurchaseOrderItemAsync(
            draft.Value!.Id,
            draft.Value.Items.Single().Id,
            Guid.NewGuid(),
            new ReceivePurchaseOrderItemRequest { QuantityReceived = 1 });
        var approved = await service.ApprovePurchaseOrderAsync(draft.Value.Id, Guid.NewGuid());
        var received = await service.ReceivePurchaseOrderItemAsync(
            approved.Value!.Id,
            approved.Value.Items.Single().Id,
            Guid.NewGuid(),
            new ReceivePurchaseOrderItemRequest { QuantityReceived = 1 });
        var cancelReceived = await service.CancelPurchaseOrderAsync(
            received.Value!.Id,
            Guid.NewGuid(),
            new CancelPurchaseOrderRequest { Reason = "No longer needed." });

        draftReceive.Status.Should().Be(ServiceResultStatus.Conflict);
        approved.Value!.Status.Should().Be(PurchaseOrderStatus.Approved);
        received.Value!.Status.Should().Be(PurchaseOrderStatus.Received);
        cancelReceived.Status.Should().Be(ServiceResultStatus.Conflict);
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

    [Fact]
    public async Task CreatePurchaseOrderAsync_RejectsVendorFromDifferentCompound()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "PI-VS-A");
        var denied = await AddCompoundAsync(dbContext, "PI-VS-D");
        var vendor = await AddVendorAsync(dbContext, denied.Id, "Cross Compound Supplier");
        var service = CreateService(dbContext, allowed.Id);
        var stock = await service.CreateStockItemAsync(new CreateStockItemRequest
        {
            CompoundId = allowed.Id,
            Name = "Cross vendor stock",
            Sku = "CROSS-VENDOR-STOCK",
            UnitOfMeasure = "pcs"
        });
        var procurement = await service.CreateProcurementRequestAsync(
            Guid.NewGuid(),
            new CreateProcurementRequestRequest
            {
                CompoundId = allowed.Id,
                Title = "Cross vendor procurement",
                Reason = "Tenant scoping check.",
                Items =
                [
                    new CreateProcurementRequestItemRequest
                    {
                        StockItemId = stock.Value!.Id,
                        Description = "Cross vendor stock",
                        Quantity = 1,
                        EstimatedUnitCost = 5
                    }
                ]
            });
        await service.ApproveProcurementRequestAsync(procurement.Value!.Id, Guid.NewGuid());

        var result = await service.CreatePurchaseOrderAsync(
            Guid.NewGuid(),
            new CreatePurchaseOrderRequest
            {
                CompoundId = allowed.Id,
                ProcurementRequestId = procurement.Value.Id,
                VendorId = vendor.Id,
                OrderNumber = "PO-CROSS-VENDOR",
                Items =
                [
                    new CreatePurchaseOrderItemRequest
                    {
                        StockItemId = stock.Value.Id,
                        Description = "Cross vendor stock",
                        QuantityOrdered = 1,
                        UnitCost = 5
                    }
                ]
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Contain("selected compound");
    }

    private static ProcurementInventoryService CreateService(ApplicationDbContext dbContext, Guid compoundId)
    {
        return new ProcurementInventoryService(dbContext, new FakeCompoundAccessService([compoundId]));
    }

    private static ProcurementInventoryService CreateAuditedService(ApplicationDbContext dbContext, Guid compoundId)
    {
        var compoundAccess = new FakeCompoundAccessService([compoundId]);
        return new ProcurementInventoryService(
            dbContext,
            compoundAccess,
            new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor()));
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

    private static async Task<ServiceVendor> AddVendorAsync(ApplicationDbContext dbContext, Guid compoundId, string name)
    {
        var vendor = new ServiceVendor
        {
            CompoundId = compoundId,
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
