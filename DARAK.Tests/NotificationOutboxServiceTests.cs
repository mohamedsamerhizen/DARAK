using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Notifications;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using DARAK.Api.Services.Notifications;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DARAK.Tests;

public sealed class NotificationOutboxServiceTests
{
    [Fact]
    public async Task EnqueueManualAsync_CreatesPendingEmailNotification()
    {
        await using var dbContext = TestDb.Create();
        var compound = new Compound
        {
            Name = "Darak One",
            Code = "D1",
            City = "Baghdad",
            Area = "Karrada"
        };

        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, [compound.Id]);
        var adminId = Guid.NewGuid();

        var result = await service.EnqueueManualAsync(
            adminId,
            new ManualNotificationRequest
            {
                CompoundId = compound.Id,
                Channel = NotificationChannel.Email,
                RecipientName = "Resident",
                RecipientEmail = "resident@example.com",
                Subject = "Payment received",
                Body = "Your payment was received."
            });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(NotificationStatus.Pending);
        result.Value.Channel.Should().Be(NotificationChannel.Email);
        result.Value.CreatedByUserId.Should().Be(adminId);

        dbContext.NotificationOutboxes.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_AppliesCompoundScope()
    {
        await using var dbContext = TestDb.Create();

        var allowedCompound = new Compound
        {
            Name = "Allowed",
            Code = "ALW",
            City = "Baghdad",
            Area = "Area 1"
        };
        var blockedCompound = new Compound
        {
            Name = "Blocked",
            Code = "BLK",
            City = "Baghdad",
            Area = "Area 2"
        };

        dbContext.Compounds.AddRange(allowedCompound, blockedCompound);
        dbContext.NotificationOutboxes.AddRange(
            new NotificationOutbox
            {
                CompoundId = allowedCompound.Id,
                Channel = NotificationChannel.InApp,
                Subject = "Allowed",
                Body = "Allowed body"
            },
            new NotificationOutbox
            {
                CompoundId = blockedCompound.Id,
                Channel = NotificationChannel.InApp,
                Subject = "Blocked",
                Body = "Blocked body"
            });

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, [allowedCompound.Id]);

        var result = await service.SearchAsync(Guid.NewGuid(), new NotificationSearchQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().ContainSingle();
        result.Value.Items.Single().Subject.Should().Be("Allowed");
    }

    [Fact]
    public async Task ProcessDueNotificationsAsync_SendsDueNotificationsAndRecordsAttempt()
    {
        await using var dbContext = TestDb.Create();

        dbContext.NotificationOutboxes.Add(new NotificationOutbox
        {
            Channel = NotificationChannel.Email,
            RecipientName = "Resident",
            RecipientEmail = "resident@example.com",
            Subject = "Bill created",
            Body = "Your bill is ready.",
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-1),
            MaxRetryCount = 3
        });

        await dbContext.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = CreateService(dbContext, [], isSuperAdmin: true, emailSender: emailSender);

        var processed = await service.ProcessDueNotificationsAsync(10);

        processed.Should().Be(1);
        emailSender.SentMessages.Should().ContainSingle();

        var notification = dbContext.NotificationOutboxes.Single();
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.SentAtUtc.Should().NotBeNull();

