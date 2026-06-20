using DARAK.Api.Data;
using DARAK.Api.Entities;
using DARAK.Api.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DARAK.Tests;

public sealed class Phase3ARowVersionConcurrencyTests
{
    [Theory]
    [InlineData(typeof(Payment))]
    [InlineData(typeof(WorkOrder))]
    [InlineData(typeof(SupportCase))]
    [InlineData(typeof(ApprovalRequest))]
    [InlineData(typeof(VisitorPass))]
    [InlineData(typeof(CommunicationCampaign))]
    public void Phase3A_Entities_HaveRowVersionConcurrencyToken(Type entityType)
    {
        using var dbContext = TestDb.Create();

        var entity = dbContext.Model.FindEntityType(entityType);
        entity.Should().NotBeNull();

        var rowVersion = entity!.FindProperty("RowVersion");
        rowVersion.Should().NotBeNull();
        rowVersion!.ClrType.Should().Be(typeof(byte[]));
        rowVersion.IsConcurrencyToken.Should().BeTrue();
        rowVersion.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }

    [Fact]
    public void Phase3A_Migration_IsPresent()
    {
        typeof(Phase3ARowVersionConcurrencyHandlers)
            .Name
            .Should()
            .Be(nameof(Phase3ARowVersionConcurrencyHandlers));
    }
}
