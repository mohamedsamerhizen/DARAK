using DARAK.Api.Data;
using DARAK.Api.DTOs.UtilityBills;
using DARAK.Api.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class FinancialConcurrencyModelTests
{
    [Theory]
    [InlineData(typeof(UtilityBill))]
    [InlineData(typeof(RentInvoice))]
    [InlineData(typeof(InstallmentScheduleItem))]
    [InlineData(typeof(ViolationFine))]
    public void FinancialEntities_HaveRowVersionConcurrencyToken(Type entityType)
    {
        using var dbContext = TestDb.Create();

        var entity = dbContext.Model.FindEntityType(entityType);
        entity.Should().NotBeNull();

        var rowVersion = entity!.FindProperty("RowVersion");
        rowVersion.Should().NotBeNull();
        rowVersion!.IsConcurrencyToken.Should().BeTrue();
        rowVersion.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
    }

    [Fact]
    public void Payment_HasStandaloneTargetIdIndex()
    {
        using var dbContext = TestDb.Create();

        var entity = dbContext.Model.FindEntityType(typeof(Payment));
        entity.Should().NotBeNull();

        entity!.GetIndexes()
            .Should()
            .Contain(index => index.Properties.Count == 1
                && index.Properties[0].Name == nameof(Payment.TargetId));
    }

    [Fact]
    public void UpdateUtilityBillRequest_DoesNotExposePreviousBalanceAmount()
    {
        typeof(UpdateUtilityBillRequest)
            .GetProperty("PreviousBalanceAmount")
            .Should()
            .BeNull("previous balance is a calculated financial carry-forward and should not be manually changed through update requests");
    }
}