        dbContext.NotificationDeliveryAttempts.Should().ContainSingle();
        dbContext.NotificationDeliveryAttempts.Single().Status.Should().Be(NotificationDeliveryAttemptStatus.Succeeded);
    }

    [Fact]
    public async Task ProcessDueNotificationsAsync_MarksSmsAsFailedWhenProviderFails()
    {
        await using var dbContext = TestDb.Create();

        dbContext.NotificationOutboxes.Add(new NotificationOutbox
        {
            Channel = NotificationChannel.Sms,
            RecipientName = "Resident",
            RecipientPhoneNumber = "+9647000000000",
            Subject = "Alert",
            Body = "Test SMS",
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-1),
            MaxRetryCount = 1
        });

        await dbContext.SaveChangesAsync();

        var smsSender = new RecordingSmsSender(shouldFail: true);
        var service = CreateService(dbContext, [], isSuperAdmin: true, smsSender: smsSender);

        var processed = await service.ProcessDueNotificationsAsync(10);

        processed.Should().Be(1);
        smsSender.SentMessages.Should().ContainSingle();

        var notification = dbContext.NotificationOutboxes.Single();
        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.LastError.Should().Be("SMS provider failed.");

        dbContext.NotificationDeliveryAttempts.Single().Status.Should().Be(NotificationDeliveryAttemptStatus.Failed);
    }


    [Fact]
    public async Task EnqueueManualAsync_WithResidentProfileAndNullCompound_PersistsResolvedCompound()
    {
        await using var dbContext = TestDb.Create();
        var compound = new Compound
        {
            Name = "Resident Compound",
            Code = "RC1",
            City = "Baghdad",
            Area = "Karrada"
        };

        var residentUser = CreateUser("resident-scope@darak.test", "Scoped Resident");
        var resident = new ResidentProfile
        {
            CompoundId = compound.Id,
            UserId = residentUser.Id,
            FullName = "Scoped Resident",
            PhoneNumber = "+9647000000001"
        };

        dbContext.Compounds.Add(compound);
        dbContext.Users.Add(residentUser);
        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, [compound.Id]);

        var result = await service.EnqueueManualAsync(
            Guid.NewGuid(),
            new ManualNotificationRequest
            {
                ResidentProfileId = resident.Id,
                Channel = NotificationChannel.InApp,
                Subject = "Maintenance update",
                Body = "Your request has been updated."
            });

        result.IsSuccess.Should().BeTrue();
        result.Value!.CompoundId.Should().Be(compound.Id);
        result.Value.ResidentProfileId.Should().Be(resident.Id);
        result.Value.RecipientUserId.Should().Be(residentUser.Id);
        result.Value.RecipientName.Should().Be("Scoped Resident");
        result.Value.RecipientPhoneNumber.Should().Be("+9647000000001");

        var notification = dbContext.NotificationOutboxes.Single();
        notification.CompoundId.Should().Be(compound.Id);
        notification.ResidentProfileId.Should().Be(resident.Id);
        notification.RecipientUserId.Should().Be(residentUser.Id);
    }

    [Fact]
    public async Task EnqueueManualAsync_WithResidentProfileAndMismatchedRecipientUser_ReturnsBadRequest()
    {
        await using var dbContext = TestDb.Create();
        var compound = new Compound
        {
            Name = "Mismatch Compound",
            Code = "MC1",
            City = "Baghdad",
            Area = "Mansour"
        };

        var residentUser = CreateUser("resident-owner@darak.test", "Resident Owner");
        var otherUser = CreateUser("other-recipient@darak.test", "Other Recipient");
        var resident = new ResidentProfile
        {
            CompoundId = compound.Id,
            UserId = residentUser.Id,
            FullName = "Resident Owner"
        };

        dbContext.Compounds.Add(compound);
        dbContext.Users.AddRange(residentUser, otherUser);
        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, [compound.Id]);

        var result = await service.EnqueueManualAsync(
            Guid.NewGuid(),
            new ManualNotificationRequest
            {
                ResidentProfileId = resident.Id,
                RecipientUserId = otherUser.Id,
                Channel = NotificationChannel.InApp,
                Subject = "Invalid target",
                Body = "This should be rejected."
            });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        result.Message.Should().Be("Recipient user does not match the selected resident profile.");
        dbContext.NotificationOutboxes.Should().BeEmpty();
    }

    [Fact]
    public async Task EnqueueManualAsync_WithResidentProfileOutsideAllowedScope_ReturnsForbidden()
    {
        await using var dbContext = TestDb.Create();
        var allowedCompound = new Compound
        {
            Name = "Allowed Communications",
            Code = "AC1",
            City = "Baghdad",
            Area = "Area A"
        };
        var blockedCompound = new Compound
        {
            Name = "Blocked Communications",
            Code = "BC1",
            City = "Baghdad",
            Area = "Area B"
        };

        var blockedUser = CreateUser("blocked-resident@darak.test", "Blocked Resident");
        var blockedResident = new ResidentProfile
        {
            CompoundId = blockedCompound.Id,
            UserId = blockedUser.Id,
            FullName = "Blocked Resident"
        };

        dbContext.Compounds.AddRange(allowedCompound, blockedCompound);
        dbContext.Users.Add(blockedUser);
        dbContext.ResidentProfiles.Add(blockedResident);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, [allowedCompound.Id]);

        var result = await service.EnqueueManualAsync(
            Guid.NewGuid(),
            new ManualNotificationRequest
            {
                ResidentProfileId = blockedResident.Id,
                Channel = NotificationChannel.InApp,
                Subject = "Cross compound",
                Body = "This should be blocked."
            });

        result.Status.Should().Be(ServiceResultStatus.Forbidden);
        dbContext.NotificationOutboxes.Should().BeEmpty();
    }

    private static NotificationOutboxService CreateService(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        Guid[] allowedCompoundIds,
        bool isSuperAdmin = false,
        IEmailSender? emailSender = null,
        ISmsSender? smsSender = null)
    {
        return new NotificationOutboxService(
            dbContext,
            new FakeCompoundAccessService(allowedCompoundIds, isSuperAdmin: isSuperAdmin),
            emailSender ?? new RecordingEmailSender(),
            smsSender ?? new RecordingSmsSender(),
            Options.Create(new NotificationOptions
            {
                RetryDelayMinutes = 5
            }));
    }


    private static ApplicationUser CreateUser(string email, string fullName)
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = fullName,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
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
