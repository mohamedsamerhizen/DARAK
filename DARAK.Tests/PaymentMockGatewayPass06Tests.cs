using System.Net;
using System.Net.Http.Json;
using DARAK.Api.DTOs.Payments;
using DARAK.Api.Entities;
using DARAK.Api.Enums;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class PaymentMockGatewayPass06Tests
{
    [Fact]
    public async Task Pass06_MockPaymentConfirmationEndpoint_IsDisabledByDefault()
    {
        using var factory = new DarakApiFactory();
        var resident = await factory.CreateAuthenticatedClientAsync(UserRole.Resident);

        var response = await resident.Client.PostAsJsonAsync(
            $"/api/resident/payments/{Guid.NewGuid()}/mock-zaincash/success",
            new ConfirmMockPaymentRequest { ProviderTransactionId = "DISABLED-P06" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pass06_MockPaymentConfirmationEndpoint_WorksOnlyWhenExplicitlyEnabled()
    {
        using var factory = new DarakApiFactory(enableMockGatewayEndpoints: true);
        var resident = await factory.CreateAuthenticatedClientAsync(UserRole.Resident);
        var paymentId = Guid.NewGuid();

        await factory.SeedAsync(dbContext =>
        {
            var compound = new Compound
            {
                Name = "PASS06 Mock Gateway Compound",
                Code = "P06-MOCK",
                City = "Baghdad",
                Area = "Karrada"
            };
            var unit = new PropertyUnit
            {
                CompoundId = compound.Id,
                UnitNumber = "P06-101",
                PropertyType = PropertyType.Apartment,
                UnitStatus = UnitStatus.Occupied
            };
            var residentProfile = new ResidentProfile
            {
                UserId = resident.UserId,
                CompoundId = compound.Id,
                FullName = "PASS06 Resident"
            };
            var cycle = new BillingCycle
            {
                CompoundId = compound.Id,
                Year = 2026,
                Month = 6,
                PeriodStart = new DateOnly(2026, 6, 1),
                PeriodEnd = new DateOnly(2026, 6, 30),
                DueDate = new DateOnly(2026, 7, 10)
            };
            var bill = new UtilityBill
            {
                CompoundId = compound.Id,
                PropertyUnitId = unit.Id,
                ResidentProfileId = residentProfile.Id,
                BillingCycleId = cycle.Id,
                BillNumber = "P06-UB-1",
                IssueDate = new DateOnly(2026, 6, 1),
                DueDate = new DateOnly(2026, 7, 10),
                SubtotalAmount = 40m,
                TotalAmount = 40m,
                BillStatus = BillStatus.Unpaid
            };
            var payment = new Payment
            {
                Id = paymentId,
                CompoundId = compound.Id,
                ResidentProfileId = residentProfile.Id,
                TargetType = PaymentTargetType.UtilityBill,
                TargetId = bill.Id,
                PaymentMethod = PaymentMethod.ZainCashMock,
                PaymentStatus = PaymentStatus.Pending,
                Amount = 40m,
                PaymentReference = "P06-MOCK-PAYMENT"
            };

            dbContext.AddRange(compound, unit, residentProfile, cycle, bill, payment);
            return Task.CompletedTask;
        });

        var response = await resident.Client.PostAsJsonAsync(
            $"/api/resident/payments/{paymentId}/mock-zaincash/success",
            new ConfirmMockPaymentRequest { ProviderTransactionId = "ENABLED-P06" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
