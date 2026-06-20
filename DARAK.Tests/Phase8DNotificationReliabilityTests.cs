using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Notifications;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using DARAK.Api.Services;
using DARAK.Api.Services.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DARAK.Tests;

public sealed class Phase8DNotificationReliabilityTests
{
    [Fact]
    public async Task MarkForRetryAsync_ResetsFailedNotificationRetryState()
    {
        await using var dbContext = TestDb.Create();
        var compound = new Compound
        {
            Name = "Darak One",
            Code = "D1",
            City = "Baghdad",
            Area = "Karrada"
        };
        var notification = new NotificationOutbox
        {
            Compound = compound,
            Channel = NotificationChannel.Email,
            RecipientName = "Resident",
            RecipientEmail = "resident@example.com",
            Subject = "Failed notice",
            Body = "Retry me.",
            Status = NotificationStatus.Failed,
            RetryCount = 3,
            MaxRetryCount = 3,
            FailedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            LastError = "SMTP failed.",
            NextRetryAtUtc = null
        };

        dbContext.NotificationOutboxes.Add(notification);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, [compound.Id]);

        var result = await service.MarkForRetryAsync(Guid.NewGuid(), notification.Id);

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.Status.Should().Be(NotificationStatus.Pending);
        result.Value.RetryCount.Should().Be(0);
        result.Value.FailedAtUtc.Should().BeNull();
        result.Value.LastError.Should().BeNull();
        result.Value.NextRetryAtUtc.Should().NotBeNull();

