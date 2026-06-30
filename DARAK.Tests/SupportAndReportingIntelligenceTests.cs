using DARAK.Api.Data;
using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Reports;
using DARAK.Api.DTOs.Support;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using DARAK.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Tests;

public sealed class SupportAndReportingIntelligenceTests
{
    [Fact]
    public async Task CreateCaseAsync_CreatesCaseEventAndAuditInsideScope()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUnitResidentAsync(dbContext, "P21-SC1");
        var service = CreateSupportService(dbContext, seed.Compound.Id);

        var result = await service.CreateCaseAsync(Guid.NewGuid(), new CreateSupportCaseRequest
        {
            CompoundId = seed.Compound.Id,
            ResidentProfileId = seed.Resident.Id,
            PropertyUnitId = seed.Unit.Id,
            Category = SupportCaseCategory.Billing,
            Priority = SupportCasePriority.High,
            Title = "Payment dispute",
            Description = "Resident says payment was duplicated."
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        dbContext.SupportCases.Should().ContainSingle(item => item.Title == "Payment dispute");
        dbContext.SupportCaseEvents.Should().ContainSingle(item => item.EventType == SupportCaseEventType.Created);
        dbContext.AuditLogEntries.Should().ContainSingle(item => item.ActionType == AuditActionType.SupportCaseCreated);
    }

    [Fact]
    public async Task CreateCaseAsync_RejectsResidentOutsideCompound()
    {
        await using var dbContext = TestDb.Create();
        var compoundA = await AddCompoundAsync(dbContext, "P21-SC2-A");
        var compoundB = await AddCompoundAsync(dbContext, "P21-SC2-B");
        var residentB = await AddResidentAsync(dbContext, compoundB.Id, "Resident B");
        var service = CreateSupportService(dbContext, compoundA.Id);

        var result = await service.CreateCaseAsync(Guid.NewGuid(), new CreateSupportCaseRequest
        {
            CompoundId = compoundA.Id,
            ResidentProfileId = residentB.Id,
            Title = "Cross compound",
            Description = "Should fail."
        });

        result.IsSuccess.Should().BeFalse();
        dbContext.SupportCases.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignEscalateResolveReopenAndNote_WorkflowCreatesEvents()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUnitResidentAsync(dbContext, "P21-SC3");
        var service = CreateSupportService(dbContext, seed.Compound.Id);
        var created = await service.CreateCaseAsync(Guid.NewGuid(), new CreateSupportCaseRequest
        {
            CompoundId = seed.Compound.Id,
            ResidentProfileId = seed.Resident.Id,
            Title = "AC problem",
            Description = "Cooling issue."
        });

        var assignee = await AddUserWithRolesAsync(dbContext, [UserRole.CompoundAdmin], seed.Compound.Id);
        var caseId = created.Value!.Id;
        var assigned = await service.AssignCaseAsync(Guid.NewGuid(), caseId, new AssignSupportCaseRequest
        {
            AssignedToUserId = assignee.Id,
            Note = "Assign to support supervisor."
        });
        var escalated = await service.EscalateCaseAsync(Guid.NewGuid(), caseId, new EscalateSupportCaseRequest
        {
            Priority = SupportCasePriority.Critical,
            Reason = "Resident is angry and issue is urgent."
        });
        var resolved = await service.ResolveCaseAsync(Guid.NewGuid(), caseId, new ResolveSupportCaseRequest
        {
            ResolutionSummary = "Technician resolved it.",
            CloseImmediately = true
        });
        var reopened = await service.ReopenCaseAsync(Guid.NewGuid(), caseId, new ReopenSupportCaseRequest
        {
            Reason = "Resident reported issue again."
        });
        var noted = await service.AddNoteAsync(Guid.NewGuid(), caseId, new AddSupportCaseNoteRequest
        {
            Note = "Follow up tomorrow."
        });

        assigned.Value!.Status.Should().Be(SupportCaseStatus.Assigned);
        escalated.Value!.Priority.Should().Be(SupportCasePriority.Critical);
        resolved.Value!.Status.Should().Be(SupportCaseStatus.Closed);
        reopened.Value!.ReopenCount.Should().Be(1);
        noted.Value!.Events.Should().Contain(item => item.EventType == SupportCaseEventType.NoteAdded);
        dbContext.SupportCaseEvents.Should().HaveCount(6);
        dbContext.AuditLogEntries.Should().Contain(item => item.ActionType == AuditActionType.SupportCaseReopened);
    }

    [Fact]
    public async Task GetDashboardAsync_CountsOpenEscalatedOverdueCriticalAndReopenedCases()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P21-SD1");
        dbContext.SupportCases.AddRange(
            new SupportCase
            {
                CompoundId = compound.Id,
                Title = "Open overdue",
                Description = "Overdue",
                Status = SupportCaseStatus.Open,
                Priority = SupportCasePriority.Critical,
                Category = SupportCaseCategory.Security,
                DueAtUtc = DateTime.UtcNow.AddHours(-2)
            },
            new SupportCase
            {
                CompoundId = compound.Id,
                Title = "Escalated",
                Description = "Escalated",
                Status = SupportCaseStatus.Escalated,
                Priority = SupportCasePriority.High,
                Category = SupportCaseCategory.Maintenance,
                ReopenCount = 1,
                DueAtUtc = DateTime.UtcNow.AddHours(2)
            });
        await dbContext.SaveChangesAsync();
        var service = CreateSupportService(dbContext, compound.Id);

        var result = await service.GetDashboardAsync(new SupportDashboardQuery { CompoundId = compound.Id });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.OpenCount.Should().Be(1);
        result.Value.EscalatedCount.Should().Be(1);
        result.Value.OverdueCount.Should().Be(1);
        result.Value.CriticalCount.Should().Be(1);
        result.Value.ReopenedCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchCasesAsync_RespectsCompoundScope()
    {
        await using var dbContext = TestDb.Create();
        var compoundA = await AddCompoundAsync(dbContext, "P21-SS-A");
        var compoundB = await AddCompoundAsync(dbContext, "P21-SS-B");
        dbContext.SupportCases.AddRange(
            new SupportCase { CompoundId = compoundA.Id, Title = "Visible", Description = "A", DueAtUtc = DateTime.UtcNow.AddDays(1) },
            new SupportCase { CompoundId = compoundB.Id, Title = "Hidden", Description = "B", DueAtUtc = DateTime.UtcNow.AddDays(1) });
        await dbContext.SaveChangesAsync();
        var service = CreateSupportService(dbContext, compoundA.Id);

        var result = await service.SearchCasesAsync(new SupportCaseSearchQuery { PageSize = 20 });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.Items.Should().ContainSingle(item => item.Title == "Visible");
    }

    [Fact]
    public async Task GetFinancialReportAsync_AggregatesBillsPaymentsAndLedger()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedUnitResidentAsync(dbContext, "P21-FR1");
        dbContext.UtilityBills.Add(new UtilityBill
        {
            CompoundId = seed.Compound.Id,
            PropertyUnitId = seed.Unit.Id,
            ResidentProfileId = seed.Resident.Id,
            BillingCycleId = Guid.NewGuid(),
            BillNumber = "UB-P21",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            BillStatus = BillStatus.Unpaid,
            TotalAmount = 1000m
        });
        dbContext.Payments.Add(new Payment
        {
            CompoundId = seed.Compound.Id,
            ResidentProfileId = seed.Resident.Id,
            TargetType = PaymentTargetType.UtilityBill,
            TargetId = Guid.NewGuid(),
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = PaymentStatus.Succeeded,
            PaymentReference = "P21-PAY",
            Amount = 400m
        });
        dbContext.ResidentLedgerEntries.Add(new ResidentLedgerEntry
        {
            CompoundId = seed.Compound.Id,
            ResidentProfileId = seed.Resident.Id,
            Direction = FinancialLedgerEntryDirection.Debit,
            SourceType = FinancialLedgerSourceType.FinancialAdjustment,
            SourceId = Guid.NewGuid(),
            Amount = 200m,
            Reference = "ADJ",
            Description = "Manual debit"
        });
        await dbContext.SaveChangesAsync();
        var service = CreateReportService(dbContext, seed.Compound.Id);

        var result = await service.GetFinancialReportAsync(new ManagementReportQuery { CompoundId = seed.Compound.Id, FromUtc = DateTime.UtcNow.AddDays(-1), ToUtc = DateTime.UtcNow.AddDays(1) });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalBilledAmount.Should().Be(1200m);
        result.Value.TotalCollectedAmount.Should().Be(400m);
        result.Value.OutstandingAmount.Should().Be(800m);
    }

