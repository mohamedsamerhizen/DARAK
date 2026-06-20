using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class OwnershipServiceTests
{
    [Fact]
    public async Task RateWorkOrderAsync_ResidentCannotRateUnownedCommonAreaWorkOrder()
    {
        await using var dbContext = TestDb.Create();
        var userId = Guid.NewGuid();
        var workOrder = new WorkOrder
        {
            Title = "Lobby repair",
            Description = "Common area repair",
            Status = WorkOrderStatus.Completed,
            SourceType = WorkOrderSourceType.Manual
        };
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync();

        var service = new OperationsService(dbContext);
        var result = await service.RateWorkOrderAsync(
            workOrder.Id,
            userId,
            isManager: false,
            new CreateWorkOrderRatingRequest { Rating = 5 });

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }
}
