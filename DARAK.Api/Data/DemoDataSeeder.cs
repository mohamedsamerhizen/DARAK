using DARAK.Api.Enums;
using DARAK.Api.Entities;
using DARAK.Api.Identity;
using DARAK.Api.Interfaces;
using DARAK.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace DARAK.Api.Data;

public static class DemoDataSeeder
{
    private const string RiverCode = "DEMO-RIVER";
    private const string GardenCode = "DEMO-GARDEN";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var options = configuration.GetSection(DemoSeedOptions.SectionName).Get<DemoSeedOptions>()
            ?? new DemoSeedOptions();

        if (!options.Enabled)
        {
            return;
        }

        var environment = services.GetRequiredService<IWebHostEnvironment>();
        var isDemoSafeEnvironment = environment.IsDevelopment()
            || environment.IsEnvironment("Demo")
            || environment.IsEnvironment("Testing");
        if (!isDemoSafeEnvironment && !options.AllowProduction)
        {
            throw new InvalidOperationException("DemoSeed can run only in Development, Demo, or Testing unless DemoSeed:AllowProduction is explicitly true.");
        }

        if (options.SeedUsers)
        {
            ValidateDemoPassword(options.DemoPassword);
        }

        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        if (await dbContext.Compounds.AnyAsync(compound => compound.Code == RiverCode, cancellationToken)
            && await dbContext.Compounds.AnyAsync(compound => compound.Code == GardenCode, cancellationToken))
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var accessCodeHasher = services.GetRequiredService<IAccessCodeHasher>();