        var stored = dbContext.NotificationOutboxes.Single();
        stored.RetryCount.Should().Be(0);
        stored.Status.Should().Be(NotificationStatus.Pending);
    }

    [Fact]
    public async Task MarkForRetryAsync_RejectsNotificationThatIsCurrentlyProcessing()
    {
        await using var dbContext = TestDb.Create();
        var compound = new Compound
        {
            Name = "Darak One",
            Code = "D1",
            City = "Baghdad",
            Area = "Karrada"
        };
        var notification = new NotificationOutbox
        {
            Compound = compound,
            Channel = NotificationChannel.Email,
            RecipientName = "Resident",
            RecipientEmail = "resident@example.com",
            Subject = "Processing notice",
            Body = "Do not retry yet.",
            Status = NotificationStatus.Processing,
            ProcessingStartedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            RetryCount = 1,
            MaxRetryCount = 3
        };

        dbContext.NotificationOutboxes.Add(notification);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, [compound.Id]);

        var result = await service.MarkForRetryAsync(Guid.NewGuid(), notification.Id);

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        dbContext.NotificationOutboxes.Single().Status.Should().Be(NotificationStatus.Processing);
    }

    [Fact]
    public async Task ProcessDueNotificationsAsync_RequeuesAndProcessesTimedOutNotification()
    {
        await using var dbContext = TestDb.Create();
        var notification = new NotificationOutbox
        {
            Channel = NotificationChannel.Email,
            RecipientName = "Resident",
            RecipientEmail = "resident@example.com",
            Subject = "Recovered notice",
            Body = "The previous worker timed out.",
            Status = NotificationStatus.Processing,
            ProcessingStartedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            RetryCount = 1,
            MaxRetryCount = 3
        };

        dbContext.NotificationOutboxes.Add(notification);
        await dbContext.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = CreateService(
            dbContext,
            [],
            isSuperAdmin: true,
            emailSender: emailSender,
            options: new NotificationOptions
            {
                ProcessingTimeoutMinutes = 5,
                RetryDelayMinutes = 5
            });

        var processed = await service.ProcessDueNotificationsAsync(10);

        processed.Should().Be(1);
        emailSender.SentMessages.Should().ContainSingle();

        var stored = dbContext.NotificationOutboxes.Single();
        stored.Status.Should().Be(NotificationStatus.Sent);
        stored.ProcessingStartedAtUtc.Should().BeNull();
        stored.SentAtUtc.Should().NotBeNull();
        dbContext.NotificationDeliveryAttempts.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessDueNotificationsAsync_FailsTimedOutNotificationWhenRetriesAreExhausted()
    {
        await using var dbContext = TestDb.Create();
        var notification = new NotificationOutbox
        {
            Channel = NotificationChannel.Email,
            RecipientName = "Resident",
            RecipientEmail = "resident@example.com",
            Subject = "Exhausted notice",
            Body = "Retries are exhausted.",
            Status = NotificationStatus.Processing,
            ProcessingStartedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            RetryCount = 3,
            MaxRetryCount = 3
        };

        dbContext.NotificationOutboxes.Add(notification);
        await dbContext.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = CreateService(
            dbContext,
            [],
            isSuperAdmin: true,
            emailSender: emailSender,
            options: new NotificationOptions
            {
                ProcessingTimeoutMinutes = 5
            });

        var processed = await service.ProcessDueNotificationsAsync(10);

        processed.Should().Be(0);
        emailSender.SentMessages.Should().BeEmpty();

        var stored = dbContext.NotificationOutboxes.Single();
        stored.Status.Should().Be(NotificationStatus.Failed);
        stored.FailedAtUtc.Should().NotBeNull();
        stored.ProcessingStartedAtUtc.Should().BeNull();
        stored.LastError.Should().Be("Notification processing timed out after exhausting retry attempts.");
    }

    [Fact]
    public async Task ProcessDueNotificationsAsync_UsesExponentialRetryBackoffForProviderFailures()
    {
        await using var dbContext = TestDb.Create();
        var notification = new NotificationOutbox
        {
            Channel = NotificationChannel.Sms,
            RecipientName = "Resident",
            RecipientPhoneNumber = "+9647000000000",
            Subject = "SMS alert",
            Body = "Test SMS.",
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-1),
            MaxRetryCount = 3
        };

        dbContext.NotificationOutboxes.Add(notification);
        await dbContext.SaveChangesAsync();

        var service = CreateService(
            dbContext,
            [],
            isSuperAdmin: true,
            smsSender: new RecordingSmsSender(shouldFail: true),
            options: new NotificationOptions
            {
                RetryDelayMinutes = 5,
                RetryBackoffMultiplier = 3,
                MaxRetryDelayMinutes = 60
            });

        var firstStartedAt = DateTime.UtcNow;
        var firstProcessed = await service.ProcessDueNotificationsAsync(10);

        firstProcessed.Should().Be(1);
        var afterFirstFailure = dbContext.NotificationOutboxes.Single();
        afterFirstFailure.Status.Should().Be(NotificationStatus.Pending);
        afterFirstFailure.RetryCount.Should().Be(1);
        afterFirstFailure.NextRetryAtUtc.Should().NotBeNull();
        afterFirstFailure.NextRetryAtUtc!.Value.Should().BeOnOrAfter(firstStartedAt.AddMinutes(5).AddSeconds(-1));

        afterFirstFailure.NextRetryAtUtc = DateTime.UtcNow.AddMinutes(-1);
        afterFirstFailure.ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();

        var secondStartedAt = DateTime.UtcNow;
        var secondProcessed = await service.ProcessDueNotificationsAsync(10);

        secondProcessed.Should().Be(1);
        var afterSecondFailure = dbContext.NotificationOutboxes.Single();
        afterSecondFailure.Status.Should().Be(NotificationStatus.Pending);
        afterSecondFailure.RetryCount.Should().Be(2);
        afterSecondFailure.NextRetryAtUtc.Should().NotBeNull();
        afterSecondFailure.NextRetryAtUtc!.Value.Should().BeOnOrAfter(secondStartedAt.AddMinutes(15).AddSeconds(-1));
    }

    private static NotificationOutboxService CreateService(
        ApplicationDbContext dbContext,
        Guid[] allowedCompoundIds,
        bool isSuperAdmin = false,
        IEmailSender? emailSender = null,
        ISmsSender? smsSender = null,
        NotificationOptions? options = null)
    {
        return new NotificationOutboxService(
            dbContext,
            new FakeCompoundAccessService(allowedCompoundIds, isSuperAdmin: isSuperAdmin),
            emailSender ?? new RecordingEmailSender(),
            smsSender ?? new RecordingSmsSender(),
            Options.Create(options ?? new NotificationOptions
            {
                RetryDelayMinutes = 5
            }));
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailNotificationMessage> SentMessages { get; } = [];

        public Task<NotificationDeliveryResult> SendAsync(
            EmailNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);
            return Task.FromResult(NotificationDeliveryResult.Success("TestEmail", Guid.NewGuid().ToString()));
        }
    }

    private sealed class RecordingSmsSender(bool shouldFail = false) : ISmsSender
    {
        public List<SmsNotificationMessage> SentMessages { get; } = [];

        public Task<NotificationDeliveryResult> SendAsync(
            SmsNotificationMessage message,
            CancellationToken cancellationToken = default)
        {
            SentMessages.Add(message);

            return Task.FromResult(shouldFail
                ? NotificationDeliveryResult.Failed("TestSms", "SMS provider failed.")
                : NotificationDeliveryResult.Success("TestSms", Guid.NewGuid().ToString()));
        }
    }
}