    [Fact]
    public async Task GetOccupancyReportAsync_CalculatesOccupancyRate()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P21-OR1");
        dbContext.PropertyUnits.AddRange(
            new PropertyUnit { CompoundId = compound.Id, UnitNumber = "1", PropertyType = PropertyType.Apartment, UnitStatus = UnitStatus.Occupied, AreaSquareMeters = 90m, Bedrooms = 2, Bathrooms = 1 },
            new PropertyUnit { CompoundId = compound.Id, UnitNumber = "2", PropertyType = PropertyType.Apartment, UnitStatus = UnitStatus.Available, AreaSquareMeters = 90m, Bedrooms = 2, Bathrooms = 1 });
        await dbContext.SaveChangesAsync();
        var service = CreateReportService(dbContext, compound.Id);

        var result = await service.GetOccupancyReportAsync(new ManagementReportQuery { CompoundId = compound.Id });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.TotalUnits.Should().Be(2);
        result.Value.OccupancyRatePercent.Should().Be(50);
    }

    [Fact]
    public async Task GetSupportReportAsync_UsesSupportCaseData()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P21-SR1");
        dbContext.SupportCases.AddRange(
            new SupportCase { CompoundId = compound.Id, Title = "Open", Description = "A", Status = SupportCaseStatus.Open, DueAtUtc = DateTime.UtcNow.AddHours(-1) },
            new SupportCase { CompoundId = compound.Id, Title = "Resolved", Description = "B", Status = SupportCaseStatus.Resolved, ReopenCount = 1, DueAtUtc = DateTime.UtcNow.AddHours(2) });
        await dbContext.SaveChangesAsync();
        var service = CreateReportService(dbContext, compound.Id);

        var result = await service.GetSupportReportAsync(new ManagementReportQuery { CompoundId = compound.Id });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.OpenCases.Should().Be(1);
        result.Value.OverdueCases.Should().Be(1);
        result.Value.ResolvedCases.Should().Be(1);
        result.Value.ReopenedCases.Should().Be(1);
    }

    [Fact]
    public async Task SavedReportAndExportJob_WorkflowsCreateAuditEntries()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P21-RJ1");
        var service = CreateReportService(dbContext, compound.Id);
        var userId = Guid.NewGuid();

        var saved = await service.CreateSavedReportAsync(userId, new CreateSavedReportRequest
        {
            CompoundId = compound.Id,
            ReportType = ManagementReportType.Financial,
            Visibility = SavedReportVisibility.FinanceTeam,
            Name = "Monthly collection",
            FilterJson = "{\"month\":\"2026-06\"}"
        });
        var job = await service.CreateExportJobAsync(userId, new CreateReportExportJobRequest
        {
            CompoundId = compound.Id,
            ReportType = ManagementReportType.Financial,
            Format = ReportExportFormat.Csv,
            FilterJson = "{}"
        });
        var completed = await service.CompleteExportJobAsync(userId, job.Value!.Id, new CompleteReportExportJobRequest
        {
            FileName = "financial.csv",
            DownloadPath = "financial.csv"
        });

        saved.IsSuccess.Should().BeTrue(saved.Message);
        completed.Value!.Status.Should().Be(ReportExportJobStatus.Completed);
        dbContext.ChangeTracker.Clear();
        (await dbContext.AuditLogEntries.CountAsync(item => item.ActionType == AuditActionType.SavedReportCreated)).Should().Be(1);
        dbContext.AuditLogEntries.Should().Contain(item => item.ActionType == AuditActionType.ReportExportCompleted);
    }

    [Fact]
    public async Task CompleteExportJobAsync_RejectsTraversalAndLeavesJobQueued()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P21-RJ-TRAV");
        var service = CreateReportService(dbContext, compound.Id);
        var job = await service.CreateExportJobAsync(Guid.NewGuid(), new CreateReportExportJobRequest
        {
            CompoundId = compound.Id,
            ReportType = ManagementReportType.Financial,
            Format = ReportExportFormat.Csv,
            FilterJson = "{}"
        });

        var result = await service.CompleteExportJobAsync(Guid.NewGuid(), job.Value!.Id, new CompleteReportExportJobRequest
        {
            FileName = "../financial.csv",
            DownloadPath = "financial.csv"
        });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
        dbContext.ReportExportJobs.Single().Status.Should().Be(ReportExportJobStatus.Queued);
        dbContext.ReportExportJobs.Single().FileName.Should().BeNull();
        dbContext.AuditLogEntries.Should().NotContain(item => item.ActionType == AuditActionType.ReportExportCompleted);
    }

    [Fact]
    public async Task CompleteExportJobAsync_StoresSanitizedPathInsideReportExportRoot()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P21-RJ-SAFE");
        var service = CreateReportService(dbContext, compound.Id);
        var job = await service.CreateExportJobAsync(Guid.NewGuid(), new CreateReportExportJobRequest
        {
            CompoundId = compound.Id,
            ReportType = ManagementReportType.Financial,
            Format = ReportExportFormat.Csv,
            FilterJson = "{}"
        });

        var result = await service.CompleteExportJobAsync(Guid.NewGuid(), job.Value!.Id, new CompleteReportExportJobRequest
        {
            FileName = "financial<>summary",
            DownloadPath = "ignored-client-name.csv"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.FileName.Should().Be("financial__summary.csv");
        result.Value.DownloadPath.Should().Be($"App_Data/Exports/Reports/{job.Value.Id:N}/financial__summary.csv");
        result.Value.DownloadPath.Should().NotContain("..");
    }

    [Fact]
    public async Task CreateSavedReportAsync_WithEmptyFilterJson_StoresNormalizedEmptyObject()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P19-RJ-EMPTY");
        var service = CreateReportService(dbContext, compound.Id);

        var result = await service.CreateSavedReportAsync(Guid.NewGuid(), new CreateSavedReportRequest
        {
            CompoundId = compound.Id,
            ReportType = ManagementReportType.Financial,
            Visibility = SavedReportVisibility.Private,
            Name = "Empty filter report",
            FilterJson = "   "
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.FilterJson.Should().Be("{}");
        dbContext.SavedReports.Should().ContainSingle(item => item.FilterJson == "{}");
    }

    [Fact]
    public async Task CreateSavedReportAsync_WithValidFilterJson_NormalizesFilterJson()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P19-RJ-VALID");
        var service = CreateReportService(dbContext, compound.Id);

        var result = await service.CreateSavedReportAsync(Guid.NewGuid(), new CreateSavedReportRequest
        {
            CompoundId = compound.Id,
            ReportType = ManagementReportType.Financial,
            Visibility = SavedReportVisibility.Private,
            Name = "Valid filter report",
            FilterJson = "{ \"month\" : \"2026-06\" }"
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.FilterJson.Should().Be("{\"month\":\"2026-06\"}");
    }

    [Fact]
    public async Task CreateSavedReportAsync_WithInvalidFilterJson_ReturnsBadRequestAndDoesNotPersist()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P19-RJ-BAD");
        var service = CreateReportService(dbContext, compound.Id);

        var result = await service.CreateSavedReportAsync(Guid.NewGuid(), new CreateSavedReportRequest
        {
            CompoundId = compound.Id,
            ReportType = ManagementReportType.Financial,
            Visibility = SavedReportVisibility.Private,
            Name = "Invalid filter report",
            FilterJson = "{ invalid-json"
        });

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Report filter JSON must be valid JSON.");
        dbContext.SavedReports.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateExportJobAsync_WithInvalidFilterJson_ReturnsBadRequestAndDoesNotPersist()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P19-RJ-EXPORT-BAD");
        var service = CreateReportService(dbContext, compound.Id);

        var result = await service.CreateExportJobAsync(Guid.NewGuid(), new CreateReportExportJobRequest
        {
            CompoundId = compound.Id,
            ReportType = ManagementReportType.Financial,
            Format = ReportExportFormat.Csv,
            FilterJson = "{ invalid-json"
        });

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Report filter JSON must be valid JSON.");
        dbContext.ReportExportJobs.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateSavedReportAsync_WithOversizedFilterJson_ReturnsBadRequestAndDoesNotTruncate()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P19-RJ-LONG");
        var service = CreateReportService(dbContext, compound.Id);
        var oversizedFilter = "{\"value\":\"" + new string('x', 4000) + "\"}";

        var result = await service.CreateSavedReportAsync(Guid.NewGuid(), new CreateSavedReportRequest
        {
            CompoundId = compound.Id,
            ReportType = ManagementReportType.Financial,
            Visibility = SavedReportVisibility.Private,
            Name = "Oversized filter report",
            FilterJson = oversizedFilter
        });

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("Report filter JSON must not exceed 4000 characters.");
        dbContext.SavedReports.Should().BeEmpty();
    }

    [Fact]
    public async Task RiskAuditReportAsync_AggregatesRiskFlagsAndAuditEvents()
    {
        await using var dbContext = TestDb.Create();
        var compound = await AddCompoundAsync(dbContext, "P21-RA1");
        dbContext.ResidentRiskFlags.Add(new ResidentRiskFlag
        {
            CompoundId = compound.Id,
            ResidentProfileId = Guid.NewGuid(),
            FlagType = ResidentRiskFlagType.PaymentInstability,
            Severity = ResidentRiskFlagSeverity.Critical,
            Source = ResidentRiskFlagSource.Manual,
            Status = ResidentRiskFlagStatus.Active,
            Title = "High debt",
            Description = "Critical financial risk",
            CreatedByUserId = Guid.NewGuid(),
            InternalNotes = "Many overdue bills",
            NextReviewAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            CompoundId = compound.Id,
            ActionType = AuditActionType.AccessDenied,
            EntityType = AuditEntityType.User,
            Severity = AuditSeverity.Critical,
            SourceModule = "Security",
            Description = "Access denied"
        });
        await dbContext.SaveChangesAsync();
        var service = CreateReportService(dbContext, compound.Id);

        var result = await service.GetRiskAuditReportAsync(new ManagementReportQuery { CompoundId = compound.Id, FromUtc = DateTime.UtcNow.AddDays(-1), ToUtc = DateTime.UtcNow.AddDays(1) });

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Value!.ActiveRiskFlags.Should().Be(1);
        result.Value.CriticalRiskFlags.Should().Be(1);
        result.Value.OverdueRiskReviews.Should().Be(1);
        result.Value.CriticalAuditEvents.Should().Be(1);
    }

    private static SupportCaseService CreateSupportService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        var compoundAccess = new FakeCompoundAccessService(allowedCompoundIds);
        return new SupportCaseService(
            dbContext,
            compoundAccess,
            new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor()));
    }

    private static ManagementReportService CreateReportService(ApplicationDbContext dbContext, params Guid[] allowedCompoundIds)
    {
        var compoundAccess = new FakeCompoundAccessService(allowedCompoundIds);
        return new ManagementReportService(
            dbContext,
            compoundAccess,
            new AuditLogService(dbContext, compoundAccess, new HttpContextAccessor()));
    }

    private static async Task<Compound> AddCompoundAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = new Compound
        {
            Name = $"Compound {code}",
            Code = code,
            City = "Baghdad",
            Area = "Karrada"
        };
        dbContext.Compounds.Add(compound);
        await dbContext.SaveChangesAsync();
        return compound;
    }

    private static async Task<PropertyUnit> AddUnitAsync(ApplicationDbContext dbContext, Guid compoundId, string unitNumber)
    {
        var unit = new PropertyUnit
        {
            CompoundId = compoundId,
            UnitNumber = unitNumber,
            PropertyType = PropertyType.Apartment,
            UnitStatus = UnitStatus.Available,
            AreaSquareMeters = 100m,
            Bedrooms = 2,
            Bathrooms = 1
        };
        dbContext.PropertyUnits.Add(unit);
        await dbContext.SaveChangesAsync();
        return unit;
    }

    private static async Task<ResidentProfile> AddResidentAsync(ApplicationDbContext dbContext, Guid compoundId, string fullName)
    {
        var resident = new ResidentProfile
        {
            CompoundId = compoundId,
            UserId = Guid.NewGuid(),
            FullName = fullName,
            PhoneNumber = "07700000000"
        };
        dbContext.ResidentProfiles.Add(resident);
        await dbContext.SaveChangesAsync();
        return resident;
    }

    private static async Task<ApplicationUser> AddUserWithRolesAsync(
        ApplicationDbContext dbContext,
        IReadOnlyCollection<UserRole> roles,
        Guid? compoundId = null)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = $"support-{Guid.NewGuid():N}@darak.test",
            UserName = $"support-{Guid.NewGuid():N}@darak.test",
            FullName = "Support Assignee",
            EmailConfirmed = true
        };
        dbContext.Users.Add(user);

        foreach (var role in roles.Distinct())
        {
            var roleName = role.ToString();
            var identityRole = dbContext.Roles.Local.FirstOrDefault(item => item.Name == roleName)
                ?? await dbContext.Roles.FirstOrDefaultAsync(item => item.Name == roleName);
            if (identityRole is null)
            {
                identityRole = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                };
                dbContext.Roles.Add(identityRole);
            }

            dbContext.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = user.Id,
                RoleId = identityRole.Id
            });

            if (compoundId.HasValue && role is not UserRole.SuperAdmin and not UserRole.Resident)
            {
                dbContext.UserCompoundAssignments.Add(new UserCompoundAssignment
                {
                    UserId = user.Id,
                    CompoundId = compoundId.Value,
                    Role = role,
                    IsActive = true
                });
            }
        }

        await dbContext.SaveChangesAsync();
        return user;
    }

    private static async Task<(Compound Compound, PropertyUnit Unit, ResidentProfile Resident)> SeedUnitResidentAsync(ApplicationDbContext dbContext, string code)
    {
        var compound = await AddCompoundAsync(dbContext, code);
        var unit = await AddUnitAsync(dbContext, compound.Id, $"U-{code}");
        var resident = await AddResidentAsync(dbContext, compound.Id, $"Resident {code}");
        return (compound, unit, resident);
    }
}
