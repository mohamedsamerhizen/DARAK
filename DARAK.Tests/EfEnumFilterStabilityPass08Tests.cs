using DARAK.Api.Data;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class EfEnumFilterStabilityPass08Tests
{
    [Fact]
    public void Pass08_EnumValuesUsedByFilteredIndexes_AreExplicitlyStable()
    {
        ((int)OccupancyStatus.Active).Should().Be(0);
        ((int)OccupancyStatus.Ended).Should().Be(1);
        ((int)OccupancyStatus.Cancelled).Should().Be(2);

        ((int)SaleContractStatus.Active).Should().Be(0);
        ((int)SaleContractStatus.Completed).Should().Be(1);
        ((int)SaleContractStatus.Cancelled).Should().Be(2);

        ((int)RentContractStatus.Active).Should().Be(0);
        ((int)RentContractStatus.Terminated).Should().Be(1);
        ((int)RentContractStatus.Expired).Should().Be(2);
        ((int)RentContractStatus.Cancelled).Should().Be(3);

        ((int)ViolationFineStatus.Unpaid).Should().Be(0);
        ((int)ViolationFineStatus.PartiallyPaid).Should().Be(1);
        ((int)ViolationFineStatus.Paid).Should().Be(2);
        ((int)ViolationFineStatus.Cancelled).Should().Be(3);

        ((int)OwnershipTransferStatus.PendingApproval).Should().Be(1);
        ((int)OwnershipTransferStatus.Approved).Should().Be(2);
        ((int)OwnershipTransferStatus.Rejected).Should().Be(3);
        ((int)OwnershipTransferStatus.Completed).Should().Be(4);
        ((int)OwnershipTransferStatus.Cancelled).Should().Be(5);
    }

    [Theory]
    [InlineData(typeof(OccupancyRecord), nameof(OccupancyRecord.PropertyUnitId), "[OccupancyStatus] = 0")]
    [InlineData(typeof(PropertySaleContract), nameof(PropertySaleContract.PropertyUnitId), "[ContractStatus] = 0")]
    [InlineData(typeof(RentContract), nameof(RentContract.PropertyUnitId), "[ContractStatus] = 0")]
    [InlineData(typeof(ViolationFine), nameof(ViolationFine.ViolationId), "[Status] <> 3")]
    [InlineData(typeof(OwnershipTransferRequest), nameof(OwnershipTransferRequest.PropertyUnitId), "[Status] = 1")]
    public void Pass08_FilteredUniqueIndexes_KeepExpectedEnumBackedSqlFilters(
        Type entityType,
        string indexedPropertyName,
        string expectedFilter)
    {
        using var dbContext = TestDb.Create();

        var entity = dbContext.Model.FindEntityType(entityType);
        entity.Should().NotBeNull();

        var index = entity!.GetIndexes()
            .SingleOrDefault(candidate => candidate.IsUnique
                && candidate.Properties.Count == 1
                && candidate.Properties[0].Name == indexedPropertyName
                && !string.IsNullOrWhiteSpace(candidate.GetFilter()));

        index.Should().NotBeNull($"{entityType.Name}.{indexedPropertyName} must keep its enum-backed filtered unique index");
        index!.GetFilter().Should().Be(expectedFilter);
    }
}
