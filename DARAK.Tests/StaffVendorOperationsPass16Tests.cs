using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class StaffVendorOperationsPass16Tests
{
    [Fact]
    public async Task Pass16_SearchStaffMembersAsync_RedactsSensitivePersonnelFieldsFromList()
    {
        await using var dbContext = TestDb.Create();
        var staff = new StaffMember
        {
            FullName = "Operations Supervisor",
            PhoneNumber = "07700000001",
            Email = "ops.supervisor@darak.test",
            StaffType = StaffType.Supervisor,
            Status = StaffStatus.Active,
            Specialization = "Compound operations",
            NationalId = "NAT-P16-001",
            Notes = "Internal HR note for admins only."
        };
        dbContext.StaffMembers.Add(staff);
        await dbContext.SaveChangesAsync();
        var service = new StaffMemberService(dbContext);

        var searchResult = await service.SearchStaffMembersAsync(new StaffMemberQueryRequest());

        var listItem = searchResult.Items.Single();
        listItem.NationalId.Should().BeNull();
        listItem.Notes.Should().BeNull();
        listItem.FullName.Should().Be(staff.FullName);
        listItem.PhoneNumber.Should().Be(staff.PhoneNumber);

        var detailResult = await service.GetStaffMemberAsync(staff.Id);
        detailResult.Status.Should().Be(ServiceResultStatus.Success);
        detailResult.Value!.NationalId.Should().Be(staff.NationalId);
        detailResult.Value.Notes.Should().Be(staff.Notes);
    }

    [Fact]
    public async Task Pass16_CreateStaffMemberAsync_RejectsDuplicateLinkedUser()
    {
        await using var dbContext = TestDb.Create();
        var user = AddUser("p16-user-create@darak.test");
        dbContext.Users.Add(user);
        dbContext.StaffMembers.Add(new StaffMember
        {
            FullName = "Existing Technician",
            PhoneNumber = "07700000002",
            StaffType = StaffType.MaintenanceTechnician,
            UserId = user.Id
        });
        await dbContext.SaveChangesAsync();
        var service = new StaffMemberService(dbContext);

        var result = await service.CreateStaffMemberAsync(new CreateStaffMemberRequest
        {
            FullName = "Duplicate Technician",
            PhoneNumber = "07700000003",
            StaffType = StaffType.MaintenanceTechnician,
            UserId = user.Id
        });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
        result.Message.Should().Contain("already assigned");
        dbContext.StaffMembers.Count().Should().Be(1);
    }

    [Fact]
    public async Task Pass16_UpdateStaffMemberAsync_RejectsDuplicateLinkedUserButAllowsKeepingOwnUser()
    {
        await using var dbContext = TestDb.Create();
        var firstUser = AddUser("p16-user-first@darak.test");
        var secondUser = AddUser("p16-user-second@darak.test");
        var firstStaff = new StaffMember
        {
            FullName = "First Technician",
            PhoneNumber = "07700000004",
            StaffType = StaffType.MaintenanceTechnician,
            UserId = firstUser.Id
        };
        var secondStaff = new StaffMember
        {
            FullName = "Second Technician",
            PhoneNumber = "07700000005",
            StaffType = StaffType.SecurityGuard,
            UserId = secondUser.Id
        };
        dbContext.Users.AddRange(firstUser, secondUser);
        dbContext.StaffMembers.AddRange(firstStaff, secondStaff);
        await dbContext.SaveChangesAsync();
        var service = new StaffMemberService(dbContext);

        var keepOwnUserResult = await service.UpdateStaffMemberAsync(firstStaff.Id, new UpdateStaffMemberRequest
        {
            FullName = "First Technician Updated",
            PhoneNumber = "07700000004",
            StaffType = StaffType.MaintenanceTechnician,
            Status = StaffStatus.Active,
            UserId = firstUser.Id
        });

        keepOwnUserResult.Status.Should().Be(ServiceResultStatus.Success);

        var duplicateUserResult = await service.UpdateStaffMemberAsync(secondStaff.Id, new UpdateStaffMemberRequest
        {
            FullName = "Second Technician Updated",
            PhoneNumber = "07700000005",
            StaffType = StaffType.SecurityGuard,
            Status = StaffStatus.Active,
            UserId = firstUser.Id
        });

        duplicateUserResult.Status.Should().Be(ServiceResultStatus.Conflict);
        duplicateUserResult.Message.Should().Contain("already assigned");
        dbContext.StaffMembers.Single(item => item.Id == secondStaff.Id).UserId.Should().Be(secondUser.Id);
    }

    private static ApplicationUser AddUser(string email)
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            FullName = email.Split('@')[0]
        };
    }
}
