using DARAK.Api.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DARAK.Tests;

public sealed class Phase3BDatabaseIntegrityAndOutboxTests
{
    [Fact]
    public void PaymentModel_HasCompositeIndexesForStatusAndLedgerLookups()
    {
        using var dbContext = TestDb.Create();

        var entity = dbContext.Model.FindEntityType(typeof(Payment));
        entity.Should().NotBeNull();

        HasIndex(entity!, nameof(Payment.CompoundId), nameof(Payment.PaymentStatus), nameof(Payment.CreatedAt))
            .Should().BeTrue();

        HasIndex(entity!, nameof(Payment.ResidentProfileId), nameof(Payment.PaymentStatus), nameof(Payment.CreatedAt))
            .Should().BeTrue();

        HasIndex(entity!, nameof(Payment.TargetType), nameof(Payment.TargetId), nameof(Payment.PaymentStatus))
            .Should().BeTrue();
    }

    [Fact]
    public void PaymentAttemptModel_HasUniqueProviderTransactionIndex()
    {
        using var dbContext = TestDb.Create();

        var entity = dbContext.Model.FindEntityType(typeof(PaymentAttempt));
        entity.Should().NotBeNull();

        var index = entity!.GetIndexes().SingleOrDefault(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(PaymentAttempt.Provider),
                nameof(PaymentAttempt.ProviderTransactionId)
            }));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("[ProviderTransactionId] IS NOT NULL");
    }

    [Fact]
    public void ActivityEventModel_HasTimelineQueryIndexes()
    {
        using var dbContext = TestDb.Create();

        var entity = dbContext.Model.FindEntityType(typeof(ActivityEvent));
        entity.Should().NotBeNull();

        HasIndex(entity!, nameof(ActivityEvent.CompoundId), nameof(ActivityEvent.CreatedAtUtc))
            .Should().BeTrue();

        HasIndex(entity!, nameof(ActivityEvent.ResidentProfileId), nameof(ActivityEvent.CreatedAtUtc))
            .Should().BeTrue();

        HasIndex(entity!, nameof(ActivityEvent.EntityType), nameof(ActivityEvent.EntityId), nameof(ActivityEvent.CreatedAtUtc))
            .Should().BeTrue();
    }

    [Fact]
    public void NotificationOutboxModel_HasAtomicClaimIndexes()
    {
        using var dbContext = TestDb.Create();

        var entity = dbContext.Model.FindEntityType(typeof(NotificationOutbox));
        entity.Should().NotBeNull();

        HasIndex(entity!, nameof(NotificationOutbox.Status), nameof(NotificationOutbox.ScheduledAtUtc), nameof(NotificationOutbox.Priority))
            .Should().BeTrue();

        HasIndex(entity!, nameof(NotificationOutbox.Status), nameof(NotificationOutbox.NextRetryAtUtc))
            .Should().BeTrue();
    }

    [Fact]
    public void ViolationFineModel_HasDuplicateAndDashboardIndexes()
    {
        using var dbContext = TestDb.Create();

        var entity = dbContext.Model.FindEntityType(typeof(ViolationFine));
        entity.Should().NotBeNull();

        var duplicateIndex = entity!.GetIndexes().SingleOrDefault(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(ViolationFine.ViolationId) }));

        duplicateIndex.Should().NotBeNull();
        duplicateIndex!.IsUnique.Should().BeTrue();
        duplicateIndex.GetFilter().Should().Be("[Status] <> 3");

        HasIndex(entity!, nameof(ViolationFine.CompoundId), nameof(ViolationFine.ResidentProfileId), nameof(ViolationFine.Status), nameof(ViolationFine.DueDate))
            .Should().BeTrue();
    }

    private static bool HasIndex(IEntityType entity, params string[] propertyNames)
    {
        return entity.GetIndexes().Any(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
    }
}
