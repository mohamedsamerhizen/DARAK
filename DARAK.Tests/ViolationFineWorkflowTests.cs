using DARAK.Api.DTOs.Common;
using DARAK.Api.DTOs.Violations;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Services;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class ViolationFineWorkflowTests
{
    [Fact]
    public async Task CreateViolationFineAsync_CreatesFineForViolationInAssignedCompound()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedViolationAsync(dbContext);
        var service = new ComplaintViolationService(
            dbContext,
            new FakeCompoundAccessService(new[] { seed.CompoundId }));

        var result = await service.CreateViolationFineAsync(new CreateViolationFineRequest
        {
            ViolationId = seed.ViolationId,
            Amount = 75m,
            Reason = "Noise after midnight",
            DueDate = new DateOnly(2026, 10, 1)
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.CompoundId.Should().Be(seed.CompoundId);
        result.Value.ResidentProfileId.Should().Be(seed.ResidentProfileId);
        result.Value.Amount.Should().Be(75m);
        result.Value.Status.Should().Be(ViolationFineStatus.Unpaid);
    }

    [Fact]
    public async Task CreateViolationFineAsync_RejectsDuplicateNonCancelledFineForSameViolation()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedViolationAsync(dbContext);
        dbContext.ViolationFines.Add(CreateFine(seed, ViolationFineStatus.Unpaid, paidAmount: 0m));
        await dbContext.SaveChangesAsync();
        var service = new ComplaintViolationService(
            dbContext,
            new FakeCompoundAccessService(new[] { seed.CompoundId }));

        var result = await service.CreateViolationFineAsync(new CreateViolationFineRequest
        {
            ViolationId = seed.ViolationId,
            Amount = 25m,
            Reason = "Duplicate",
            DueDate = new DateOnly(2026, 10, 1)
        });

        result.Status.Should().Be(ServiceResultStatus.Conflict);
    }

    [Fact]
    public async Task CreateViolationFineAsync_AllowsNewFineAfterPreviousFineWasCancelled()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedViolationAsync(dbContext);
        dbContext.ViolationFines.Add(CreateFine(seed, ViolationFineStatus.Cancelled, paidAmount: 0m));
        await dbContext.SaveChangesAsync();
        var service = new ComplaintViolationService(
            dbContext,
            new FakeCompoundAccessService(new[] { seed.CompoundId }));

        var result = await service.CreateViolationFineAsync(new CreateViolationFineRequest
        {
            ViolationId = seed.ViolationId,
            Amount = 25m,
            Reason = "Replacement fine",
            DueDate = new DateOnly(2026, 10, 1)
        });

        result.Status.Should().Be(ServiceResultStatus.Success);
    }

    [Fact]
    public async Task CancelViolationFineAsync_RejectsFineWithPaidAmount()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedViolationAsync(dbContext);
        var fine = CreateFine(seed, ViolationFineStatus.PartiallyPaid, paidAmount: 10m);
        dbContext.ViolationFines.Add(fine);
        await dbContext.SaveChangesAsync();
        var service = new ComplaintViolationService(
            dbContext,
            new FakeCompoundAccessService(new[] { seed.CompoundId }));

        var result = await service.CancelViolationFineAsync(
            fine.Id,
            new CancelViolationFineRequest { Reason = "Waived" });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task CancelViolationFineAsync_RequiresReason()
    {
        await using var dbContext = TestDb.Create();
        var seed = await SeedViolationAsync(dbContext);
        var fine = CreateFine(seed, ViolationFineStatus.Unpaid, paidAmount: 0m);
        dbContext.ViolationFines.Add(fine);
        await dbContext.SaveChangesAsync();
        var service = new ComplaintViolationService(
            dbContext,
            new FakeCompoundAccessService(new[] { seed.CompoundId }));

        var result = await service.CancelViolationFineAsync(
            fine.Id,
            new CancelViolationFineRequest { Reason = "   " });

        result.Status.Should().Be(ServiceResultStatus.BadRequest);
    }

    [Fact]
    public async Task SearchViolationFinesResidentAsync_ReturnsOnlyCurrentResidentsFines()
    {
        await using var dbContext = TestDb.Create();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var current = await SeedViolationAsync(dbContext, currentUserId, "Current");
        var other = await SeedViolationAsync(dbContext, otherUserId, "Other");
        dbContext.ViolationFines.AddRange(
            CreateFine(current, ViolationFineStatus.Unpaid, paidAmount: 0m),
            CreateFine(other, ViolationFineStatus.Unpaid, paidAmount: 0m));
        await dbContext.SaveChangesAsync();
        var service = new ComplaintViolationService(dbContext);

        var result = await service.SearchViolationFinesResidentAsync(
            currentUserId,
            new ViolationFineSearchQuery());

        result.Status.Should().Be(ServiceResultStatus.Success);
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Single().ResidentProfileId.Should().Be(current.ResidentProfileId);
    }

    [Fact]
    public async Task GetViolationFineResidentAsync_ReturnsNotFoundForAnotherResidentsFine()
    {
        await using var dbContext = TestDb.Create();
        var currentUserId = Guid.NewGuid();
        var other = await SeedViolationAsync(dbContext, Guid.NewGuid(), "Other");
        var otherFine = CreateFine(other, ViolationFineStatus.Unpaid, paidAmount: 0m);
        dbContext.ViolationFines.Add(otherFine);
        await dbContext.SaveChangesAsync();
        var service = new ComplaintViolationService(dbContext);

        var result = await service.GetViolationFineResidentAsync(currentUserId, otherFine.Id);

        result.Status.Should().Be(ServiceResultStatus.NotFound);
    }

    private static async Task<ViolationSeed> SeedViolationAsync(
        DARAK.Api.Data.ApplicationDbContext dbContext,
        Guid? userId = null,
        string name = "Resident")
    {
        var compound = new Compound
        {
            Name = name + " Compound",
            Code = Guid.NewGuid().ToString("N")[..8],
            City = "Baghdad",
            Area = "Karrada"
        };
        var resident = new ResidentProfile
        {
            UserId = userId ?? Guid.NewGuid(),
            CompoundId = compound.Id,
            FullName = name
        };
        var violation = new Violation
        {
            CompoundId = compound.Id,
            ResidentProfileId = resident.Id,
            ViolationType = ViolationType.NoiseAfterHours,
            Title = "Noise",
            Description = "Noise after midnight"
        };

        dbContext.AddRange(compound, resident, violation);
        await dbContext.SaveChangesAsync();

        return new ViolationSeed(compound.Id, resident.Id, violation.Id);
    }

    private static ViolationFine CreateFine(
        ViolationSeed seed,
        ViolationFineStatus status,
        decimal paidAmount)
    {
        return new ViolationFine
        {
            ViolationId = seed.ViolationId,
            CompoundId = seed.CompoundId,
            ResidentProfileId = seed.ResidentProfileId,
            Amount = 100m,
            PaidAmount = paidAmount,
            Status = status,
            Reason = "Fine",
            DueDate = new DateOnly(2026, 10, 1)
        };
    }

    private sealed record ViolationSeed(
        Guid CompoundId,
        Guid ResidentProfileId,
        Guid ViolationId);
}
