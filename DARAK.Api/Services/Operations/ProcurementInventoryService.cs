using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Services;

public sealed class ProcurementInventoryService(
    ApplicationDbContext dbContext,
    ICompoundAccessService? compoundAccessService = null)
    : IProcurementInventoryService
{
    private const int MaxNameLength = 150;
    private const int MaxSkuLength = 80;
    private const int MaxTitleLength = 150;
    private const int MaxDescriptionLength = 2000;
    private const int MaxLineDescriptionLength = 300;
    private const int MaxNotesLength = 1000;

    public async Task<ServiceResult<StockItemResponse>> CreateStockItemAsync(
        CreateStockItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = TrimOrNull(request.Name);
        var sku = TrimOrNull(request.Sku);
        var unit = TrimOrNull(request.UnitOfMeasure);
        if (name is null || sku is null || unit is null)
        {
            return ServiceResult<StockItemResponse>.BadRequest("Stock item name, SKU and unit of measure are required.");
        }

        if (name.Length > MaxNameLength || sku.Length > MaxSkuLength || unit.Length > 30)
        {
            return ServiceResult<StockItemResponse>.BadRequest("Stock item metadata is too long.");
        }

        if (request.CurrentQuantity < 0 || request.MinimumQuantity < 0 || (request.AverageUnitCost.HasValue && request.AverageUnitCost.Value < 0))
        {
            return ServiceResult<StockItemResponse>.BadRequest("Stock quantities and costs cannot be negative.");
        }

        if (!Enum.IsDefined(request.Status))
        {
            return ServiceResult<StockItemResponse>.BadRequest("Stock item status is invalid.");
        }

        if (!await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<StockItemResponse>.Forbidden("Current user cannot access this compound.");
        }

        var compoundExists = await dbContext.Compounds
            .AsNoTracking()
            .AnyAsync(compound => compound.Id == request.CompoundId, cancellationToken);
        if (!compoundExists)
        {
            return ServiceResult<StockItemResponse>.BadRequest("Compound was not found.");
        }

        var duplicateSku = await dbContext.StockItems
            .AsNoTracking()
            .AnyAsync(item => item.CompoundId == request.CompoundId && item.Sku == sku, cancellationToken);
        if (duplicateSku)
        {
            return ServiceResult<StockItemResponse>.Conflict("A stock item with the same SKU already exists in this compound.");
        }

        var stockItem = new StockItem
        {
            CompoundId = request.CompoundId,
            Name = name,
            Sku = sku,
            Category = TrimOrNull(request.Category),
            UnitOfMeasure = unit,
            CurrentQuantity = request.CurrentQuantity,
            MinimumQuantity = request.MinimumQuantity,
            AverageUnitCost = request.AverageUnitCost,
            Status = request.Status,
            Notes = TrimOrNull(request.Notes),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.StockItems.Add(stockItem);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<StockItemResponse>.Success(ToStockItemResponse(stockItem));
    }

    public async Task<PagedResult<StockItemResponse>> SearchStockItemsAsync(
        StockItemQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var stockItems = dbContext.StockItems.AsNoTracking().AsQueryable();
        if (query.CompoundId.HasValue)
        {
            stockItems = stockItems.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.Status.HasValue)
        {
            stockItems = stockItems.Where(item => item.Status == query.Status.Value);
        }

        if (query.LowStockOnly == true)
        {
            stockItems = stockItems.Where(item => item.CurrentQuantity <= item.MinimumQuantity);
        }

        var search = TrimOrNull(query.SearchTerm);
        if (search is not null)
        {
            stockItems = stockItems.Where(item => item.Name.Contains(search)
                || item.Sku.Contains(search)
                || (item.Category != null && item.Category.Contains(search)));
        }

        stockItems = await ApplyCurrentCompoundAccessAsync(stockItems, cancellationToken);
        return await ToPagedResultAsync(stockItems.OrderBy(item => item.Name), query, ToStockItemResponse, cancellationToken);
    }

    public async Task<ServiceResult<InventoryMovementResponse>> RecordInventoryAdjustmentAsync(
        Guid stockItemId,
        Guid? currentUserId,
        InventoryAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            return ServiceResult<InventoryMovementResponse>.BadRequest("Movement quantity must be greater than zero.");
        }

        if (request.MovementType is not InventoryMovementType.AdjustmentIncrease
            and not InventoryMovementType.AdjustmentDecrease)
        {
            return ServiceResult<InventoryMovementResponse>.BadRequest("Only adjustment movement types are allowed here.");
        }

        var stockItem = await dbContext.StockItems.FirstOrDefaultAsync(item => item.Id == stockItemId, cancellationToken);
        if (stockItem is null || !await CanAccessCompoundAsync(stockItem.CompoundId, cancellationToken))
        {
            return ServiceResult<InventoryMovementResponse>.NotFound("Stock item was not found.");
        }

        if (request.MovementType == InventoryMovementType.AdjustmentDecrease
            && stockItem.CurrentQuantity < request.Quantity)
        {
            return ServiceResult<InventoryMovementResponse>.Conflict("Stock quantity is not enough for this adjustment.");
        }

        var movement = CreateMovement(
            stockItem,
            request.MovementType,
            request.Quantity,
            request.UnitCost,
            currentUserId,
            TrimOrNull(request.Notes));

        ApplyMovementToStock(stockItem, movement);
        dbContext.InventoryMovements.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<InventoryMovementResponse>.Success(ToMovementResponse(movement, stockItem.Name));
    }

    public async Task<ServiceResult<InventoryMovementResponse>> IssueStockToWorkOrderAsync(
        Guid stockItemId,
        Guid? currentUserId,
        IssueStockToWorkOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            return ServiceResult<InventoryMovementResponse>.BadRequest("Issued quantity must be greater than zero.");
        }

        var stockItem = await dbContext.StockItems.FirstOrDefaultAsync(item => item.Id == stockItemId, cancellationToken);
        if (stockItem is null || !await CanAccessCompoundAsync(stockItem.CompoundId, cancellationToken))
        {
            return ServiceResult<InventoryMovementResponse>.NotFound("Stock item was not found.");
        }

        if (stockItem.Status != StockItemStatus.Active)
        {
            return ServiceResult<InventoryMovementResponse>.Conflict("Only active stock items can be issued.");
        }

        if (stockItem.CurrentQuantity < request.Quantity)
        {
            return ServiceResult<InventoryMovementResponse>.Conflict("Stock quantity is not enough for this work order issue.");
        }

        var workOrder = await dbContext.WorkOrders.FirstOrDefaultAsync(order => order.Id == request.WorkOrderId, cancellationToken);
        if (workOrder is null || workOrder.CompoundId != stockItem.CompoundId)
        {
            return ServiceResult<InventoryMovementResponse>.BadRequest("Work order was not found in the same compound.");
        }

        var unitCost = request.UnitCost ?? stockItem.AverageUnitCost;
        var movement = CreateMovement(
            stockItem,
            InventoryMovementType.IssuedToWorkOrder,
            request.Quantity,
            unitCost,
            currentUserId,
            TrimOrNull(request.Notes));
        movement.WorkOrderId = workOrder.Id;

        ApplyMovementToStock(stockItem, movement);
        dbContext.InventoryMovements.Add(movement);

        if (unitCost.HasValue)
        {
            dbContext.WorkOrderCostItems.Add(new WorkOrderCostItem
            {
                WorkOrderId = workOrder.Id,
                Description = $"Inventory issue: {stockItem.Name}",
                CostType = WorkOrderCostType.Material,
                Amount = Math.Round(unitCost.Value * request.Quantity, 2),
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<InventoryMovementResponse>.Success(ToMovementResponse(movement, stockItem.Name));
    }

    public async Task<ServiceResult<ProcurementRequestResponse>> CreateProcurementRequestAsync(
        Guid? currentUserId,
        CreateProcurementRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = TrimOrNull(request.Title);
        var reason = TrimOrNull(request.Reason);
        if (title is null || reason is null)
        {
            return ServiceResult<ProcurementRequestResponse>.BadRequest("Procurement request title and reason are required.");
        }

        if (title.Length > MaxTitleLength || reason.Length > MaxDescriptionLength)
        {
            return ServiceResult<ProcurementRequestResponse>.BadRequest("Procurement request metadata is too long.");
        }

        if (!Enum.IsDefined(request.Priority) || !Enum.IsDefined(request.Status))
        {
            return ServiceResult<ProcurementRequestResponse>.BadRequest("Procurement request priority or status is invalid.");
        }

        if (!await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<ProcurementRequestResponse>.Forbidden("Current user cannot access this compound.");
        }

        if (request.Items.Count == 0)
        {
            return ServiceResult<ProcurementRequestResponse>.BadRequest("At least one procurement item is required.");
        }

        var validation = await ValidateStockItemReferencesAsync(request.CompoundId, request.Items.Select(item => item.StockItemId), cancellationToken);
        if (validation is not null)
        {
            return ServiceResult<ProcurementRequestResponse>.BadRequest(validation);
        }

        if (request.RelatedWorkOrderId.HasValue)
        {
            var workOrderValid = await dbContext.WorkOrders
                .AsNoTracking()
                .AnyAsync(order => order.Id == request.RelatedWorkOrderId.Value && order.CompoundId == request.CompoundId, cancellationToken);
            if (!workOrderValid)
            {
                return ServiceResult<ProcurementRequestResponse>.BadRequest("Related work order was not found in this compound.");
            }
        }

        var procurementRequest = new ProcurementRequest
        {
            CompoundId = request.CompoundId,
            RequestedByUserId = currentUserId,
            Title = title,
            Reason = reason,
            Priority = request.Priority,
            Status = request.Status,
            RelatedWorkOrderId = request.RelatedWorkOrderId,
            Notes = TrimOrNull(request.Notes),
            CreatedAtUtc = DateTime.UtcNow,
            Items = request.Items.Select(ToProcurementRequestItem).ToList()
        };

        dbContext.ProcurementRequests.Add(procurementRequest);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ProcurementRequestResponse>.Success(await LoadProcurementRequestResponseAsync(procurementRequest.Id, cancellationToken));
    }

    public async Task<PagedResult<ProcurementRequestResponse>> SearchProcurementRequestsAsync(
        ProcurementRequestQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var requests = dbContext.ProcurementRequests
            .AsNoTracking()
            .Include(item => item.Items)
                .ThenInclude(item => item.StockItem)
            .AsQueryable();

        if (query.CompoundId.HasValue)
        {
            requests = requests.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.Status.HasValue)
        {
            requests = requests.Where(item => item.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            requests = requests.Where(item => item.Priority == query.Priority.Value);
        }

        if (query.RelatedWorkOrderId.HasValue)
        {
            requests = requests.Where(item => item.RelatedWorkOrderId == query.RelatedWorkOrderId.Value);
        }

        var search = TrimOrNull(query.SearchTerm);
        if (search is not null)
        {
            requests = requests.Where(item => item.Title.Contains(search) || item.Reason.Contains(search));
        }

        requests = await ApplyCurrentCompoundAccessAsync(requests, cancellationToken);
        return await ToPagedResultAsync(requests.OrderByDescending(item => item.CreatedAtUtc), query, ToProcurementRequestResponse, cancellationToken);
    }

    public async Task<ServiceResult<ProcurementRequestResponse>> ApproveProcurementRequestAsync(
        Guid requestId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await dbContext.ProcurementRequests
            .Include(item => item.Items)
                .ThenInclude(item => item.StockItem)
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request is null || !await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<ProcurementRequestResponse>.NotFound("Procurement request was not found.");
        }

        if (request.Status is ProcurementRequestStatus.Cancelled or ProcurementRequestStatus.Rejected)
        {
            return ServiceResult<ProcurementRequestResponse>.Conflict("Cancelled or rejected requests cannot be approved.");
        }

        request.Status = ProcurementRequestStatus.Approved;
        request.ApprovedAtUtc = DateTime.UtcNow;
        request.ApprovedByUserId = currentUserId;
        request.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<ProcurementRequestResponse>.Success(ToProcurementRequestResponse(request));
    }

    public async Task<ServiceResult<PurchaseOrderResponse>> CreatePurchaseOrderAsync(
        Guid? currentUserId,
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var orderNumber = TrimOrNull(request.OrderNumber);
        if (orderNumber is null)
        {
            return ServiceResult<PurchaseOrderResponse>.BadRequest("Purchase order number is required.");
        }

        if (!Enum.IsDefined(request.Status))
        {
            return ServiceResult<PurchaseOrderResponse>.BadRequest("Purchase order status is invalid.");
        }

        if (!await CanAccessCompoundAsync(request.CompoundId, cancellationToken))
        {
            return ServiceResult<PurchaseOrderResponse>.Forbidden("Current user cannot access this compound.");
        }

        if (request.Items.Count == 0)
        {
            return ServiceResult<PurchaseOrderResponse>.BadRequest("At least one purchase order item is required.");
        }

        var vendor = await dbContext.ServiceVendors
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.VendorId, cancellationToken);
        if (vendor is null || vendor.Status != VendorStatus.Active)
        {
            return ServiceResult<PurchaseOrderResponse>.BadRequest("Active vendor was not found.");
        }

        ProcurementRequest? procurementRequest = null;
        if (request.ProcurementRequestId.HasValue)
        {
            procurementRequest = await dbContext.ProcurementRequests
                .FirstOrDefaultAsync(item => item.Id == request.ProcurementRequestId.Value, cancellationToken);
            if (procurementRequest is null || procurementRequest.CompoundId != request.CompoundId)
            {
                return ServiceResult<PurchaseOrderResponse>.BadRequest("Procurement request was not found in this compound.");
            }

            if (procurementRequest.Status != ProcurementRequestStatus.Approved)
            {
                return ServiceResult<PurchaseOrderResponse>.Conflict("Purchase orders can only be created from approved procurement requests.");
            }
        }

        var duplicateNumber = await dbContext.PurchaseOrders
            .AsNoTracking()
            .AnyAsync(item => item.CompoundId == request.CompoundId && item.OrderNumber == orderNumber, cancellationToken);
        if (duplicateNumber)
        {
            return ServiceResult<PurchaseOrderResponse>.Conflict("A purchase order with the same number already exists in this compound.");
        }

        var validation = await ValidateStockItemReferencesAsync(request.CompoundId, request.Items.Select(item => item.StockItemId), cancellationToken);
        if (validation is not null)
        {
            return ServiceResult<PurchaseOrderResponse>.BadRequest(validation);
        }

        var purchaseOrder = new PurchaseOrder
        {
            CompoundId = request.CompoundId,
            ProcurementRequestId = request.ProcurementRequestId,
            VendorId = request.VendorId,
            OrderNumber = orderNumber,
            Status = request.Status,
            OrderedAtUtc = request.OrderedAtUtc ?? DateTime.UtcNow,
            ExpectedDeliveryAtUtc = request.ExpectedDeliveryAtUtc,
            CreatedByUserId = currentUserId,
            Notes = TrimOrNull(request.Notes),
            CreatedAtUtc = DateTime.UtcNow,
            Items = request.Items.Select(ToPurchaseOrderItem).ToList()
        };

        if (procurementRequest is not null)
        {
            procurementRequest.Status = ProcurementRequestStatus.Ordered;
            procurementRequest.UpdatedAtUtc = DateTime.UtcNow;
        }

        dbContext.PurchaseOrders.Add(purchaseOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<PurchaseOrderResponse>.Success(await LoadPurchaseOrderResponseAsync(purchaseOrder.Id, cancellationToken));
    }

    public async Task<PagedResult<PurchaseOrderResponse>> SearchPurchaseOrdersAsync(
        PurchaseOrderQueryRequest query,
        CancellationToken cancellationToken = default)
    {
        var orders = dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(item => item.Vendor)
            .Include(item => item.Items)
                .ThenInclude(item => item.StockItem)
            .AsQueryable();

        if (query.CompoundId.HasValue)
        {
            orders = orders.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (query.VendorId.HasValue)
        {
            orders = orders.Where(item => item.VendorId == query.VendorId.Value);
        }

        if (query.ProcurementRequestId.HasValue)
        {
            orders = orders.Where(item => item.ProcurementRequestId == query.ProcurementRequestId.Value);
        }

        if (query.Status.HasValue)
        {
            orders = orders.Where(item => item.Status == query.Status.Value);
        }

        var search = TrimOrNull(query.SearchTerm);
        if (search is not null)
        {
            orders = orders.Where(item => item.OrderNumber.Contains(search)
                || item.Vendor.Name.Contains(search));
        }

        orders = await ApplyCurrentCompoundAccessAsync(orders, cancellationToken);
        return await ToPagedResultAsync(orders.OrderByDescending(item => item.CreatedAtUtc), query, ToPurchaseOrderResponse, cancellationToken);
    }

    public async Task<ServiceResult<PurchaseOrderResponse>> ReceivePurchaseOrderItemAsync(
        Guid purchaseOrderId,
        Guid itemId,
        Guid? currentUserId,
        ReceivePurchaseOrderItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.QuantityReceived <= 0)
        {
            return ServiceResult<PurchaseOrderResponse>.BadRequest("Received quantity must be greater than zero.");
        }

        var purchaseOrder = await dbContext.PurchaseOrders
            .Include(order => order.Vendor)
            .Include(order => order.Items)
                .ThenInclude(item => item.StockItem)
            .FirstOrDefaultAsync(order => order.Id == purchaseOrderId, cancellationToken);
        if (purchaseOrder is null || !await CanAccessCompoundAsync(purchaseOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<PurchaseOrderResponse>.NotFound("Purchase order was not found.");
        }

        if (purchaseOrder.Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Closed)
        {
            return ServiceResult<PurchaseOrderResponse>.Conflict("Closed or cancelled purchase orders cannot be received.");
        }

        var item = purchaseOrder.Items.FirstOrDefault(line => line.Id == itemId);
        if (item is null)
        {
            return ServiceResult<PurchaseOrderResponse>.NotFound("Purchase order item was not found.");
        }

        if (item.StockItemId is null || item.StockItem is null)
        {
            return ServiceResult<PurchaseOrderResponse>.BadRequest("Purchase order item is not linked to a stock item.");
        }

        var remaining = item.QuantityOrdered - item.QuantityReceived;
        if (request.QuantityReceived > remaining)
        {
            return ServiceResult<PurchaseOrderResponse>.Conflict("Received quantity cannot exceed remaining order quantity.");
        }

        item.QuantityReceived += request.QuantityReceived;
        item.Notes = MergeNotes(item.Notes, request.Notes);
        item.StockItem.CurrentQuantity += request.QuantityReceived;
        item.StockItem.AverageUnitCost = item.UnitCost;
        item.StockItem.UpdatedAtUtc = DateTime.UtcNow;

        dbContext.InventoryMovements.Add(new InventoryMovement
        {
            CompoundId = purchaseOrder.CompoundId,
            StockItemId = item.StockItemId.Value,
            PurchaseOrderItemId = item.Id,
            MovementType = InventoryMovementType.ReceivedFromPurchaseOrder,
            Quantity = request.QuantityReceived,
            UnitCost = item.UnitCost,
            CreatedByUserId = currentUserId,
            Notes = TrimOrNull(request.Notes),
            CreatedAtUtc = DateTime.UtcNow
        });

        purchaseOrder.Status = purchaseOrder.Items.All(line => line.QuantityReceived >= line.QuantityOrdered)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;
        purchaseOrder.ReceivedAtUtc = purchaseOrder.Status == PurchaseOrderStatus.Received ? DateTime.UtcNow : purchaseOrder.ReceivedAtUtc;
        purchaseOrder.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<PurchaseOrderResponse>.Success(ToPurchaseOrderResponse(purchaseOrder));
    }

    public async Task<ServiceResult<ProcurementInventorySummaryResponse>> GetSummaryAsync(
        Guid? compoundId,
        CancellationToken cancellationToken = default)
    {
        if (compoundId.HasValue && !await CanAccessCompoundAsync(compoundId.Value, cancellationToken))
        {
            return ServiceResult<ProcurementInventorySummaryResponse>.Forbidden("Current user cannot access this compound.");
        }

        var stockItems = dbContext.StockItems.AsNoTracking().AsQueryable();
        var requests = dbContext.ProcurementRequests.AsNoTracking().AsQueryable();
        var purchaseOrders = dbContext.PurchaseOrders.AsNoTracking().Include(order => order.Items).AsQueryable();
        if (compoundId.HasValue)
        {
            stockItems = stockItems.Where(item => item.CompoundId == compoundId.Value);
            requests = requests.Where(item => item.CompoundId == compoundId.Value);
            purchaseOrders = purchaseOrders.Where(item => item.CompoundId == compoundId.Value);
        }
        else
        {
            stockItems = await ApplyCurrentCompoundAccessAsync(stockItems, cancellationToken);
            requests = await ApplyCurrentCompoundAccessAsync(requests, cancellationToken);
            purchaseOrders = await ApplyCurrentCompoundAccessAsync(purchaseOrders, cancellationToken);
        }

        var activeStockItemCount = await stockItems.CountAsync(item => item.Status == StockItemStatus.Active, cancellationToken);
        var lowStockItemCount = await stockItems.CountAsync(item => item.CurrentQuantity <= item.MinimumQuantity, cancellationToken);
        var pendingRequestCount = await requests.CountAsync(item => item.Status == ProcurementRequestStatus.PendingApproval, cancellationToken);
        var openPurchaseOrderCount = await purchaseOrders.CountAsync(item => item.Status == PurchaseOrderStatus.Ordered || item.Status == PurchaseOrderStatus.PartiallyReceived, cancellationToken);
        var inventoryValue = await stockItems
            .SumAsync(item => (decimal?)(item.CurrentQuantity * (item.AverageUnitCost ?? 0m)), cancellationToken) ?? 0m;
        var openPoValue = await purchaseOrders
            .Where(item => item.Status == PurchaseOrderStatus.Ordered || item.Status == PurchaseOrderStatus.PartiallyReceived)
            .SelectMany(item => item.Items)
            .SumAsync(item => (decimal?)((item.QuantityOrdered - item.QuantityReceived) * item.UnitCost), cancellationToken) ?? 0m;

        return ServiceResult<ProcurementInventorySummaryResponse>.Success(new ProcurementInventorySummaryResponse(
            compoundId,
            activeStockItemCount,
            lowStockItemCount,
            pendingRequestCount,
            openPurchaseOrderCount,
            inventoryValue,
            openPoValue));
    }


    public async Task<PagedResult<SparePartAvailabilityItemResponse>> SearchSparePartAvailabilityAsync(
        SparePartAvailabilityQuery query,
        CancellationToken cancellationToken = default)
    {
        Guid? workOrderCompoundId = null;
        if (query.WorkOrderId.HasValue)
        {
            var workOrder = await dbContext.WorkOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == query.WorkOrderId.Value, cancellationToken);
            if (workOrder is not null && await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
            {
                workOrderCompoundId = workOrder.CompoundId;
            }
            else
            {
                return new PagedResult<SparePartAvailabilityItemResponse>(Array.Empty<SparePartAvailabilityItemResponse>(), query.PageNumber, query.PageSize, 0);
            }
        }

        var stockItems = dbContext.StockItems.AsNoTracking().AsQueryable();
        if (query.CompoundId.HasValue)
        {
            stockItems = stockItems.Where(item => item.CompoundId == query.CompoundId.Value);
        }

        if (workOrderCompoundId.HasValue)
        {
            stockItems = stockItems.Where(item => item.CompoundId == workOrderCompoundId.Value);
        }

        if (query.LowStockOnly)
        {
            stockItems = stockItems.Where(item => item.CurrentQuantity <= item.MinimumQuantity);
        }

        var search = TrimOrNull(query.SearchTerm);
        if (search is not null)
        {
            stockItems = stockItems.Where(item => item.Name.Contains(search)
                || item.Sku.Contains(search)
                || (item.Category != null && item.Category.Contains(search)));
        }

        stockItems = await ApplyCurrentCompoundAccessAsync(stockItems, cancellationToken);
        var totalCount = await stockItems.CountAsync(cancellationToken);
        var items = await stockItems
            .OrderBy(item => item.CurrentQuantity <= item.MinimumQuantity ? 0 : 1)
            .ThenBy(item => item.Name)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SparePartAvailabilityItemResponse>(
            items.Select(item => ToSparePartAvailabilityItemResponse(item, query.WorkOrderId.HasValue)).ToArray(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<ServiceResult<WorkOrderSparePartConsumptionSummaryResponse>> GetWorkOrderSparePartConsumptionAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default)
    {
        var workOrder = await dbContext.WorkOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == workOrderId, cancellationToken);
        if (workOrder is null || !await CanAccessCompoundAsync(workOrder.CompoundId, cancellationToken))
        {
            return ServiceResult<WorkOrderSparePartConsumptionSummaryResponse>.NotFound("Work order was not found.");
        }

        var movements = await dbContext.InventoryMovements
            .AsNoTracking()
            .Include(item => item.StockItem)
            .Where(item => item.WorkOrderId == workOrderId && item.MovementType == InventoryMovementType.IssuedToWorkOrder)
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var lines = movements.Select(ToSparePartConsumptionLineResponse).ToArray();

        return ServiceResult<WorkOrderSparePartConsumptionSummaryResponse>.Success(new WorkOrderSparePartConsumptionSummaryResponse(
            workOrder.Id,
            workOrder.CompoundId,
            workOrder.Title,
            lines.Length,
            lines.Sum(item => item.Quantity),
            lines.Where(item => item.LineCost.HasValue).Sum(item => item.LineCost!.Value),
            lines));
    }

    private async Task<string?> ValidateStockItemReferencesAsync(
        Guid compoundId,
        IEnumerable<Guid?> stockItemIds,
        CancellationToken cancellationToken)
    {
        var ids = stockItemIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return null;
        }

        var validCount = await dbContext.StockItems
            .AsNoTracking()
            .CountAsync(item => ids.Contains(item.Id) && item.CompoundId == compoundId, cancellationToken);
        return validCount == ids.Length ? null : "One or more stock items were not found in this compound.";
    }

    private static ProcurementRequestItem ToProcurementRequestItem(CreateProcurementRequestItemRequest item)
    {
        return new ProcurementRequestItem
        {
            StockItemId = item.StockItemId,
            Description = TrimOrNull(item.Description) ?? string.Empty,
            Quantity = item.Quantity,
            EstimatedUnitCost = item.EstimatedUnitCost
        };
    }

    private static PurchaseOrderItem ToPurchaseOrderItem(CreatePurchaseOrderItemRequest item)
    {
        return new PurchaseOrderItem
        {
            StockItemId = item.StockItemId,
            Description = TrimOrNull(item.Description) ?? string.Empty,
            QuantityOrdered = item.QuantityOrdered,
            UnitCost = item.UnitCost,
            Notes = TrimOrNull(item.Notes)
        };
    }

    private static InventoryMovement CreateMovement(
        StockItem stockItem,
        InventoryMovementType movementType,
        decimal quantity,
        decimal? unitCost,
        Guid? currentUserId,
        string? notes)
    {
        return new InventoryMovement
        {
            CompoundId = stockItem.CompoundId,
            StockItemId = stockItem.Id,
            MovementType = movementType,
            Quantity = quantity,
            UnitCost = unitCost,
            CreatedByUserId = currentUserId,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static void ApplyMovementToStock(StockItem stockItem, InventoryMovement movement)
    {
        if (movement.MovementType is InventoryMovementType.ReceivedFromPurchaseOrder or InventoryMovementType.AdjustmentIncrease)
        {
            stockItem.CurrentQuantity += movement.Quantity;
        }
        else
        {
            stockItem.CurrentQuantity -= movement.Quantity;
        }

        if (movement.UnitCost.HasValue)
        {
            stockItem.AverageUnitCost = movement.UnitCost;
        }

        stockItem.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task<ProcurementRequestResponse> LoadProcurementRequestResponseAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var request = await dbContext.ProcurementRequests
            .AsNoTracking()
            .Include(item => item.Items)
                .ThenInclude(item => item.StockItem)
            .SingleAsync(item => item.Id == requestId, cancellationToken);
        return ToProcurementRequestResponse(request);
    }

    private async Task<PurchaseOrderResponse> LoadPurchaseOrderResponseAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var purchaseOrder = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(item => item.Vendor)
            .Include(item => item.Items)
                .ThenInclude(item => item.StockItem)
            .SingleAsync(item => item.Id == purchaseOrderId, cancellationToken);
        return ToPurchaseOrderResponse(purchaseOrder);
    }

    private static StockItemResponse ToStockItemResponse(StockItem item)
    {
        return new StockItemResponse(
            item.Id,
            item.CompoundId,
            item.Name,
            item.Sku,
            item.Category,
            item.UnitOfMeasure,
            item.CurrentQuantity,
            item.MinimumQuantity,
            item.AverageUnitCost,
            item.Status,
            item.CurrentQuantity <= item.MinimumQuantity,
            item.Notes,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    private static InventoryMovementResponse ToMovementResponse(InventoryMovement movement, string stockItemName)
    {
        return new InventoryMovementResponse(
            movement.Id,
            movement.CompoundId,
            movement.StockItemId,
            stockItemName,
            movement.MovementType,
            movement.Quantity,
            movement.UnitCost,
            movement.WorkOrderId,
            movement.PurchaseOrderItemId,
            movement.CreatedByUserId,
            movement.Notes,
            movement.CreatedAtUtc);
    }

    private static ProcurementRequestResponse ToProcurementRequestResponse(ProcurementRequest request)
    {
        var items = request.Items
            .OrderBy(item => item.Description)
            .Select(item => new ProcurementRequestItemResponse(
                item.Id,
                item.StockItemId,
                item.StockItem?.Name,
                item.Description,
                item.Quantity,
                item.EstimatedUnitCost,
                item.EstimatedUnitCost.HasValue ? item.Quantity * item.EstimatedUnitCost.Value : null))
            .ToArray();

        return new ProcurementRequestResponse(
            request.Id,
            request.CompoundId,
            request.RequestedByUserId,
            request.Title,
            request.Reason,
            request.Priority,
            request.Status,
            request.RelatedWorkOrderId,
            request.Notes,
            request.CreatedAtUtc,
            request.ApprovedAtUtc,
            request.ApprovedByUserId,
            request.UpdatedAtUtc,
            items.Where(item => item.EstimatedTotal.HasValue).Sum(item => item.EstimatedTotal!.Value),
            items);
    }

    private static PurchaseOrderResponse ToPurchaseOrderResponse(PurchaseOrder order)
    {
        var items = order.Items
            .OrderBy(item => item.Description)
            .Select(item => new PurchaseOrderItemResponse(
                item.Id,
                item.StockItemId,
                item.StockItem?.Name,
                item.Description,
                item.QuantityOrdered,
                item.QuantityReceived,
                item.QuantityOrdered - item.QuantityReceived,
                item.UnitCost,
                item.QuantityOrdered * item.UnitCost,
                item.Notes))
            .ToArray();

        return new PurchaseOrderResponse(
            order.Id,
            order.CompoundId,
            order.ProcurementRequestId,
            order.VendorId,
            order.Vendor.Name,
            order.OrderNumber,
            order.Status,
            order.OrderedAtUtc,
            order.ExpectedDeliveryAtUtc,
            order.ReceivedAtUtc,
            order.CreatedByUserId,
            order.Notes,
            order.CreatedAtUtc,
            order.UpdatedAtUtc,
            items.Sum(item => item.LineTotal),
            items);
    }

    private async Task<IQueryable<T>> ApplyCurrentCompoundAccessAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken)
        where T : class
    {
        if (compoundAccessService is null)
        {
            return query;
        }

        var scope = await compoundAccessService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsSuperAdmin)
        {
            return query;
        }

        var allowedIds = scope.AllowedCompoundIds;
        if (typeof(T) == typeof(StockItem))
        {
            return (IQueryable<T>)((IQueryable<StockItem>)query).Where(item => allowedIds.Contains(item.CompoundId));
        }

        if (typeof(T) == typeof(ProcurementRequest))
        {
            return (IQueryable<T>)((IQueryable<ProcurementRequest>)query).Where(item => allowedIds.Contains(item.CompoundId));
        }

        if (typeof(T) == typeof(PurchaseOrder))
        {
            return (IQueryable<T>)((IQueryable<PurchaseOrder>)query).Where(item => allowedIds.Contains(item.CompoundId));
        }

        return query;
    }

    private async Task<bool> CanAccessCompoundAsync(Guid compoundId, CancellationToken cancellationToken)
    {
        return compoundAccessService is null
            || await compoundAccessService.CanCurrentUserAccessCompoundAsync(compoundId, cancellationToken);
    }

    private static async Task<PagedResult<TResponse>> ToPagedResultAsync<TEntity, TResponse>(
        IQueryable<TEntity> query,
        PaginationQuery pagination,
        Func<TEntity, TResponse> selector,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<TResponse>(items.Select(selector).ToArray(), pagination.PageNumber, pagination.PageSize, totalCount);
    }


    private static SparePartAvailabilityItemResponse ToSparePartAvailabilityItemResponse(StockItem item, bool hasWorkOrderContext)
    {
        var isLowStock = item.CurrentQuantity <= item.MinimumQuantity;
        var canIssue = hasWorkOrderContext && item.Status == StockItemStatus.Active && item.CurrentQuantity > 0;
        var availabilityStatus = item.Status != StockItemStatus.Active
            ? "Unavailable"
            : item.CurrentQuantity <= 0
                ? "OutOfStock"
                : isLowStock
                    ? "LowStock"
                    : "Available";
        var recommendedAction = availabilityStatus switch
        {
            "Unavailable" => "Do not issue this part until it is reactivated or replaced.",
            "OutOfStock" => "Create an urgent procurement request before scheduling dependent maintenance.",
            "LowStock" => "Reserve usage for critical work orders and replenish stock.",
            _ => canIssue ? "Available for issue to the selected work order." : "Available for future work order consumption."
        };

        return new SparePartAvailabilityItemResponse(
            item.Id,
            item.CompoundId,
            item.Name,
            item.Sku,
            item.Category,
            item.UnitOfMeasure,
            item.CurrentQuantity,
            item.MinimumQuantity,
            Math.Max(item.CurrentQuantity, 0m),
            item.AverageUnitCost,
            isLowStock,
            canIssue,
            availabilityStatus,
            recommendedAction);
    }

    private static SparePartConsumptionLineResponse ToSparePartConsumptionLineResponse(InventoryMovement movement)
    {
        var lineCost = movement.UnitCost.HasValue
            ? Math.Round(movement.UnitCost.Value * movement.Quantity, 2)
            : (decimal?)null;
        return new SparePartConsumptionLineResponse(
            movement.Id,
            movement.StockItemId,
            movement.StockItem.Name,
            movement.StockItem.Sku,
            movement.Quantity,
            movement.UnitCost,
            lineCost,
            movement.CreatedByUserId,
            movement.Notes,
            movement.CreatedAtUtc);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? MergeNotes(string? current, string? incoming)
    {
        var note = TrimOrNull(incoming);
        if (note is null)
        {
            return current;
        }

        return string.IsNullOrWhiteSpace(current) ? note : $"{current}\n{note}";
    }
}
