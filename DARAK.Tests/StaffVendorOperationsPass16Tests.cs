using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Operations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DARAK.Tests;

public sealed class StaffVendorOperationsPass16Tests
{
    [Fact]
    public async Task Pass16_SearchStaffMembersAsync_RedactsSensitivePersonnelFieldsFromList()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P16-RED");
        var staff = new StaffMember
        {
            CompoundId = compound.Id,
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
        var compound = await AddCompoundAsync(dbContext, "P16-CRE");
        var user = AddUser("p16-user-create@darak.test");
        dbContext.Users.Add(user);
        dbContext.StaffMembers.Add(new StaffMember
        {
            CompoundId = compound.Id,
            FullName = "Existing Technician",
            PhoneNumber = "07700000002",
            StaffType = StaffType.MaintenanceTechnician,
            UserId = user.Id
        });
        await dbContext.SaveChangesAsync();
        var service = new StaffMemberService(dbContext);

        var result = await service.CreateStaffMemberAsync(new CreateStaffMemberRequest
        {
            CompoundId = compound.Id,
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
        var compound = await AddCompoundAsync(dbContext, "P16-UPD");
        var firstUser = AddUser("p16-user-first@darak.test");
        var secondUser = AddUser("p16-user-second@darak.test");
        var firstStaff = new StaffMember
        {
            CompoundId = compound.Id,
            FullName = "First Technician",
            PhoneNumber = "07700000004",
            StaffType = StaffType.MaintenanceTechnician,
            UserId = firstUser.Id
        };
        var secondStaff = new StaffMember
        {
            CompoundId = compound.Id,
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
            CompoundId = compound.Id,
            FullName = "First Technician Updated",
            PhoneNumber = "07700000004",
            StaffType = StaffType.MaintenanceTechnician,
            Status = StaffStatus.Active,
            UserId = firstUser.Id
        });

        keepOwnUserResult.Status.Should().Be(ServiceResultStatus.Success);

        var duplicateUserResult = await service.UpdateStaffMemberAsync(secondStaff.Id, new UpdateStaffMemberRequest
        {
            CompoundId = compound.Id,
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

    [Fact]
    public async Task Phase3_StaffMembers_AreScopedByCompoundAccess()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P3-ST-A");
        var denied = await AddCompoundAsync(dbContext, "P3-ST-D");
        dbContext.StaffMembers.AddRange(
            new StaffMember
            {
                CompoundId = allowed.Id,
                FullName = "Allowed Technician",
                PhoneNumber = "07700000100",
                StaffType = StaffType.MaintenanceTechnician,
                Status = StaffStatus.Active
            },
            new StaffMember
            {
                CompoundId = denied.Id,
                FullName = "Denied Technician",
                PhoneNumber = "07700000101",
                StaffType = StaffType.SecurityGuard,
                Status = StaffStatus.Active
            });
        await dbContext.SaveChangesAsync();
        var service = new StaffMemberService(dbContext, new FakeCompoundAccessService([allowed.Id]));

        var search = await service.SearchStaffMembersAsync(new StaffMemberQueryRequest());
        var createDenied = await service.CreateStaffMemberAsync(new CreateStaffMemberRequest
        {
            CompoundId = denied.Id,
            FullName = "Cross Compound Technician",
            PhoneNumber = "07700000102",
            StaffType = StaffType.MaintenanceTechnician
        });

        search.Items.Should().ContainSingle(item => item.CompoundId == allowed.Id);
        createDenied.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    [Fact]
    public async Task Phase3_ServiceVendors_AreScopedByCompoundAccess()
    {
        await using var dbContext = TestDb.Create();
        var allowed = await AddCompoundAsync(dbContext, "P3-SV-A");
        var denied = await AddCompoundAsync(dbContext, "P3-SV-D");
        dbContext.ServiceVendors.AddRange(
            new ServiceVendor
            {
                CompoundId = allowed.Id,
                Name = "Allowed Vendor",
                PhoneNumber = "07700000200",
                ServiceType = VendorServiceType.Maintenance,
                Status = VendorStatus.Active
            },
            new ServiceVendor
            {
                CompoundId = denied.Id,
                Name = "Denied Vendor",
                PhoneNumber = "07700000201",
                ServiceType = VendorServiceType.Security,
                Status = VendorStatus.Active
            });
        await dbContext.SaveChangesAsync();
        var service = new ServiceVendorService(dbContext, new FakeCompoundAccessService([allowed.Id]));

        var search = await service.SearchServiceVendorsAsync(new ServiceVendorQueryRequest());
        var createDenied = await service.CreateServiceVendorAsync(new CreateServiceVendorRequest
        {
            CompoundId = denied.Id,
            Name = "Cross Compound Vendor",
            PhoneNumber = "07700000202",
            ServiceType = VendorServiceType.Maintenance
        });

        search.Items.Should().ContainSingle(item => item.CompoundId == allowed.Id);
        createDenied.Status.Should().Be(ServiceResultStatus.Forbidden);
    }

    [Fact]
    public async Task StaffAndVendorManagementActions_WriteAuditEntries()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P16-AUD");
        var compoundAccess = new FakeCompoundAccessService([compound.Id]);
        var auditLogService = new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor());
        var staffService = new StaffMemberService(dbContext, compoundAccess, auditLogService);
        var vendorService = new ServiceVendorService(dbContext, compoundAccess, auditLogService);

        var staff = await staffService.CreateStaffMemberAsync(new CreateStaffMemberRequest
        {
            CompoundId = compound.Id,
            FullName = "Audited Technician",
            PhoneNumber = "07700000300",
            StaffType = StaffType.MaintenanceTechnician
        });
        var vendor = await vendorService.CreateServiceVendorAsync(new CreateServiceVendorRequest
        {
            CompoundId = compound.Id,
            Name = "Audited Vendor",
            PhoneNumber = "07700000301",
            ServiceType = VendorServiceType.Maintenance
        });

        staff.IsSuccess.Should().BeTrue(staff.Message);
        vendor.IsSuccess.Should().BeTrue(vendor.Message);
        dbContext.AuditLogEntries.Should().Contain(item => item.ActionType == AuditActionType.StaffMemberChanged);
        dbContext.AuditLogEntries.Should().Contain(item => item.ActionType == AuditActionType.ServiceVendorChanged);
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