        await EnsureRolesAsync(roleManager);
        var users = await SeedUsersAsync(dbContext, userManager, options, cancellationToken);
        var structure = await SeedStructureAsync(dbContext, cancellationToken);
        var residents = await SeedResidentsAsync(dbContext, structure, users, cancellationToken);
        await SeedFinanceAsync(dbContext, structure, residents, users, cancellationToken);
        await SeedAccessControlAsync(dbContext, structure, residents, users, accessCodeHasher, cancellationToken);
        var operations = await SeedOperationsAsync(dbContext, structure, residents, users, cancellationToken);
        await SeedCommunicationsAsync(dbContext, structure, residents, users, cancellationToken);
        await SeedDocumentsApprovalsReportsAuditAsync(dbContext, structure, residents, users, operations, cancellationToken);
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var roleName in Enum.GetNames<UserRole>())
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }
    }

    private static async Task<DemoUsers> SeedUsersAsync(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        DemoSeedOptions options,
        CancellationToken cancellationToken)
    {
        var superAdmin = await EnsureUserAsync(dbContext, userManager, options, "demo.superadmin@darak.local", "Demo SuperAdmin", UserRole.SuperAdmin, cancellationToken);
        var compoundAdmin = await EnsureUserAsync(dbContext, userManager, options, "demo.compound.admin@darak.local", "Demo Compound Admin", UserRole.CompoundAdmin, cancellationToken);
        var accountant = await EnsureUserAsync(dbContext, userManager, options, "demo.accountant@darak.local", "Demo Accountant", UserRole.Accountant, cancellationToken);
        var maintenance = await EnsureUserAsync(dbContext, userManager, options, "demo.maintenance@darak.local", "Demo Maintenance Manager", UserRole.MaintenanceStaff, cancellationToken);
        var guard = await EnsureUserAsync(dbContext, userManager, options, "demo.guard@darak.local", "Demo Gate Guard", UserRole.Guard, cancellationToken);
        var operations = await EnsureUserAsync(dbContext, userManager, options, "demo.operations@darak.local", "Demo Operations Coordinator", UserRole.CompoundAdmin, cancellationToken);

        var residents = new List<ApplicationUser>();
        for (var index = 1; index <= 30; index++)
        {
            var role = index <= 12 ? UserRole.Resident : (UserRole?)null;
            residents.Add(await EnsureUserAsync(
                dbContext,
                userManager,
                options,
                $"demo.resident{index:00}@darak.local",
                $"Demo Resident {index:00}",
                role,
                cancellationToken));
        }

        return new DemoUsers(superAdmin, compoundAdmin, accountant, maintenance, guard, operations, residents);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        DemoSeedOptions options,
        string email,
        string fullName,
        UserRole? role,
        CancellationToken cancellationToken)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (role.HasValue && !await userManager.IsInRoleAsync(existing, role.Value.ToString()))
            {
                await userManager.AddToRoleAsync(existing, role.Value.ToString());
            }

            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            NormalizedUserName = email.ToUpperInvariant(),
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = fullName,
            EmailConfirmed = true,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        if (options.SeedUsers)
        {
            var created = await userManager.CreateAsync(user, options.DemoPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create demo user {email}: {string.Join("; ", created.Errors.Select(error => error.Description))}");
            }

            if (role.HasValue)
            {
                await userManager.AddToRoleAsync(user, role.Value.ToString());
            }
        }
        else
        {
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    private static async Task<DemoStructure> SeedStructureAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var compounds = new[]
        {
            new Compound
            {
                Name = "DARAK River Residence",
                Code = RiverCode,
                Description = "Demo mid-rise residential compound for finance, maintenance, and guard workflows.",
                City = "Baghdad",
                Area = "Karrada",
                Address = "Demo Street 10"
            },
            new Compound
            {
                Name = "DARAK Garden Courts",
                Code = GardenCode,
                Description = "Demo family compound used for tenant isolation and multi-compound reporting.",
                City = "Baghdad",
                Area = "Mansour",
                Address = "Demo Street 22"
            }
        };
        dbContext.Compounds.AddRange(compounds);

        var buildings = new List<Building>();
        var floors = new List<Floor>();
        var units = new List<PropertyUnit>();
        var parking = new List<ParkingSpot>();
        foreach (var compound in compounds)
        {
            for (var buildingIndex = 1; buildingIndex <= 2; buildingIndex++)
            {
                var building = new Building
                {
                    CompoundId = compound.Id,
                    Name = $"{compound.Code[^5..]} Tower {buildingIndex}",
                    Code = $"{compound.Code}-B{buildingIndex}",
                    NumberOfFloors = 3
                };
                buildings.Add(building);

                for (var floorIndex = 1; floorIndex <= 3; floorIndex++)
                {
                    var floor = new Floor
                    {
                        CompoundId = compound.Id,
                        BuildingId = building.Id,
                        FloorNumber = floorIndex,
                        Name = $"Floor {floorIndex}"
                    };
                    floors.Add(floor);

                    for (var unitIndex = 1; unitIndex <= 6; unitIndex++)
                    {
                        units.Add(new PropertyUnit
                        {
                            CompoundId = compound.Id,
                            BuildingId = building.Id,
                            FloorId = floor.Id,
                            UnitNumber = $"{buildingIndex}{floorIndex}{unitIndex:00}",
                            PropertyType = unitIndex % 5 == 0 ? PropertyType.Shop : PropertyType.Apartment,
                            UnitStatus = UnitStatus.Available,
                            AreaSquareMeters = 95 + unitIndex * 4,
                            Bedrooms = unitIndex % 3 + 1,
                            Bathrooms = unitIndex % 2 + 1,
                            HasParking = unitIndex % 2 == 0,
                            Notes = "Demo unit"
                        });
                    }
                }
            }

            for (var spot = 1; spot <= 12; spot++)
            {
                parking.Add(new ParkingSpot
                {
                    CompoundId = compound.Id,
                    SpotNumber = $"{compound.Code}-P{spot:00}",
                    IsCovered = spot % 3 == 0,
                    IsReserved = spot <= 5
                });
            }
        }

        dbContext.Buildings.AddRange(buildings);
        dbContext.Floors.AddRange(floors);
        dbContext.PropertyUnits.AddRange(units);
        dbContext.ParkingSpots.AddRange(parking);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DemoStructure(compounds[0], compounds[1], buildings, floors, units);
    }

    private static async Task<List<ResidentProfile>> SeedResidentsAsync(
        ApplicationDbContext dbContext,
        DemoStructure structure,
        DemoUsers users,
        CancellationToken cancellationToken)
    {
        var residents = new List<ResidentProfile>();
        var occupiedUnits = structure.Units.Take(30).ToArray();
        for (var index = 0; index < 30; index++)
        {
            var user = users.Residents[index];
            var unit = occupiedUnits[index];
            var occupancyType = index % 3 == 0
                ? OccupancyType.OwnerCash
                : index % 3 == 1
                    ? OccupancyType.OwnerInstallment
                    : OccupancyType.Tenant;
            var resident = new ResidentProfile
            {
                UserId = user.Id,
                CompoundId = unit.CompoundId,
                FullName = user.FullName,
                NationalId = $"DEMO-NID-{index + 1:0000}",
                PhoneNumber = $"+964770000{index + 1:0000}",
                DateOfBirth = new DateOnly(1980 + index % 18, (index % 12) + 1, (index % 25) + 1),
                Notes = "Synthetic demo resident profile."
            };
            resident.FamilyMembers.Add(new FamilyMember
            {
                FullName = $"{user.FullName} Family",
                Relationship = index % 2 == 0 ? "Spouse" : "Sibling",
                DateOfBirth = new DateOnly(1990, 1, 1)
            });
            resident.EmergencyContacts.Add(new EmergencyContact
            {
                FullName = $"{user.FullName} Emergency",
                Relationship = "Emergency contact",
                PhoneNumber = $"+964771000{index + 1:0000}"
            });
            resident.OccupancyRecords.Add(new OccupancyRecord
            {
                CompoundId = unit.CompoundId,
                PropertyUnitId = unit.Id,
                OccupancyType = occupancyType,
                OccupancyStatus = OccupancyStatus.Active,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-8)),
                ContractNumber = $"DEMO-OCC-{index + 1:0000}"
            });
            residents.Add(resident);
            unit.UnitStatus = UnitStatus.Occupied;
        }

        dbContext.ResidentProfiles.AddRange(residents);
        await dbContext.SaveChangesAsync(cancellationToken);
        return residents;
    }

    private static async Task SeedFinanceAsync(
        ApplicationDbContext dbContext,
        DemoStructure structure,
        IReadOnlyList<ResidentProfile> residents,
        DemoUsers users,
        CancellationToken cancellationToken)
    {
        var servicesByCompound = structure.Compounds.SelectMany(compound => new[]
        {
            new CompoundService { CompoundId = compound.Id, ServiceType = UtilityServiceType.Water, Name = "Water service", DefaultMonthlyFee = 25000m, IsMeterBased = true },
            new CompoundService { CompoundId = compound.Id, ServiceType = UtilityServiceType.Electricity, Name = "Electricity service", DefaultMonthlyFee = 45000m, IsMeterBased = true },
            new CompoundService { CompoundId = compound.Id, ServiceType = UtilityServiceType.Maintenance, Name = "Common-area service", DefaultMonthlyFee = 35000m, IsMeterBased = false }
        }).ToList();
        dbContext.CompoundServices.AddRange(servicesByCompound);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cycles = new List<BillingCycle>();
        foreach (var compound in structure.Compounds)
        {
            for (var offset = 2; offset >= 0; offset--)
            {
                var date = DateTime.UtcNow.AddMonths(-offset);
                cycles.Add(new BillingCycle
                {
                    CompoundId = compound.Id,
                    Year = date.Year,
                    Month = date.Month,
                    PeriodStart = new DateOnly(date.Year, date.Month, 1),
                    PeriodEnd = new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)),
                    DueDate = new DateOnly(date.Year, date.Month, Math.Min(25, DateTime.DaysInMonth(date.Year, date.Month)))
                });
            }
        }
        dbContext.BillingCycles.AddRange(cycles);
        await dbContext.SaveChangesAsync(cancellationToken);

        var bills = new List<UtilityBill>();
        var payments = new List<Payment>();
        var ledger = new List<ResidentLedgerEntry>();
        foreach (var resident in residents.Take(24).Select((resident, index) => new { resident, index }))
        {
            var occupancy = resident.resident.OccupancyRecords.First();
            var cycle = cycles.Last(item => item.CompoundId == resident.resident.CompoundId);
            var subtotal = 110000m + resident.index * 2500m;
            var paid = resident.index % 4 == 0 ? subtotal : resident.index % 4 == 1 ? Math.Round(subtotal / 2m, 2) : 0m;
            var status = paid >= subtotal
                ? BillStatus.Paid
                : paid > 0
                    ? BillStatus.PartiallyPaid
                    : resident.index % 4 == 2 ? BillStatus.Overdue : BillStatus.Unpaid;
            var bill = new UtilityBill
            {
                CompoundId = resident.resident.CompoundId,
                PropertyUnitId = occupancy.PropertyUnitId,
                ResidentProfileId = resident.resident.Id,
                BillingCycleId = cycle.Id,
                BillNumber = $"DEMO-BILL-{resident.index + 1:0000}",
                BillStatus = status,
                IssueDate = today.AddDays(-20),
                DueDate = today.AddDays(status == BillStatus.Overdue ? -5 : 10),
                SubtotalAmount = subtotal,
                TotalAmount = subtotal,
                PaidAmount = paid,
                Notes = "Demo utility bill"
            };
            bill.Lines.Add(new UtilityBillLine
            {
                CompoundServiceId = servicesByCompound.First(service => service.CompoundId == bill.CompoundId).Id,
                Description = "Demo utility consumption",
                Quantity = 1,
                UnitPrice = subtotal,
                LineTotal = subtotal
            });
            bills.Add(bill);

            if (paid > 0)
            {
                var payment = new Payment
                {
                    CompoundId = bill.CompoundId,
                    ResidentProfileId = bill.ResidentProfileId,
                    TargetType = PaymentTargetType.UtilityBill,
                    TargetId = bill.Id,
                    PaymentMethod = resident.index % 2 == 0 ? PaymentMethod.Cash : PaymentMethod.BankTransfer,
                    PaymentStatus = PaymentStatus.Succeeded,
                    Amount = paid,
                    PaymentReference = $"DEMO-PAY-{resident.index + 1:0000}",
                    CompletedAt = DateTime.UtcNow.AddDays(-resident.index % 7)
                };
                payment.Attempts.Add(new PaymentAttempt
                {
                    AttemptStatus = PaymentStatus.Succeeded,
                    Provider = "DemoLocal",
                    ProviderTransactionId = $"DEMO-TX-{resident.index + 1:0000}",
                    Message = "Demo payment accepted."
                });
                payment.Receipt = new Receipt
                {
                    ReceiptNumber = $"DEMO-RCPT-{resident.index + 1:0000}",
                    Amount = paid
                };
                payments.Add(payment);
                ledger.Add(new ResidentLedgerEntry
                {
                    CompoundId = bill.CompoundId,
                    ResidentProfileId = resident.resident.Id,
                    Direction = FinancialLedgerEntryDirection.Credit,
                    SourceType = FinancialLedgerSourceType.Payment,
                    SourceId = payment.Id,
                    Amount = paid,
                    Reference = payment.PaymentReference,
                    Description = "Demo payment ledger credit.",
                    CreatedByUserId = users.Accountant.Id
                });
            }
        }
        dbContext.UtilityBills.AddRange(bills);
        dbContext.Payments.AddRange(payments);
        dbContext.ResidentLedgerEntries.AddRange(ledger);

        var tenant = residents.First(resident => resident.OccupancyRecords.First().OccupancyType == OccupancyType.Tenant);
        var tenantUnitId = tenant.OccupancyRecords.First().PropertyUnitId;
        var rentContract = new RentContract
        {
            CompoundId = tenant.CompoundId,
            PropertyUnitId = tenantUnitId,
            ResidentProfileId = tenant.Id,
            ContractNumber = "DEMO-RENT-0001",
            StartDate = today.AddMonths(-4),
            EndDate = today.AddMonths(8),
            MonthlyRentAmount = 650000m,
            DepositAmount = 650000m,
            Notes = "Demo active rent contract."
        };
        rentContract.RentInvoices.Add(new RentInvoice
        {
            CompoundId = tenant.CompoundId,
            PropertyUnitId = tenantUnitId,
            ResidentProfileId = tenant.Id,
            InvoiceNumber = "DEMO-RENT-INV-0001",
            Year = today.Year,
            Month = today.Month,
            IssueDate = today.AddDays(-10),
            DueDate = today.AddDays(5),
            RentAmount = 650000m,
            TotalAmount = 650000m,
            PaidAmount = 325000m,
            RentInvoiceStatus = RentInvoiceStatus.PartiallyPaid
        });
        dbContext.RentContracts.Add(rentContract);

        var owner = residents.First(resident => resident.OccupancyRecords.First().OccupancyType == OccupancyType.OwnerInstallment);
        var ownerUnitId = owner.OccupancyRecords.First().PropertyUnitId;
        var sale = new PropertySaleContract
        {
            CompoundId = owner.CompoundId,
            PropertyUnitId = ownerUnitId,
            ResidentProfileId = owner.Id,
            SaleType = SaleType.Installment,
            ContractNumber = "DEMO-SALE-0001",
            ContractDate = today.AddMonths(-6),
            PropertyPrice = 185000000m,
            DownPaymentAmount = 25000000m,
            InstallmentCount = 12,
            FirstInstallmentDueDate = today.AddMonths(-5)
        };
        for (var installment = 1; installment <= 6; installment++)
        {
            sale.Installments.Add(new InstallmentScheduleItem
            {
                CompoundId = owner.CompoundId,
                PropertyUnitId = ownerUnitId,
                ResidentProfileId = owner.Id,
                InstallmentNumber = installment,
                DueDate = today.AddMonths(installment - 5),
                Amount = 13333333m,
                PaidAmount = installment <= 3 ? 13333333m : 0m,
                InstallmentStatus = installment <= 3 ? InstallmentStatus.Paid : installment == 4 ? InstallmentStatus.Overdue : InstallmentStatus.Pending,
                PaidAt = installment <= 3 ? DateTime.UtcNow.AddMonths(-installment) : null
            });
        }
        dbContext.PropertySaleContracts.Add(sale);

        var overdueResident = residents[2];
        var collection = new CollectionCase
        {
            CompoundId = overdueResident.CompoundId,
            ResidentProfileId = overdueResident.Id,
            SourceType = FinancialCollectionSourceType.UtilityBill,
            SourceId = bills.First(bill => bill.ResidentProfileId == overdueResident.Id).Id,
            Stage = CollectionStage.PaymentPlan,
            AmountDue = 220000m,
            DueDate = today.AddDays(-8),
            Reason = "Demo overdue bill follow-up.",
            AssignedToUserId = users.Accountant.Id,
            CreatedByUserId = users.Accountant.Id,
            LastActionAtUtc = DateTime.UtcNow.AddDays(-2)
        };
        collection.LegalNotices.Add(new LegalNotice
        {
            CompoundId = overdueResident.CompoundId,
            ResidentProfileId = overdueResident.Id,
            NoticeType = LegalNoticeType.PaymentReminder,
            Status = LegalNoticeStatus.Issued,
            Title = "Demo payment reminder",
            Body = "Synthetic reminder for demo collection workflow.",
            DeliveryChannel = "InApp",
            DeliveryReference = "DEMO-NOTICE-0001",
            DeadlineDate = today.AddDays(7),
            CreatedByUserId = users.Accountant.Id,
            IssuedByUserId = users.Accountant.Id,
            IssuedAtUtc = DateTime.UtcNow.AddDays(-1)
        });
        var plan = new PaymentPlan
        {
            CompoundId = overdueResident.CompoundId,
            ResidentProfileId = overdueResident.Id,
            TotalAmount = 220000m,
            InstallmentCount = 2,
            StartDate = today,
            Notes = "Demo payment plan.",
            CreatedByUserId = users.Accountant.Id
        };
        plan.Installments.Add(new PaymentPlanInstallment { InstallmentNumber = 1, DueDate = today.AddDays(10), Amount = 110000m });
        plan.Installments.Add(new PaymentPlanInstallment { InstallmentNumber = 2, DueDate = today.AddDays(40), Amount = 110000m });
        collection.PaymentPlans.Add(plan);
        dbContext.CollectionCases.Add(collection);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAccessControlAsync(
        ApplicationDbContext dbContext,
        DemoStructure structure,
        IReadOnlyList<ResidentProfile> residents,
        DemoUsers users,
        IAccessCodeHasher accessCodeHasher,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var passes = residents.Take(8).Select((resident, index) =>
        {
            var unitId = resident.OccupancyRecords.First().PropertyUnitId;
            var status = index switch
            {
                5 => VisitorPassStatus.Cancelled,
                6 => VisitorPassStatus.Expired,
                _ => VisitorPassStatus.Approved
            };
            var pass = new VisitorPass
            {
                ResidentProfileId = resident.Id,
                CompoundId = resident.CompoundId,
                PropertyUnitId = unitId,
                VisitorName = $"Demo Visitor {index + 1}",
                VisitorPhoneNumber = $"+964780000{index + 1:0000}",
                VisitReason = index % 2 == 0 ? "Family visit" : "Delivery",
                AccessCode = accessCodeHasher.Hash($"DEMO-VISITOR-{index + 1:0000}"),
                Status = status,
                ValidFrom = status == VisitorPassStatus.Expired ? now.AddDays(-3) : now.AddHours(-2),
                ValidUntil = status == VisitorPassStatus.Expired ? now.AddDays(-1) : now.AddHours(8),
                CheckedInAt = index == 0 ? now.AddHours(-1) : null,
                CheckedOutAt = index == 0 ? now.AddMinutes(-20) : null,
                CancelledAt = status == VisitorPassStatus.Cancelled ? now.AddHours(-1) : null,
                DenialReason = status == VisitorPassStatus.Cancelled ? "Demo cancellation" : null
            };
            pass.AccessLogs.Add(new VisitorAccessLog
            {
                GuardUserId = users.Guard.Id,
                Action = VisitorAccessAction.Verified,
                Notes = "Demo verification log.",
                CreatedAt = now.AddHours(-1)
            });
            return pass;
        }).ToList();
        dbContext.VisitorPasses.AddRange(passes);

        var vendor = new ServiceVendor
        {
            CompoundId = structure.River.Id,
            Name = "Demo Secure Contractors",
            ContactPersonName = "Demo Contractor Lead",
            PhoneNumber = "+9647900000001",
            Email = "contractors@darak.local",
            ServiceType = VendorServiceType.Security,
            Status = VendorStatus.Active
        };
        dbContext.ServiceVendors.Add(vendor);
        await dbContext.SaveChangesAsync(cancellationToken);

        var permit = new ContractorWorkPermit
        {
            CompoundId = structure.River.Id,
            VendorId = vendor.Id,
            Purpose = "CCTV preventive inspection",
            WorkArea = "Gate and parking",
            EquipmentList = "Ladders, tester",
            RiskLevel = ContractorWorkPermitRiskLevel.Medium,
            Status = ContractorWorkPermitStatus.Approved,
            AllowedFromUtc = now.AddHours(-2),
            AllowedUntilUtc = now.AddHours(6),
            RequiresEscort = true,
            CreatedByUserId = users.Operations.Id,
            ApprovedByUserId = users.Operations.Id,
            ApprovedAtUtc = now.AddHours(-3),
            CheckedInAtUtc = now.AddMinutes(-45),
            GuardCheckedInByUserId = users.Guard.Id
        };
        permit.AccessLogs.Add(new ContractorAccessLog
        {
            GuardUserId = users.Guard.Id,
            Action = ContractorAccessAction.CheckIn,
            Notes = "Demo contractor check-in.",
            CreatedAtUtc = now.AddMinutes(-45)
        });
        dbContext.ContractorWorkPermits.Add(permit);
        dbContext.AccessCredentials.Add(new AccessCredential
        {
            CompoundId = structure.River.Id,
            CredentialType = AccessCredentialType.TemporaryAccessCode,
            OwnerType = AccessCredentialOwnerType.Contractor,
            OwnerEntityId = permit.Id,
            OwnerDisplayName = vendor.Name,
            CredentialCode = accessCodeHasher.Hash("DEMO-CONTRACTOR-0001"),
            ValidFromUtc = now.AddHours(-2),
            ValidUntilUtc = now.AddHours(6),
            SourceContractorWorkPermitId = permit.Id,
            Notes = "Demo hashed contractor credential."
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<DemoOperations> SeedOperationsAsync(
        ApplicationDbContext dbContext,
        DemoStructure structure,
        IReadOnlyList<ResidentProfile> residents,
        DemoUsers users,
        CancellationToken cancellationToken)
    {
        var staff = new[]
        {
            new StaffMember { CompoundId = structure.River.Id, FullName = "Demo Electrician", PhoneNumber = "+9647500000001", Email = "electrician@darak.local", StaffType = StaffType.MaintenanceTechnician, Status = StaffStatus.Active, UserId = users.Maintenance.Id },
            new StaffMember { CompoundId = structure.Garden.Id, FullName = "Demo Guard Supervisor", PhoneNumber = "+9647500000002", Email = "guard.supervisor@darak.local", StaffType = StaffType.SecurityGuard, Status = StaffStatus.Active, UserId = users.Guard.Id }
        };
        var vendors = new[]
        {
            new ServiceVendor { CompoundId = structure.River.Id, Name = "Demo Elevator Vendor", PhoneNumber = "+9647510000001", ServiceType = VendorServiceType.Maintenance, Status = VendorStatus.Active },
            new ServiceVendor { CompoundId = structure.Garden.Id, Name = "Demo Plumbing Vendor", PhoneNumber = "+9647510000002", ServiceType = VendorServiceType.Plumbing, Status = VendorStatus.Active }
        };
        dbContext.StaffMembers.AddRange(staff);
        dbContext.ServiceVendors.AddRange(vendors);
        await dbContext.SaveChangesAsync(cancellationToken);

        var asset = new MaintenanceAsset
        {
            CompoundId = structure.River.Id,
            BuildingId = structure.Buildings.First(building => building.CompoundId == structure.River.Id).Id,
            Name = "Demo Elevator A",
            Code = "DEMO-ASSET-ELEV-A",
            AssetType = MaintenanceAssetType.Elevator,
            Status = MaintenanceAssetStatus.Active,
            LocationDescription = "River Tower 1",
            LastServiceAtUtc = DateTime.UtcNow.AddDays(-40),
            NextServiceDueAtUtc = DateTime.UtcNow.AddDays(5)
        };
        dbContext.MaintenanceAssets.Add(asset);
        await dbContext.SaveChangesAsync(cancellationToken);

        var resident = residents[0];
        var maintenanceRequest = new MaintenanceRequest
        {
            ResidentProfileId = resident.Id,
            CompoundId = resident.CompoundId,
            PropertyUnitId = resident.OccupancyRecords.First().PropertyUnitId,
            AssignedToUserId = users.Maintenance.Id,
            Title = "Demo water pressure issue",
            Description = "Resident reported low pressure.",
            Priority = MaintenancePriority.High,
            Status = MaintenanceStatus.InProgress,
            CostEstimate = 85000m,
            AssignedAt = DateTime.UtcNow.AddHours(-3),
            StartedAt = DateTime.UtcNow.AddHours(-2)
        };
        maintenanceRequest.StatusHistory.Add(new MaintenanceStatusHistory
        {
            ChangedByUserId = users.Maintenance.Id,
            NewStatus = MaintenanceStatus.InProgress,
            Notes = "Demo status history."
        });
        dbContext.MaintenanceRequests.Add(maintenanceRequest);

        var workOrders = new[]
        {
            new WorkOrder
            {
                CompoundId = structure.River.Id,
                Title = "Demo elevator PM",
                Description = "Monthly elevator preventive maintenance.",
                SourceType = WorkOrderSourceType.Manual,
                SourceEntityId = asset.Id,
                Priority = WorkOrderPriority.High,
                Status = WorkOrderStatus.Assigned,
                AssignedStaffMemberId = staff[0].Id,
                MaintenanceAssetId = asset.Id,
                ScheduledAtUtc = DateTime.UtcNow.AddDays(1),
                DueAtUtc = DateTime.UtcNow.AddDays(2),
                EstimatedCost = 150000m,
                PreventiveMaintenanceOccurrenceKey = $"PM:{asset.Id:N}:{DateTime.UtcNow:yyyyMMddHHmmss}",
                SlaStatus = MaintenanceSlaStatus.WithinSla,
                CreatedByUserId = users.Maintenance.Id
            },
            new WorkOrder
            {
                CompoundId = structure.River.Id,
                Title = "Demo breached pump repair",
                Description = "Pump repair intentionally breached for SLA dashboard.",
                Priority = WorkOrderPriority.Urgent,
                Status = WorkOrderStatus.InProgress,
                AssignedVendorId = vendors[0].Id,
                DueAtUtc = DateTime.UtcNow.AddHours(-6),
                ResolutionDueAtUtc = DateTime.UtcNow.AddHours(-2),
                SlaStatus = MaintenanceSlaStatus.Escalated,
                SlaBreachedAtUtc = DateTime.UtcNow.AddHours(-2),
                SlaEscalatedAtUtc = DateTime.UtcNow.AddHours(-1),
                LastSlaEscalatedAtUtc = DateTime.UtcNow.AddHours(-1),
                SlaEscalationCount = 1,
                SlaBreachReason = "Demo overdue resolution.",
                EstimatedCost = 275000m,
                CreatedByUserId = users.Maintenance.Id
            }
        };
        dbContext.WorkOrders.AddRange(workOrders);
        dbContext.PreventiveMaintenancePlans.Add(new PreventiveMaintenancePlan
        {
            CompoundId = structure.River.Id,
            MaintenanceAssetId = asset.Id,
            Title = "Demo elevator monthly PM",
            Description = "Monthly safety and performance check.",
            Cadence = PreventiveMaintenanceCadence.Monthly,
            Priority = WorkOrderPriority.High,
            AssignedStaffMemberId = staff[0].Id,
            NextDueAtUtc = DateTime.UtcNow.AddDays(28),
            LastGeneratedAtUtc = DateTime.UtcNow,
            LastGeneratedOccurrenceKey = workOrders[0].PreventiveMaintenanceOccurrenceKey
        });

        var stock = new[]
        {
            new StockItem { CompoundId = structure.River.Id, Name = "Water pump seal", Sku = "DEMO-SEAL-01", Category = "Plumbing", UnitOfMeasure = "pcs", CurrentQuantity = 3, MinimumQuantity = 5, AverageUnitCost = 12000m },
            new StockItem { CompoundId = structure.River.Id, Name = "LED corridor lamp", Sku = "DEMO-LED-01", Category = "Electrical", UnitOfMeasure = "pcs", CurrentQuantity = 45, MinimumQuantity = 10, AverageUnitCost = 3500m },
            new StockItem { CompoundId = structure.Garden.Id, Name = "Gate access card", Sku = "DEMO-CARD-01", Category = "Security", UnitOfMeasure = "pcs", CurrentQuantity = 8, MinimumQuantity = 20, AverageUnitCost = 2500m }
        };
        dbContext.StockItems.AddRange(stock);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.InventoryMovements.AddRange(
            new InventoryMovement { CompoundId = structure.River.Id, StockItemId = stock[1].Id, MovementType = InventoryMovementType.AdjustmentIncrease, Quantity = 45, UnitCost = 3500m, CreatedByUserId = users.Operations.Id, Reference = "DEMO-STOCK-OPENING-LED" },
            new InventoryMovement { CompoundId = structure.River.Id, StockItemId = stock[0].Id, WorkOrderId = workOrders[1].Id, MovementType = InventoryMovementType.IssuedToWorkOrder, Quantity = 2, UnitCost = 12000m, CreatedByUserId = users.Maintenance.Id, Reference = "DEMO-STOCK-ISSUE-001" });

        var procurement = new ProcurementRequest
        {
            CompoundId = structure.River.Id,
            RequestedByUserId = users.Operations.Id,
            Title = "Demo replenish low-stock parts",
            Reason = "Pump seals are below minimum stock.",
            Priority = WorkOrderPriority.High,
            Status = ProcurementRequestStatus.Approved,
            RelatedWorkOrderId = workOrders[1].Id,
            ApprovedAtUtc = DateTime.UtcNow.AddDays(-1),
            ApprovedByUserId = users.Operations.Id
        };
        procurement.Items.Add(new ProcurementRequestItem
        {
            StockItemId = stock[0].Id,
            Description = "Water pump seal",
            Quantity = 20,
            EstimatedUnitCost = 12000m
        });
        dbContext.ProcurementRequests.Add(procurement);
        await dbContext.SaveChangesAsync(cancellationToken);

        var po = new PurchaseOrder
        {
            CompoundId = structure.River.Id,
            ProcurementRequestId = procurement.Id,
            VendorId = vendors[1].Id,
            OrderNumber = "DEMO-PO-0001",
            Status = PurchaseOrderStatus.PartiallyReceived,
            OrderedAtUtc = DateTime.UtcNow.AddDays(-2),
            ExpectedDeliveryAtUtc = DateTime.UtcNow.AddDays(3),
            CreatedByUserId = users.Operations.Id,
            Notes = "Demo partially received purchase order."
        };
        po.Items.Add(new PurchaseOrderItem
        {
            StockItemId = stock[0].Id,
            Description = "Water pump seal",
            QuantityOrdered = 20,
            QuantityReceived = 8,
            UnitCost = 12000m
        });
        dbContext.PurchaseOrders.Add(po);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.InventoryMovements.Add(new InventoryMovement
        {
            CompoundId = structure.River.Id,
            StockItemId = stock[0].Id,
            PurchaseOrderItemId = po.Items.Single().Id,
            MovementType = InventoryMovementType.ReceivedFromPurchaseOrder,
            Quantity = 8,
            UnitCost = 12000m,
            CreatedByUserId = users.Operations.Id,
            Reference = "DEMO-RECEIPT-PO-0001"
        });
        stock[0].CurrentQuantity += 8;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DemoOperations(staff, vendors, stock, workOrders);
    }

    private static async Task SeedCommunicationsAsync(
        ApplicationDbContext dbContext,
        DemoStructure structure,
        IReadOnlyList<ResidentProfile> residents,
        DemoUsers users,
        CancellationToken cancellationToken)
    {
        var announcement = new Announcement
        {
            CompoundId = structure.River.Id,
            Title = "Demo water maintenance window",
            Body = "Water pressure may be reduced tonight while the pump room is serviced.",
            Category = AnnouncementCategory.Utility,
            Priority = AnnouncementPriority.High,
            Audience = AnnouncementAudience.AllResidents,
            Status = AnnouncementStatus.Published,
            PublishedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddDays(2),
            CreatedByUserId = users.Operations.Id,
            IsPinned = true
        };
        dbContext.Announcements.Add(announcement);

        var outage = new UtilityOutage
        {
            CompoundId = structure.River.Id,
            AnnouncementId = announcement.Id,
            CreatedByUserId = users.Operations.Id,
            ServiceType = UtilityOutageServiceType.Water,
            AffectedScope = UtilityOutageAffectedScope.Compound,
            Status = UtilityOutageStatus.Active,
            Severity = UtilityOutageSeverity.High,
            Title = "Demo water pressure reduction",
            Description = "Pump room maintenance in progress.",
            EstimatedStartAtUtc = DateTime.UtcNow.AddHours(-1),
            EstimatedEndAtUtc = DateTime.UtcNow.AddHours(4),
            PublishedAtUtc = DateTime.UtcNow.AddHours(-1),
            NotifyResidents = true,
            RecipientCount = residents.Count(resident => resident.CompoundId == structure.River.Id),
            OutboxItemCount = residents.Count(resident => resident.CompoundId == structure.River.Id)
        };
        outage.Updates.Add(new UtilityOutageUpdate
        {
            CreatedByUserId = users.Operations.Id,
            UpdateType = UtilityOutageUpdateType.Information,
            Message = "Demo update: maintenance crew is on site."
        });
        dbContext.UtilityOutages.Add(outage);

        var poll = new CommunityPoll
        {
            CompoundId = structure.River.Id,
            Question = "Which community service should be improved next?",
            Description = "Synthetic poll for demo resident engagement.",
            Status = CommunityPollStatus.Open,
            StartsAt = DateTime.UtcNow.AddDays(-1),
            EndsAt = DateTime.UtcNow.AddDays(7),
            CreatedByUserId = users.Operations.Id
        };
        poll.Options.Add(new CommunityPollOption { Text = "Gym hours", DisplayOrder = 1 });
        poll.Options.Add(new CommunityPollOption { Text = "Parking markings", DisplayOrder = 2 });
        poll.Options.Add(new CommunityPollOption { Text = "Garden lighting", DisplayOrder = 3 });
        dbContext.CommunityPolls.Add(poll);

        var conversation = new Conversation
        {
            CompoundId = residents[0].CompoundId,
            ResidentProfileId = residents[0].Id,
            PropertyUnitId = residents[0].OccupancyRecords.First().PropertyUnitId,
            Status = ConversationStatus.Reopened,
            Priority = ConversationPriority.High,
            Topic = ConversationTopic.Maintenance,
            IssueType = ConversationIssueType.MaintenanceWaterLeak,
            AssignedToUserId = users.Maintenance.Id,
            AssignedByUserId = users.Operations.Id,
            AssignedAtUtc = DateTime.UtcNow.AddHours(-10),
            ReopenCount = 1,
            LastReopenReason = "Demo resident requested follow-up.",
            LastMessageAtUtc = DateTime.UtcNow.AddHours(-2),
            LastResidentMessageAtUtc = DateTime.UtcNow.AddHours(-2)
        };
        conversation.Messages.Add(new ConversationMessage { SenderUserId = residents[0].UserId, MessageType = ConversationMessageType.ResidentMessage, Body = "The leak returned under the sink." });
        conversation.Messages.Add(new ConversationMessage { SenderUserId = null, MessageType = ConversationMessageType.SystemMessage, Body = "Conversation reopened by resident." });
        dbContext.Conversations.Add(conversation);

        foreach (var resident in residents.Take(12))
        {
            dbContext.ResidentNotificationPreferences.Add(new ResidentNotificationPreference
            {
                UserId = resident.UserId,
                AnnouncementNotificationsEnabled = resident != residents[3],
                CampaignNotificationsEnabled = resident != residents[4],
                InAppEnabled = true
            });
            dbContext.ResidentNotifications.Add(new ResidentNotification
            {
                UserId = resident.UserId,
                Title = announcement.Title,
                Message = announcement.Body,
                Type = ResidentNotificationType.Announcement,
                Severity = ResidentNotificationSeverity.Warning,
                RelatedEntityType = nameof(Announcement),
                RelatedEntityId = announcement.Id
            });
            dbContext.NotificationOutboxes.Add(new NotificationOutbox
            {
                CompoundId = resident.CompoundId,
                ResidentProfileId = resident.Id,
                RecipientUserId = resident.UserId,
                Channel = NotificationChannel.InApp,
                EventType = NotificationEventType.AnnouncementPublished,
                Priority = NotificationPriority.High,
                Status = resident == residents[2] ? NotificationStatus.Failed : resident == residents[1] ? NotificationStatus.Sent : NotificationStatus.Pending,
                RecipientName = resident.FullName,
                Subject = announcement.Title,
                Body = announcement.Body,
                RelatedEntityType = NotificationRelatedEntityType.Announcement,
                RelatedEntityId = announcement.Id,
                SentAtUtc = resident == residents[1] ? DateTime.UtcNow.AddHours(-1) : null,
                FailedAtUtc = resident == residents[2] ? DateTime.UtcNow.AddMinutes(-30) : null,
                LastError = resident == residents[2] ? "Demo simulated delivery failure." : null,
                RetryCount = resident == residents[2] ? 3 : 0
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDocumentsApprovalsReportsAuditAsync(
        ApplicationDbContext dbContext,
        DemoStructure structure,
        IReadOnlyList<ResidentProfile> residents,
        DemoUsers users,
        DemoOperations operations,
        CancellationToken cancellationToken)
    {
        dbContext.DocumentRequirements.AddRange(
            new DocumentRequirement { CompoundId = structure.River.Id, Category = DocumentCategory.ResidentIdentity, AppliesTo = DocumentRequirementAppliesTo.Resident, Title = "Demo resident ID", Description = "Synthetic identity requirement.", CreatedByUserId = users.Operations.Id },
            new DocumentRequirement { CompoundId = structure.River.Id, Category = DocumentCategory.LeaseContract, AppliesTo = DocumentRequirementAppliesTo.Tenant, Title = "Demo lease contract", Description = "Synthetic lease requirement.", CreatedByUserId = users.Operations.Id });

        var document = new DocumentFile
        {
            CompoundId = residents[0].CompoundId,
            PropertyUnitId = residents[0].OccupancyRecords.First().PropertyUnitId,
            OriginalFileName = "demo-resident-id.pdf",
            StoredFileName = "demo-resident-id.pdf",
            ContentType = "application/pdf",
            Extension = "pdf",
            SizeInBytes = 2048,
            StoragePath = "App_Data/Uploads/Documents/demo/demo-resident-id.pdf",
            Category = DocumentCategory.ResidentIdentity,
            Visibility = DocumentVisibility.ResidentAndAdmin,
            OwnerUserId = residents[0].UserId,
            UploadedByUserId = users.Operations.Id,
            ApprovalStatus = DocumentApprovalStatus.Approved,
            ReviewedByUserId = users.Operations.Id,
            ReviewedAtUtc = DateTime.UtcNow.AddDays(-1),
            Description = "Demo metadata-only document."
        };
        document.AccessLogs.Add(new DocumentAccessLog { UserId = users.Operations.Id, Action = DocumentAccessAction.Uploaded });
        document.AccessLogs.Add(new DocumentAccessLog { UserId = residents[0].UserId, Action = DocumentAccessAction.Viewed, CreatedAtUtc = DateTime.UtcNow.AddHours(-4) });
        dbContext.DocumentFiles.Add(document);

        var approval = new ApprovalRequest
        {
            CompoundId = structure.River.Id,
            RequestedByUserId = users.Accountant.Id,
            LastDecisionByUserId = users.SuperAdmin.Id,
            ActionType = ApprovalActionType.ManualFinancialCorrection,
            EntityType = ApprovalEntityType.UtilityBill,
            Status = ApprovalStatus.Approved,
            Priority = ApprovalPriority.High,
            ExecutionStatus = ApprovalExecutionStatus.NotReady,
            Reason = "Demo financial correction approval.",
            DecisionReason = "Approved for demo evidence.",
            DecidedAtUtc = DateTime.UtcNow.AddHours(-3)
        };
        approval.Decisions.Add(new ApprovalDecision
        {
            DecidedByUserId = users.SuperAdmin.Id,
            DecisionType = ApprovalDecisionType.Approved,
            Reason = "Demo approval."
        });
        dbContext.ApprovalRequests.Add(approval);

        dbContext.SavedReports.Add(new SavedReport
        {
            CompoundId = structure.River.Id,
            CreatedByUserId = users.Operations.Id,
            ReportType = ManagementReportType.Financial,
            Visibility = SavedReportVisibility.FinanceTeam,
            Name = "Demo finance pressure report",
            Description = "Saved filter for finance demo.",
            FilterJson = "{\"period\":\"current-month\"}"
        });
        dbContext.ReportExportJobs.Add(new ReportExportJob
        {
            CompoundId = structure.River.Id,
            RequestedByUserId = users.Operations.Id,
            ReportType = ManagementReportType.Operations,
            Format = ReportExportFormat.Csv,
            Status = ReportExportJobStatus.Completed,
            FilterJson = "{\"demo\":true}",
            FileName = "demo-operations.csv",
            DownloadPath = "App_Data/Exports/Reports/demo/demo-operations.csv",
            CompletedAtUtc = DateTime.UtcNow.AddHours(-1)
        });

        dbContext.AuditLogEntries.AddRange(
            new AuditLogEntry
            {
                CompoundId = structure.River.Id,
                ActorUserId = users.Operations.Id,
                ActionType = AuditActionType.ReportExportCompleted,
                EntityType = AuditEntityType.ReportExportJob,
                Severity = AuditSeverity.Medium,
                SourceModule = "Reports",
                Description = "Demo report export completed."
            },
            new AuditLogEntry
            {
                CompoundId = structure.River.Id,
                ActorUserId = users.Operations.Id,
                ActionType = AuditActionType.PurchaseOrderReceived,
                EntityType = AuditEntityType.PurchaseOrder,
                EntityId = operations.WorkOrders[0].Id,
                Severity = AuditSeverity.High,
                SourceModule = "Procurement",
                Description = "Demo procurement audit trail entry."
            });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateDemoPassword(string password)
    {
        if (StartupSecurityValidator.IsPlaceholderSecret(password)
            || password.Length < 12
            || !password.Any(char.IsUpper)
            || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit)
            || !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException("DemoSeed:DemoPassword must be set locally and must be at least 12 characters with uppercase, lowercase, digit, and symbol.");
        }
    }

    private sealed record DemoUsers(
        ApplicationUser SuperAdmin,
        ApplicationUser CompoundAdmin,
        ApplicationUser Accountant,
        ApplicationUser Maintenance,
        ApplicationUser Guard,
        ApplicationUser Operations,
        IReadOnlyList<ApplicationUser> Residents);

    private sealed record DemoStructure(
        Compound River,
        Compound Garden,
        IReadOnlyList<Building> Buildings,
        IReadOnlyList<Floor> Floors,
        IReadOnlyList<PropertyUnit> Units)
    {
        public IReadOnlyList<Compound> Compounds => [River, Garden];
    }

    private sealed record DemoOperations(
        IReadOnlyList<StaffMember> Staff,
        IReadOnlyList<ServiceVendor> Vendors,
        IReadOnlyList<StockItem> Stock,
        IReadOnlyList<WorkOrder> WorkOrders);
}
