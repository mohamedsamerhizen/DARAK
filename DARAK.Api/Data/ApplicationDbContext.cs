using DARAK.Api.Entities;
using DARAK.Api.Enums;
using DARAK.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DARAK.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UserCompoundAssignment> UserCompoundAssignments => Set<UserCompoundAssignment>();

    public DbSet<Compound> Compounds => Set<Compound>();

    public DbSet<Building> Buildings => Set<Building>();

    public DbSet<Floor> Floors => Set<Floor>();

    public DbSet<PropertyUnit> PropertyUnits => Set<PropertyUnit>();

    public DbSet<ParkingSpot> ParkingSpots => Set<ParkingSpot>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<AnnouncementReadReceipt> AnnouncementReadReceipts => Set<AnnouncementReadReceipt>();

    public DbSet<UtilityOutage> UtilityOutages => Set<UtilityOutage>();

    public DbSet<UtilityOutageUpdate> UtilityOutageUpdates => Set<UtilityOutageUpdate>();

    public DbSet<ResidentNotificationPreference> ResidentNotificationPreferences => Set<ResidentNotificationPreference>();

    public DbSet<CommunicationCampaign> CommunicationCampaigns => Set<CommunicationCampaign>();

    public DbSet<CommunicationCampaignRecipient> CommunicationCampaignRecipients => Set<CommunicationCampaignRecipient>();

    public DbSet<ResidentNotification> ResidentNotifications => Set<ResidentNotification>();

    public DbSet<NotificationOutbox> NotificationOutboxes => Set<NotificationOutbox>();

    public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();

    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

    public DbSet<ApprovalPolicy> ApprovalPolicies => Set<ApprovalPolicy>();

    public DbSet<ResidentRiskFlag> ResidentRiskFlags => Set<ResidentRiskFlag>();

    public DbSet<ResidentRiskFlagAction> ResidentRiskFlagActions => Set<ResidentRiskFlagAction>();

    public DbSet<FinancialAdjustment> FinancialAdjustments => Set<FinancialAdjustment>();

    public DbSet<FinancialDispute> FinancialDisputes => Set<FinancialDispute>();

    public DbSet<PenaltyRule> PenaltyRules => Set<PenaltyRule>();

    public DbSet<CollectionCase> CollectionCases => Set<CollectionCase>();

    public DbSet<LegalNotice> LegalNotices => Set<LegalNotice>();

    public DbSet<ArbitrationCase> ArbitrationCases => Set<ArbitrationCase>();

    public DbSet<ArbitrationCaseEvent> ArbitrationCaseEvents => Set<ArbitrationCaseEvent>();

    public DbSet<PaymentPlan> PaymentPlans => Set<PaymentPlan>();

    public DbSet<PaymentPlanInstallment> PaymentPlanInstallments => Set<PaymentPlanInstallment>();

    public DbSet<ResidentLedgerEntry> ResidentLedgerEntries => Set<ResidentLedgerEntry>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public DbSet<AuditLogChange> AuditLogChanges => Set<AuditLogChange>();

    public DbSet<CommunityPoll> CommunityPolls => Set<CommunityPoll>();

    public DbSet<CommunityPollOption> CommunityPollOptions => Set<CommunityPollOption>();

    public DbSet<CommunityPollVote> CommunityPollVotes => Set<CommunityPollVote>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();

    public DbSet<ResidentProfile> ResidentProfiles => Set<ResidentProfile>();

    public DbSet<OccupancyRecord> OccupancyRecords => Set<OccupancyRecord>();

    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();

    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();

    public DbSet<CompoundService> CompoundServices => Set<CompoundService>();

    public DbSet<BillingCycle> BillingCycles => Set<BillingCycle>();

    public DbSet<UtilityBill> UtilityBills => Set<UtilityBill>();

    public DbSet<UtilityBillLine> UtilityBillLines => Set<UtilityBillLine>();

    public DbSet<Meter> Meters => Set<Meter>();

    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();

    public DbSet<SmartMeterDevice> SmartMeterDevices => Set<SmartMeterDevice>();

    public DbSet<SmartMeterReadingIngestion> SmartMeterReadingIngestions => Set<SmartMeterReadingIngestion>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    public DbSet<PaymentReconciliationBatch> PaymentReconciliationBatches => Set<PaymentReconciliationBatch>();

    public DbSet<PaymentReconciliationItem> PaymentReconciliationItems => Set<PaymentReconciliationItem>();

    public DbSet<Receipt> Receipts => Set<Receipt>();

    public DbSet<PropertySaleContract> PropertySaleContracts => Set<PropertySaleContract>();

    public DbSet<InstallmentScheduleItem> InstallmentScheduleItems => Set<InstallmentScheduleItem>();

    public DbSet<RentContract> RentContracts => Set<RentContract>();

    public DbSet<RentInvoice> RentInvoices => Set<RentInvoice>();

    public DbSet<VisitorPass> VisitorPasses => Set<VisitorPass>();

    public DbSet<VisitorAccessLog> VisitorAccessLogs => Set<VisitorAccessLog>();

    public DbSet<ContractorWorkPermit> ContractorWorkPermits => Set<ContractorWorkPermit>();

    public DbSet<ContractorAccessLog> ContractorAccessLogs => Set<ContractorAccessLog>();

    public DbSet<AccessCredential> AccessCredentials => Set<AccessCredential>();

    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();

    public DbSet<MaintenanceStatusHistory> MaintenanceStatusHistories => Set<MaintenanceStatusHistory>();

    public DbSet<Complaint> Complaints => Set<Complaint>();

    public DbSet<Violation> Violations => Set<Violation>();

    public DbSet<ViolationFine> ViolationFines => Set<ViolationFine>();

    public DbSet<ViolationAppeal> ViolationAppeals => Set<ViolationAppeal>();

    public DbSet<DocumentFile> DocumentFiles => Set<DocumentFile>();

    public DbSet<DocumentRequirement> DocumentRequirements => Set<DocumentRequirement>();

    public DbSet<DocumentAccessLog> DocumentAccessLogs => Set<DocumentAccessLog>();

    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();

    public DbSet<ServiceVendor> ServiceVendors => Set<ServiceVendor>();

    public DbSet<StockItem> StockItems => Set<StockItem>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<ProcurementRequest> ProcurementRequests => Set<ProcurementRequest>();

    public DbSet<ProcurementRequestItem> ProcurementRequestItems => Set<ProcurementRequestItem>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<MaintenanceAsset> MaintenanceAssets => Set<MaintenanceAsset>();

    public DbSet<MaintenanceSlaPolicy> MaintenanceSlaPolicies => Set<MaintenanceSlaPolicy>();

    public DbSet<PreventiveMaintenancePlan> PreventiveMaintenancePlans => Set<PreventiveMaintenancePlan>();

    public DbSet<OperationalChecklistTemplate> OperationalChecklistTemplates => Set<OperationalChecklistTemplate>();

    public DbSet<OperationalChecklistTemplateItem> OperationalChecklistTemplateItems => Set<OperationalChecklistTemplateItem>();

    public DbSet<OperationalChecklistRun> OperationalChecklistRuns => Set<OperationalChecklistRun>();

    public DbSet<OperationalChecklistRunItem> OperationalChecklistRunItems => Set<OperationalChecklistRunItem>();

    public DbSet<WorkOrderCostItem> WorkOrderCostItems => Set<WorkOrderCostItem>();

    public DbSet<WorkOrderStatusHistory> WorkOrderStatusHistories => Set<WorkOrderStatusHistory>();

    public DbSet<WorkOrderRating> WorkOrderRatings => Set<WorkOrderRating>();

    public DbSet<OperationalTask> OperationalTasks => Set<OperationalTask>();

    public DbSet<BillingRule> BillingRules => Set<BillingRule>();

    public DbSet<BillingRuleTier> BillingRuleTiers => Set<BillingRuleTier>();

    public DbSet<MeterReadingCorrection> MeterReadingCorrections => Set<MeterReadingCorrection>();

    public DbSet<ContractLifecycleEvent> ContractLifecycleEvents => Set<ContractLifecycleEvent>();

    public DbSet<UnitHandoverChecklist> UnitHandoverChecklists => Set<UnitHandoverChecklist>();

    public DbSet<UnitHandoverChecklistItem> UnitHandoverChecklistItems => Set<UnitHandoverChecklistItem>();

    public DbSet<ResidentLifecycleProcess> ResidentLifecycleProcesses => Set<ResidentLifecycleProcess>();

    public DbSet<ResidentCustodyItem> ResidentCustodyItems => Set<ResidentCustodyItem>();

    public DbSet<MoveLogisticsPermit> MoveLogisticsPermits => Set<MoveLogisticsPermit>();

    public DbSet<UnitReadinessRecord> UnitReadinessRecords => Set<UnitReadinessRecord>();

    public DbSet<UnitDamageLiability> UnitDamageLiabilities => Set<UnitDamageLiability>();

    public DbSet<OwnershipTransferRequest> OwnershipTransferRequests => Set<OwnershipTransferRequest>();

    public DbSet<InstallmentRescheduleRequest> InstallmentRescheduleRequests => Set<InstallmentRescheduleRequest>();

    public DbSet<SupportCase> SupportCases => Set<SupportCase>();

    public DbSet<SupportCaseEvent> SupportCaseEvents => Set<SupportCaseEvent>();

    public DbSet<SupportSlaPolicy> SupportSlaPolicies => Set<SupportSlaPolicy>();

    public DbSet<SavedReport> SavedReports => Set<SavedReport>();

    public DbSet<ReportExportJob> ReportExportJobs => Set<ReportExportJob>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public DbSet<LicenseProfile> LicenseProfiles => Set<LicenseProfile>();

    public DbSet<BackgroundJobRun> BackgroundJobRuns => Set<BackgroundJobRun>();

    public DbSet<SystemHealthSnapshot> SystemHealthSnapshots => Set<SystemHealthSnapshot>();

    public DbSet<IntegrationFailureEvent> IntegrationFailureEvents => Set<IntegrationFailureEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureRefreshToken(builder);
        ConfigureUserCompoundAssignment(builder);
        ConfigureCompound(builder);
        ConfigureBuilding(builder);
        ConfigureFloor(builder);
        ConfigurePropertyUnit(builder);
        ConfigureParkingSpot(builder);
        ConfigureAnnouncement(builder);
        ConfigureAnnouncementReadReceipt(builder);
        ConfigureUtilityOutage(builder);
        ConfigureUtilityOutageUpdate(builder);
        ConfigureResidentNotificationPreference(builder);
        ConfigureCommunicationCampaign(builder);
        ConfigureCommunicationCampaignRecipient(builder);
        ConfigureResidentNotification(builder);
        ConfigureNotificationOutbox(builder);
        ConfigureNotificationDeliveryAttempt(builder);
        ConfigureNotificationTemplate(builder);
        ConfigureApprovalRequest(builder);
        ConfigureApprovalDecision(builder);
        ConfigureApprovalPolicy(builder);
        ConfigureResidentRiskFlag(builder);
        ConfigureResidentRiskFlagAction(builder);
        ConfigureFinancialAdjustment(builder);
        ConfigureFinancialDispute(builder);
        ConfigurePenaltyRule(builder);
        ConfigureCollectionCase(builder);
        ConfigureLegalNotice(builder);
        ConfigureArbitrationCase(builder);
        ConfigureArbitrationCaseEvent(builder);
        ConfigurePaymentPlan(builder);
        ConfigurePaymentPlanInstallment(builder);
        ConfigureResidentLedgerEntry(builder);
        ConfigureAuditLogEntry(builder);
        ConfigureAuditLogChange(builder);
        ConfigureCommunityPoll(builder);
        ConfigureCommunityPollOption(builder);
        ConfigureCommunityPollVote(builder);
        ConfigureConversation(builder);
        ConfigureConversationMessage(builder);
        ConfigureActivityEvent(builder);
        ConfigureResidentProfile(builder);
        ConfigureOccupancyRecord(builder);
        ConfigureFamilyMember(builder);
        ConfigureEmergencyContact(builder);
        ConfigureCompoundService(builder);
        ConfigureBillingCycle(builder);
        ConfigureUtilityBill(builder);
        ConfigureUtilityBillLine(builder);
        ConfigureMeter(builder);
        ConfigureMeterReading(builder);
        ConfigureSmartMeterDevice(builder);
        ConfigureSmartMeterReadingIngestion(builder);
        ConfigurePayment(builder);
        ConfigurePaymentAttempt(builder);
        ConfigurePaymentReconciliationBatch(builder);
        ConfigurePaymentReconciliationItem(builder);
        ConfigureReceipt(builder);
        ConfigurePropertySaleContract(builder);
        ConfigureInstallmentScheduleItem(builder);
        ConfigureRentContract(builder);
        ConfigureRentInvoice(builder);
        ConfigureVisitorPass(builder);
        ConfigureVisitorAccessLog(builder);
        ConfigureContractorWorkPermit(builder);
        ConfigureContractorAccessLog(builder);
        ConfigureAccessCredential(builder);
        ConfigureMaintenanceRequest(builder);
        ConfigureMaintenanceStatusHistory(builder);
        ConfigureComplaint(builder);
        ConfigureViolation(builder);
        ConfigureViolationFine(builder);
        ConfigureViolationAppeal(builder);
        ConfigureDocumentFile(builder);
        ConfigureDocumentRequirement(builder);
        ConfigureDocumentAccessLog(builder);
        ConfigureStaffMember(builder);
        ConfigureServiceVendor(builder);
        ConfigureStockItem(builder);
        ConfigureInventoryMovement(builder);
        ConfigureProcurementRequest(builder);
        ConfigureProcurementRequestItem(builder);
        ConfigurePurchaseOrder(builder);
        ConfigurePurchaseOrderItem(builder);
        ConfigureWorkOrder(builder);
        ConfigureWorkOrderCostItem(builder);
        ConfigureWorkOrderStatusHistory(builder);
        ConfigureWorkOrderRating(builder);
        ConfigureMaintenanceAsset(builder);
        ConfigureMaintenanceSlaPolicy(builder);
        ConfigurePreventiveMaintenancePlan(builder);
        ConfigureOperationalChecklistTemplate(builder);
        ConfigureOperationalChecklistTemplateItem(builder);
        ConfigureOperationalChecklistRun(builder);
        ConfigureOperationalChecklistRunItem(builder);
        ConfigureOperationalTask(builder);
        ConfigureBillingRule(builder);
        ConfigureBillingRuleTier(builder);
        ConfigureMeterReadingCorrection(builder);
        ConfigureContractLifecycleEvent(builder);
        ConfigureUnitHandoverChecklist(builder);
        ConfigureUnitHandoverChecklistItem(builder);
        ConfigureResidentLifecycleProcess(builder);
        ConfigureResidentCustodyItem(builder);
        ConfigureMoveLogisticsPermit(builder);
        ConfigureUnitReadinessRecord(builder);
        ConfigureUnitDamageLiability(builder);
        ConfigureOwnershipTransferRequest(builder);
        ConfigureInstallmentRescheduleRequest(builder);
        ConfigureSupportCase(builder);
        ConfigureSupportCaseEvent(builder);
        ConfigureSupportSlaPolicy(builder);
        ConfigureSavedReport(builder);
        ConfigureReportExportJob(builder);

        ConfigureSystemSetting(builder);
        ConfigureLicenseProfile(builder);
        ConfigureBackgroundJobRun(builder);
        ConfigureSystemHealthSnapshot(builder);
        ConfigureIntegrationFailureEvent(builder);
    }

    private static void ConfigureRefreshToken(ModelBuilder builder)
    {
        builder.Entity<RefreshToken>(entity =>
       {
           entity.ToTable("RefreshTokens");
           entity.HasKey(refreshToken => refreshToken.Id);

           entity.Property(refreshToken => refreshToken.TokenHash)
               .IsRequired()
               .HasMaxLength(128);

           entity.Property(refreshToken => refreshToken.CreatedByIp)
               .HasMaxLength(128);

           entity.Property(refreshToken => refreshToken.RevokedByIp)
               .HasMaxLength(128);

           entity.Property(refreshToken => refreshToken.ReplacedByTokenHash)
               .HasMaxLength(128);

           entity.Property(refreshToken => refreshToken.RevokedReason)
               .HasMaxLength(256);

           entity.HasIndex(refreshToken => refreshToken.TokenHash)
               .IsUnique();

           entity.HasIndex(refreshToken => refreshToken.UserId);

           entity.HasOne(refreshToken => refreshToken.User)
               .WithMany(user => user.RefreshTokens)
               .HasForeignKey(refreshToken => refreshToken.UserId)
               .OnDelete(DeleteBehavior.Cascade);
       });
    }

    private static void ConfigureUserCompoundAssignment(ModelBuilder builder)
    {
        builder.Entity<UserCompoundAssignment>(entity =>
        {
            entity.ToTable("UserCompoundAssignments");
            entity.HasKey(assignment => assignment.Id);

            entity.HasIndex(assignment => assignment.UserId);
            entity.HasIndex(assignment => assignment.CompoundId);
            entity.HasIndex(assignment => assignment.Role);
            entity.HasIndex(assignment => new
                {
                    assignment.UserId,
                    assignment.CompoundId,
                    assignment.Role
                })
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            entity.HasOne(assignment => assignment.User)
                .WithMany(user => user.CompoundAssignments)
                .HasForeignKey(assignment => assignment.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(assignment => assignment.Compound)
                .WithMany()
                .HasForeignKey(assignment => assignment.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(assignment => assignment.CreatedByUser)
                .WithMany()
                .HasForeignKey(assignment => assignment.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCompound(ModelBuilder builder)
    {
        builder.Entity<Compound>(entity =>
        {
            entity.ToTable("Compounds");
            entity.HasKey(compound => compound.Id);

            entity.Property(compound => compound.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(compound => compound.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(compound => compound.Description)
                .HasMaxLength(1000);

            entity.Property(compound => compound.City)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(compound => compound.Area)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(compound => compound.Address)
                .HasMaxLength(300);

            entity.HasIndex(compound => compound.Code)
                .IsUnique();
        });
    }

    private static void ConfigureBuilding(ModelBuilder builder)
    {
        builder.Entity<Building>(entity =>
        {
            entity.ToTable("Buildings");
            entity.HasKey(building => building.Id);

            entity.Property(building => building.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(building => building.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(building => new { building.CompoundId, building.Code })
                .IsUnique();

            entity.HasOne(building => building.Compound)
                .WithMany(compound => compound.Buildings)
                .HasForeignKey(building => building.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureFloor(ModelBuilder builder)
    {
        builder.Entity<Floor>(entity =>
        {
            entity.ToTable("Floors");
            entity.HasKey(floor => floor.Id);

            entity.Property(floor => floor.Name)
                .HasMaxLength(100);

            entity.HasIndex(floor => new { floor.BuildingId, floor.FloorNumber })
                .IsUnique();

            entity.HasOne(floor => floor.Compound)
                .WithMany()
                .HasForeignKey(floor => floor.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(floor => floor.Building)
                .WithMany(building => building.Floors)
                .HasForeignKey(floor => floor.BuildingId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePropertyUnit(ModelBuilder builder)
    {
        builder.Entity<PropertyUnit>(entity =>
        {
            entity.ToTable("PropertyUnits");
            entity.HasKey(propertyUnit => propertyUnit.Id);

            entity.Property(propertyUnit => propertyUnit.UnitNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(propertyUnit => propertyUnit.AreaSquareMeters)
                .HasPrecision(18, 2);

            entity.Property(propertyUnit => propertyUnit.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(propertyUnit => new
                {
                    propertyUnit.CompoundId,
                    propertyUnit.BuildingId,
                    propertyUnit.UnitNumber
                })
                .IsUnique()
                .HasFilter(null);

            entity.HasOne(propertyUnit => propertyUnit.Compound)
                .WithMany(compound => compound.PropertyUnits)
                .HasForeignKey(propertyUnit => propertyUnit.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(propertyUnit => propertyUnit.Building)
                .WithMany(building => building.PropertyUnits)
                .HasForeignKey(propertyUnit => propertyUnit.BuildingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(propertyUnit => propertyUnit.Floor)
                .WithMany(floor => floor.PropertyUnits)
                .HasForeignKey(propertyUnit => propertyUnit.FloorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureParkingSpot(ModelBuilder builder)
    {
        builder.Entity<ParkingSpot>(entity =>
        {
            entity.ToTable("ParkingSpots");
            entity.HasKey(parkingSpot => parkingSpot.Id);

            entity.Property(parkingSpot => parkingSpot.SpotNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(parkingSpot => parkingSpot.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(parkingSpot => new { parkingSpot.CompoundId, parkingSpot.SpotNumber })
                .IsUnique();

            entity.HasOne(parkingSpot => parkingSpot.Compound)
                .WithMany(compound => compound.ParkingSpots)
                .HasForeignKey(parkingSpot => parkingSpot.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


    private static void ConfigureAnnouncement(ModelBuilder builder)
    {
        builder.Entity<Announcement>(entity =>
        {
            entity.ToTable("Announcements");
            entity.HasKey(announcement => announcement.Id);

            entity.Property(announcement => announcement.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(announcement => announcement.Body)
                .IsRequired()
                .HasMaxLength(4000);

            entity.HasIndex(announcement => announcement.Status);
            entity.HasIndex(announcement => announcement.CompoundId);
            entity.HasIndex(announcement => announcement.Category);
            entity.HasIndex(announcement => announcement.Priority);
            entity.HasIndex(announcement => announcement.ExpiresAt);
            entity.HasIndex(announcement => announcement.IsActive);

            entity.HasOne(announcement => announcement.Compound)
                .WithMany()
                .HasForeignKey(announcement => announcement.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(announcement => announcement.CreatedByUser)
                .WithMany()
                .HasForeignKey(announcement => announcement.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAnnouncementReadReceipt(ModelBuilder builder)
    {
        builder.Entity<AnnouncementReadReceipt>(entity =>
        {
            entity.ToTable("AnnouncementReadReceipts");
            entity.HasKey(receipt => receipt.Id);

            entity.HasIndex(receipt => new { receipt.AnnouncementId, receipt.UserId })
                .IsUnique();

            entity.HasOne(receipt => receipt.Announcement)
                .WithMany(announcement => announcement.ReadReceipts)
                .HasForeignKey(receipt => receipt.AnnouncementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(receipt => receipt.User)
                .WithMany()
                .HasForeignKey(receipt => receipt.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


    private static void ConfigureUtilityOutage(ModelBuilder builder)
    {
        builder.Entity<UtilityOutage>(entity =>
        {
            entity.ToTable("UtilityOutages");
            entity.HasKey(outage => outage.Id);

            entity.Property(outage => outage.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(outage => outage.Description)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(outage => outage.ResolutionNotes)
                .HasMaxLength(2000);

            entity.HasIndex(outage => outage.CompoundId);
            entity.HasIndex(outage => outage.BuildingId);
            entity.HasIndex(outage => outage.FloorId);
            entity.HasIndex(outage => outage.PropertyUnitId);
            entity.HasIndex(outage => outage.ServiceType);
            entity.HasIndex(outage => outage.Status);
            entity.HasIndex(outage => outage.Severity);
            entity.HasIndex(outage => outage.EstimatedStartAtUtc);
            entity.HasIndex(outage => outage.EstimatedEndAtUtc);

            entity.HasOne(outage => outage.Compound)
                .WithMany()
                .HasForeignKey(outage => outage.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(outage => outage.Building)
                .WithMany()
                .HasForeignKey(outage => outage.BuildingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(outage => outage.Floor)
                .WithMany()
                .HasForeignKey(outage => outage.FloorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(outage => outage.PropertyUnit)
                .WithMany()
                .HasForeignKey(outage => outage.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(outage => outage.Announcement)
                .WithMany()
                .HasForeignKey(outage => outage.AnnouncementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(outage => outage.CreatedByUser)
                .WithMany()
                .HasForeignKey(outage => outage.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(outage => outage.ResolvedByUser)
                .WithMany()
                .HasForeignKey(outage => outage.ResolvedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUtilityOutageUpdate(ModelBuilder builder)
    {
        builder.Entity<UtilityOutageUpdate>(entity =>
        {
            entity.ToTable("UtilityOutageUpdates");
            entity.HasKey(update => update.Id);

            entity.Property(update => update.Message)
                .IsRequired()
                .HasMaxLength(2000);

            entity.HasIndex(update => update.UtilityOutageId);
            entity.HasIndex(update => update.UpdateType);
            entity.HasIndex(update => update.CreatedAtUtc);

            entity.HasOne(update => update.UtilityOutage)
                .WithMany(outage => outage.Updates)
                .HasForeignKey(update => update.UtilityOutageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(update => update.CreatedByUser)
                .WithMany()
                .HasForeignKey(update => update.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureResidentNotificationPreference(ModelBuilder builder)
    {
        builder.Entity<ResidentNotificationPreference>(entity =>
        {
            entity.ToTable("ResidentNotificationPreferences");
            entity.HasKey(preference => preference.Id);

            entity.HasIndex(preference => preference.UserId)
                .IsUnique();

            entity.HasIndex(preference => preference.DoNotDisturbEnabled);

            entity.HasOne(preference => preference.User)
                .WithMany()
                .HasForeignKey(preference => preference.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCommunicationCampaign(ModelBuilder builder)
    {
        builder.Entity<CommunicationCampaign>(entity =>
        {
            entity.ToTable("CommunicationCampaigns");
            entity.HasKey(campaign => campaign.Id);

            entity.Property(campaign => campaign.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(campaign => campaign.Body)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(campaign => campaign.RowVersion)
                .IsRowVersion();

            entity.HasIndex(campaign => campaign.CompoundId);
            entity.HasIndex(campaign => campaign.Status);
            entity.HasIndex(campaign => campaign.TargetType);
            entity.HasIndex(campaign => campaign.CreatedAtUtc);
            entity.HasIndex(campaign => campaign.SentAtUtc);

            entity.HasOne(campaign => campaign.Compound)
                .WithMany()
                .HasForeignKey(campaign => campaign.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(campaign => campaign.CreatedByUser)
                .WithMany()
                .HasForeignKey(campaign => campaign.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCommunicationCampaignRecipient(ModelBuilder builder)
    {
        builder.Entity<CommunicationCampaignRecipient>(entity =>
        {
            entity.ToTable("CommunicationCampaignRecipients");
            entity.HasKey(recipient => recipient.Id);

            entity.Property(recipient => recipient.SuppressionReason)
                .HasMaxLength(300);

            entity.HasIndex(recipient => recipient.CampaignId);
            entity.HasIndex(recipient => recipient.ResidentProfileId);
            entity.HasIndex(recipient => recipient.UserId);
            entity.HasIndex(recipient => recipient.DeliverySuppressed);
            entity.HasIndex(recipient => new { recipient.CampaignId, recipient.ResidentProfileId })
                .IsUnique();

            entity.HasOne(recipient => recipient.Campaign)
                .WithMany(campaign => campaign.Recipients)
                .HasForeignKey(recipient => recipient.CampaignId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(recipient => recipient.ResidentProfile)
                .WithMany()
                .HasForeignKey(recipient => recipient.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(recipient => recipient.NotificationOutbox)
                .WithMany()
                .HasForeignKey(recipient => recipient.NotificationOutboxId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureResidentNotification(ModelBuilder builder)
    {
        builder.Entity<ResidentNotification>(entity =>
        {
            entity.ToTable("ResidentNotifications");
            entity.HasKey(notification => notification.Id);

            entity.Property(notification => notification.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(notification => notification.Message)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(notification => notification.RelatedEntityType)
                .HasMaxLength(100);

            entity.HasIndex(notification => notification.UserId);
            entity.HasIndex(notification => notification.IsRead);
            entity.HasIndex(notification => notification.CreatedAt);

            entity.HasOne(notification => notification.User)
                .WithMany()
                .HasForeignKey(notification => notification.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


    private static void ConfigureNotificationOutbox(ModelBuilder builder)
    {
        builder.Entity<NotificationOutbox>(entity =>
        {
            entity.ToTable("NotificationOutbox");
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.Id)
                .ValueGeneratedNever();

            entity.Property(notification => notification.RecipientName)
                .HasMaxLength(200);

            entity.Property(notification => notification.RecipientEmail)
                .HasMaxLength(256);

            entity.Property(notification => notification.RecipientPhoneNumber)
                .HasMaxLength(40);

            entity.Property(notification => notification.Subject)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(notification => notification.Body)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(notification => notification.MetadataJson)
                .HasMaxLength(4000);

            entity.Property(notification => notification.LastError)
                .HasMaxLength(1000);

            entity.Property(notification => notification.ProviderName)
                .HasMaxLength(100);

            entity.Property(notification => notification.ProviderMessageId)
                .HasMaxLength(300);

            entity.HasIndex(notification => notification.CompoundId);
            entity.HasIndex(notification => notification.ResidentProfileId);
            entity.HasIndex(notification => notification.RecipientUserId);
            entity.HasIndex(notification => notification.Channel);
            entity.HasIndex(notification => notification.EventType);
            entity.HasIndex(notification => notification.Status);
            entity.HasIndex(notification => notification.Priority);
            entity.HasIndex(notification => notification.ScheduledAtUtc);
            entity.HasIndex(notification => notification.NextRetryAtUtc);
            entity.HasIndex(notification => notification.CreatedAtUtc);
            entity.HasIndex(notification => new { notification.RelatedEntityType, notification.RelatedEntityId });
            entity.HasIndex(notification => new { notification.Status, notification.ScheduledAtUtc, notification.Priority });
            entity.HasIndex(notification => new { notification.Status, notification.NextRetryAtUtc });

            entity.HasOne(notification => notification.Compound)
                .WithMany()
                .HasForeignKey(notification => notification.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(notification => notification.ResidentProfile)
                .WithMany()
                .HasForeignKey(notification => notification.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(notification => notification.RecipientUser)
                .WithMany()
                .HasForeignKey(notification => notification.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(notification => notification.CreatedByUser)
                .WithMany()
                .HasForeignKey(notification => notification.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureNotificationDeliveryAttempt(ModelBuilder builder)
    {
        builder.Entity<NotificationDeliveryAttempt>(entity =>
        {
            entity.ToTable("NotificationDeliveryAttempts");
            entity.HasKey(attempt => attempt.Id);
            entity.Property(attempt => attempt.Id)
                .ValueGeneratedNever();

            entity.Property(attempt => attempt.ProviderName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(attempt => attempt.ProviderMessageId)
                .HasMaxLength(300);

            entity.Property(attempt => attempt.ErrorMessage)
                .HasMaxLength(1000);

            entity.HasIndex(attempt => attempt.NotificationOutboxId);
            entity.HasIndex(attempt => attempt.Status);
            entity.HasIndex(attempt => attempt.StartedAtUtc);

            entity.HasOne(attempt => attempt.NotificationOutbox)
                .WithMany(notification => notification.DeliveryAttempts)
                .HasForeignKey(attempt => attempt.NotificationOutboxId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureNotificationTemplate(ModelBuilder builder)
    {
        builder.Entity<NotificationTemplate>(entity =>
        {
            entity.ToTable("NotificationTemplates");
            entity.HasKey(template => template.Id);
            entity.Property(template => template.Id)
                .ValueGeneratedNever();

            entity.Property(template => template.Code)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(template => template.SubjectTemplate)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(template => template.BodyTemplate)
                .IsRequired()
                .HasMaxLength(4000);

            entity.HasIndex(template => template.Code)
                .IsUnique();

            entity.HasIndex(template => template.Channel);
            entity.HasIndex(template => template.EventType);
            entity.HasIndex(template => template.IsActive);
        });
    }

    private static void ConfigureApprovalRequest(ModelBuilder builder)
    {
        builder.Entity<ApprovalRequest>(entity =>
        {
            entity.ToTable("ApprovalRequests");
            entity.HasKey(approval => approval.Id);
            entity.Property(approval => approval.Id)
                .ValueGeneratedNever();

            entity.Property(approval => approval.Reason)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(approval => approval.RequestPayloadJson)
                .HasMaxLength(8000);

            entity.Property(approval => approval.DecisionReason)
                .HasMaxLength(1000);

            entity.Property(approval => approval.ExecutionNotes)
                .HasMaxLength(1000);

            entity.Property(approval => approval.RowVersion)
                .IsRowVersion();

            entity.HasIndex(approval => approval.CompoundId);
            entity.HasIndex(approval => approval.RequestedByUserId);
            entity.HasIndex(approval => approval.LastDecisionByUserId);
            entity.HasIndex(approval => approval.ActionType);
            entity.HasIndex(approval => approval.Status);
            entity.HasIndex(approval => approval.Priority);
            entity.HasIndex(approval => approval.ExecutionStatus);
            entity.HasIndex(approval => approval.CreatedAtUtc);
            entity.HasIndex(approval => approval.DueAtUtc);
            entity.HasIndex(approval => new { approval.EntityType, approval.EntityId });
            entity.HasIndex(approval => new { approval.CompoundId, approval.Status, approval.Priority });

            entity.HasOne(approval => approval.Compound)
                .WithMany()
                .HasForeignKey(approval => approval.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(approval => approval.RequestedByUser)
                .WithMany()
                .HasForeignKey(approval => approval.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(approval => approval.LastDecisionByUser)
                .WithMany()
                .HasForeignKey(approval => approval.LastDecisionByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(approval => approval.ExecutedByUser)
                .WithMany()
                .HasForeignKey(approval => approval.ExecutedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureApprovalDecision(ModelBuilder builder)
    {
        builder.Entity<ApprovalDecision>(entity =>
        {
            entity.ToTable("ApprovalDecisions");
            entity.HasKey(decision => decision.Id);
            entity.Property(decision => decision.Id)
                .ValueGeneratedNever();

            entity.Property(decision => decision.Reason)
                .IsRequired()
                .HasMaxLength(1000);

            entity.HasIndex(decision => decision.ApprovalRequestId);
            entity.HasIndex(decision => decision.DecidedByUserId);
            entity.HasIndex(decision => decision.DecisionType);
            entity.HasIndex(decision => decision.CreatedAtUtc);

            entity.HasOne(decision => decision.ApprovalRequest)
                .WithMany(approval => approval.Decisions)
                .HasForeignKey(decision => decision.ApprovalRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(decision => decision.DecidedByUser)
                .WithMany()
                .HasForeignKey(decision => decision.DecidedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureApprovalPolicy(ModelBuilder builder)
    {
        builder.Entity<ApprovalPolicy>(entity =>
        {
            entity.ToTable("ApprovalPolicies");
            entity.HasKey(policy => policy.Id);
            entity.Property(policy => policy.Id)
                .ValueGeneratedNever();

            entity.Property(policy => policy.RequiredApproverRoles)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(policy => policy.Description)
                .HasMaxLength(1000);

            entity.HasIndex(policy => policy.CompoundId);
            entity.HasIndex(policy => policy.ActionType);
            entity.HasIndex(policy => policy.IsEnabled);
            entity.HasIndex(policy => new { policy.CompoundId, policy.ActionType })
                .IsUnique();

            entity.HasOne(policy => policy.Compound)
                .WithMany()
                .HasForeignKey(policy => policy.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


    private static void ConfigureResidentRiskFlag(ModelBuilder builder)
    {
        builder.Entity<ResidentRiskFlag>(entity =>
        {
            entity.ToTable("ResidentRiskFlags");
            entity.HasKey(riskFlag => riskFlag.Id);
            entity.Property(riskFlag => riskFlag.Id)
                .ValueGeneratedNever();

            entity.Property(riskFlag => riskFlag.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(riskFlag => riskFlag.Description)
                .IsRequired()
                .HasMaxLength(1500);

            entity.Property(riskFlag => riskFlag.RecommendedAction)
                .HasMaxLength(1000);

            entity.Property(riskFlag => riskFlag.InternalNotes)
                .HasMaxLength(2000);

            entity.Property(riskFlag => riskFlag.ResolutionNotes)
                .HasMaxLength(1000);

            entity.Property(riskFlag => riskFlag.DismissalReason)
                .HasMaxLength(1000);

            entity.Property(riskFlag => riskFlag.MetadataJson)
                .HasMaxLength(4000);

            entity.HasIndex(riskFlag => riskFlag.CompoundId);
            entity.HasIndex(riskFlag => riskFlag.ResidentProfileId);
            entity.HasIndex(riskFlag => riskFlag.PropertyUnitId);
            entity.HasIndex(riskFlag => riskFlag.CreatedByUserId);
            entity.HasIndex(riskFlag => riskFlag.AssignedToUserId);
            entity.HasIndex(riskFlag => riskFlag.FlagType);
            entity.HasIndex(riskFlag => riskFlag.Severity);
            entity.HasIndex(riskFlag => riskFlag.Status);
            entity.HasIndex(riskFlag => riskFlag.Source);
            entity.HasIndex(riskFlag => riskFlag.RequiresSupervisorReview);
            entity.HasIndex(riskFlag => riskFlag.NextReviewAtUtc);
            entity.HasIndex(riskFlag => riskFlag.ExpiresAtUtc);
            entity.HasIndex(riskFlag => riskFlag.CreatedAtUtc);
            entity.HasIndex(riskFlag => new { riskFlag.SourceEntityType, riskFlag.SourceEntityId });
            entity.HasIndex(riskFlag => new { riskFlag.CompoundId, riskFlag.Status, riskFlag.Severity });
            entity.HasIndex(riskFlag => new { riskFlag.ResidentProfileId, riskFlag.Status, riskFlag.Severity });

            entity.HasOne(riskFlag => riskFlag.Compound)
                .WithMany()
                .HasForeignKey(riskFlag => riskFlag.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(riskFlag => riskFlag.ResidentProfile)
                .WithMany()
                .HasForeignKey(riskFlag => riskFlag.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(riskFlag => riskFlag.PropertyUnit)
                .WithMany()
                .HasForeignKey(riskFlag => riskFlag.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(riskFlag => riskFlag.CreatedByUser)
                .WithMany()
                .HasForeignKey(riskFlag => riskFlag.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(riskFlag => riskFlag.AssignedToUser)
                .WithMany()
                .HasForeignKey(riskFlag => riskFlag.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(riskFlag => riskFlag.LastReviewedByUser)
                .WithMany()
                .HasForeignKey(riskFlag => riskFlag.LastReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(riskFlag => riskFlag.ClosedByUser)
                .WithMany()
                .HasForeignKey(riskFlag => riskFlag.ClosedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureResidentRiskFlagAction(ModelBuilder builder)
    {
        builder.Entity<ResidentRiskFlagAction>(entity =>
        {
            entity.ToTable("ResidentRiskFlagActions");
            entity.HasKey(action => action.Id);
            entity.Property(action => action.Id)
                .ValueGeneratedNever();

            entity.Property(action => action.Notes)
                .IsRequired()
                .HasMaxLength(1000);

            entity.HasIndex(action => action.ResidentRiskFlagId);
            entity.HasIndex(action => action.ActorUserId);
            entity.HasIndex(action => action.ActionType);
            entity.HasIndex(action => action.CreatedAtUtc);

            entity.HasOne(action => action.ResidentRiskFlag)
                .WithMany(riskFlag => riskFlag.Actions)
                .HasForeignKey(action => action.ResidentRiskFlagId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(action => action.ActorUser)
                .WithMany()
                .HasForeignKey(action => action.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureFinancialAdjustment(ModelBuilder builder)
    {
        builder.Entity<FinancialAdjustment>(entity =>
        {
            entity.ToTable("FinancialAdjustments");
            entity.HasKey(adjustment => adjustment.Id);

            entity.Property(adjustment => adjustment.Amount)
                .HasPrecision(18, 2);

            entity.Property(adjustment => adjustment.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(adjustment => adjustment.Reason)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(adjustment => adjustment.CancellationReason)
                .HasMaxLength(1000);

            entity.Property(adjustment => adjustment.RowVersion)
                .IsRowVersion();

            entity.HasIndex(adjustment => adjustment.CompoundId);
            entity.HasIndex(adjustment => adjustment.ResidentProfileId);
            entity.HasIndex(adjustment => adjustment.Status);
            entity.HasIndex(adjustment => adjustment.AdjustmentType);
            entity.HasIndex(adjustment => adjustment.ApprovalRequestId);
            entity.HasIndex(adjustment => adjustment.CreatedAtUtc);
            entity.HasIndex(adjustment => new { adjustment.CompoundId, adjustment.Status, adjustment.CreatedAtUtc });

            entity.HasOne(adjustment => adjustment.Compound)
                .WithMany()
                .HasForeignKey(adjustment => adjustment.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(adjustment => adjustment.ResidentProfile)
                .WithMany()
                .HasForeignKey(adjustment => adjustment.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(adjustment => adjustment.RequestedByUser)
                .WithMany()
                .HasForeignKey(adjustment => adjustment.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(adjustment => adjustment.AppliedByUser)
                .WithMany()
                .HasForeignKey(adjustment => adjustment.AppliedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(adjustment => adjustment.CancelledByUser)
                .WithMany()
                .HasForeignKey(adjustment => adjustment.CancelledByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(adjustment => adjustment.ApprovalRequest)
                .WithMany()
                .HasForeignKey(adjustment => adjustment.ApprovalRequestId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


    private static void ConfigureFinancialDispute(ModelBuilder builder)
    {
        builder.Entity<FinancialDispute>(entity =>
        {
            entity.ToTable("FinancialDisputes");
            entity.HasKey(dispute => dispute.Id);

            entity.Property(dispute => dispute.Reason)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(dispute => dispute.ResidentMessage)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(dispute => dispute.AdminDecisionNotes)
                .HasMaxLength(2000);

            entity.Property(dispute => dispute.ResolutionSummary)
                .HasMaxLength(2000);

            entity.Property(dispute => dispute.RowVersion)
                .IsRowVersion();

            entity.HasIndex(dispute => dispute.CompoundId);
            entity.HasIndex(dispute => dispute.ResidentProfileId);
            entity.HasIndex(dispute => dispute.Status);
            entity.HasIndex(dispute => dispute.FinancialAdjustmentId)
                .HasFilter("[FinancialAdjustmentId] IS NOT NULL");
            entity.HasIndex(dispute => new { dispute.TargetType, dispute.TargetId });
            entity.HasIndex(dispute => new { dispute.CompoundId, dispute.Status, dispute.CreatedAtUtc });
            entity.HasIndex(dispute => new { dispute.CompoundId, dispute.ResidentProfileId, dispute.TargetType, dispute.TargetId, dispute.Status });

            entity.HasOne(dispute => dispute.Compound)
                .WithMany()
                .HasForeignKey(dispute => dispute.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(dispute => dispute.ResidentProfile)
                .WithMany()
                .HasForeignKey(dispute => dispute.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(dispute => dispute.Conversation)
                .WithMany()
                .HasForeignKey(dispute => dispute.ConversationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(dispute => dispute.FinancialAdjustment)
                .WithMany()
                .HasForeignKey(dispute => dispute.FinancialAdjustmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(dispute => dispute.CreatedByUser)
                .WithMany()
                .HasForeignKey(dispute => dispute.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(dispute => dispute.ReviewedByUser)
                .WithMany()
                .HasForeignKey(dispute => dispute.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(dispute => dispute.ResolvedByUser)
                .WithMany()
                .HasForeignKey(dispute => dispute.ResolvedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(dispute => dispute.CancelledByUser)
                .WithMany()
                .HasForeignKey(dispute => dispute.CancelledByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePenaltyRule(ModelBuilder builder)
    {
        builder.Entity<PenaltyRule>(entity =>
        {
            entity.ToTable("PenaltyRules");
            entity.HasKey(rule => rule.Id);

            entity.Property(rule => rule.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(rule => rule.Amount)
                .HasPrecision(18, 2);

            entity.Property(rule => rule.PercentageRate)
                .HasPrecision(9, 4);

            entity.Property(rule => rule.MaxAmount)
                .HasPrecision(18, 2);

            entity.HasIndex(rule => rule.CompoundId);
            entity.HasIndex(rule => rule.TargetType);
            entity.HasIndex(rule => rule.Status);
            entity.HasIndex(rule => new { rule.CompoundId, rule.Name, rule.TargetType });

            entity.HasOne(rule => rule.Compound)
                .WithMany()
                .HasForeignKey(rule => rule.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(rule => rule.CreatedByUser)
                .WithMany()
                .HasForeignKey(rule => rule.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCollectionCase(ModelBuilder builder)
    {
        builder.Entity<CollectionCase>(entity =>
        {
            entity.ToTable("CollectionCases");
            entity.HasKey(collectionCase => collectionCase.Id);

            entity.Property(collectionCase => collectionCase.AmountDue)
                .HasPrecision(18, 2);

            entity.Property(collectionCase => collectionCase.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(collectionCase => collectionCase.Reason)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(collectionCase => collectionCase.Notes)
                .HasMaxLength(4000);

            entity.HasIndex(collectionCase => collectionCase.CompoundId);
            entity.HasIndex(collectionCase => collectionCase.ResidentProfileId);
            entity.HasIndex(collectionCase => collectionCase.Status);
            entity.HasIndex(collectionCase => collectionCase.Stage);
            entity.HasIndex(collectionCase => new { collectionCase.SourceType, collectionCase.SourceId });
            entity.HasIndex(collectionCase => new { collectionCase.CompoundId, collectionCase.Status, collectionCase.Stage });

            entity.HasOne(collectionCase => collectionCase.Compound)
                .WithMany()
                .HasForeignKey(collectionCase => collectionCase.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(collectionCase => collectionCase.ResidentProfile)
                .WithMany()
                .HasForeignKey(collectionCase => collectionCase.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(collectionCase => collectionCase.AssignedToUser)
                .WithMany()
                .HasForeignKey(collectionCase => collectionCase.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(collectionCase => collectionCase.CreatedByUser)
                .WithMany()
                .HasForeignKey(collectionCase => collectionCase.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(collectionCase => collectionCase.ClosedByUser)
                .WithMany()
                .HasForeignKey(collectionCase => collectionCase.ClosedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLegalNotice(ModelBuilder builder)
    {
        builder.Entity<LegalNotice>(entity =>
        {
            entity.ToTable("LegalNotices");
            entity.HasKey(notice => notice.Id);

            entity.Property(notice => notice.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(notice => notice.Body)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(notice => notice.DeliveryChannel)
                .HasMaxLength(80);

            entity.Property(notice => notice.DeliveryReference)
                .HasMaxLength(160);

            entity.HasIndex(notice => notice.CompoundId);
            entity.HasIndex(notice => notice.ResidentProfileId);
            entity.HasIndex(notice => notice.CollectionCaseId);
            entity.HasIndex(notice => notice.Status);
            entity.HasIndex(notice => notice.NoticeType);
            entity.HasIndex(notice => new { notice.CompoundId, notice.Status, notice.CreatedAtUtc });

            entity.HasOne(notice => notice.Compound)
                .WithMany()
                .HasForeignKey(notice => notice.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(notice => notice.ResidentProfile)
                .WithMany()
                .HasForeignKey(notice => notice.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(notice => notice.CollectionCase)
                .WithMany(collectionCase => collectionCase.LegalNotices)
                .HasForeignKey(notice => notice.CollectionCaseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(notice => notice.CreatedByUser)
                .WithMany()
                .HasForeignKey(notice => notice.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(notice => notice.IssuedByUser)
                .WithMany()
                .HasForeignKey(notice => notice.IssuedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


    private static void ConfigureArbitrationCase(ModelBuilder builder)
    {
        builder.Entity<ArbitrationCase>(entity =>
        {
            entity.ToTable("ArbitrationCases");
            entity.HasKey(arbitrationCase => arbitrationCase.Id);

            entity.Property(arbitrationCase => arbitrationCase.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(arbitrationCase => arbitrationCase.Reason)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(arbitrationCase => arbitrationCase.FinalDecision)
                .HasMaxLength(2000);

            entity.Property(arbitrationCase => arbitrationCase.FinalDecisionSummary)
                .HasMaxLength(4000);

            entity.Property(arbitrationCase => arbitrationCase.RowVersion)
                .IsRowVersion();

            entity.HasIndex(arbitrationCase => arbitrationCase.CompoundId);
            entity.HasIndex(arbitrationCase => arbitrationCase.ResidentProfileId);
            entity.HasIndex(arbitrationCase => arbitrationCase.Status);
            entity.HasIndex(arbitrationCase => arbitrationCase.Priority);
            entity.HasIndex(arbitrationCase => new { arbitrationCase.SourceType, arbitrationCase.SourceId });
            entity.HasIndex(arbitrationCase => new { arbitrationCase.CompoundId, arbitrationCase.Status, arbitrationCase.CreatedAtUtc });

            entity.HasOne(arbitrationCase => arbitrationCase.Compound)
                .WithMany()
                .HasForeignKey(arbitrationCase => arbitrationCase.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(arbitrationCase => arbitrationCase.ResidentProfile)
                .WithMany()
                .HasForeignKey(arbitrationCase => arbitrationCase.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(arbitrationCase => arbitrationCase.CreatedByUser)
                .WithMany()
                .HasForeignKey(arbitrationCase => arbitrationCase.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(arbitrationCase => arbitrationCase.DecidedByUser)
                .WithMany()
                .HasForeignKey(arbitrationCase => arbitrationCase.DecidedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(arbitrationCase => arbitrationCase.CancelledByUser)
                .WithMany()
                .HasForeignKey(arbitrationCase => arbitrationCase.CancelledByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureArbitrationCaseEvent(ModelBuilder builder)
    {
        builder.Entity<ArbitrationCaseEvent>(entity =>
        {
            entity.ToTable("ArbitrationCaseEvents");
            entity.HasKey(arbitrationEvent => arbitrationEvent.Id);

            entity.Property(arbitrationEvent => arbitrationEvent.Message)
                .IsRequired()
                .HasMaxLength(4000);

            entity.HasIndex(arbitrationEvent => arbitrationEvent.ArbitrationCaseId);
            entity.HasIndex(arbitrationEvent => arbitrationEvent.EventType);
            entity.HasIndex(arbitrationEvent => arbitrationEvent.CreatedAtUtc);

            entity.HasOne(arbitrationEvent => arbitrationEvent.ArbitrationCase)
                .WithMany(arbitrationCase => arbitrationCase.Events)
                .HasForeignKey(arbitrationEvent => arbitrationEvent.ArbitrationCaseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(arbitrationEvent => arbitrationEvent.CreatedByUser)
                .WithMany()
                .HasForeignKey(arbitrationEvent => arbitrationEvent.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePaymentPlan(ModelBuilder builder)
    {
        builder.Entity<PaymentPlan>(entity =>
        {
            entity.ToTable("PaymentPlans");
            entity.HasKey(plan => plan.Id);

            entity.Property(plan => plan.TotalAmount)
                .HasPrecision(18, 2);

            entity.Property(plan => plan.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(plan => plan.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(plan => plan.CompoundId);
            entity.HasIndex(plan => plan.ResidentProfileId);
            entity.HasIndex(plan => plan.CollectionCaseId);
            entity.HasIndex(plan => plan.Status);

            entity.HasOne(plan => plan.Compound)
                .WithMany()
                .HasForeignKey(plan => plan.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(plan => plan.ResidentProfile)
                .WithMany()
                .HasForeignKey(plan => plan.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(plan => plan.CollectionCase)
                .WithMany(collectionCase => collectionCase.PaymentPlans)
                .HasForeignKey(plan => plan.CollectionCaseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(plan => plan.CreatedByUser)
                .WithMany()
                .HasForeignKey(plan => plan.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePaymentPlanInstallment(ModelBuilder builder)
    {
        builder.Entity<PaymentPlanInstallment>(entity =>
        {
            entity.ToTable("PaymentPlanInstallments");
            entity.HasKey(installment => installment.Id);

            entity.Property(installment => installment.Amount)
                .HasPrecision(18, 2);

            entity.Property(installment => installment.PaidAmount)
                .HasPrecision(18, 2);

            entity.HasIndex(installment => installment.PaymentPlanId);
            entity.HasIndex(installment => installment.Status);
            entity.HasIndex(installment => installment.DueDate);

            entity.HasOne(installment => installment.PaymentPlan)
                .WithMany(plan => plan.Installments)
                .HasForeignKey(installment => installment.PaymentPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureResidentLedgerEntry(ModelBuilder builder)
    {
        builder.Entity<ResidentLedgerEntry>(entity =>
        {
            entity.ToTable("ResidentLedgerEntries");
            entity.HasKey(entry => entry.Id);

            entity.Property(entry => entry.Amount)
                .HasPrecision(18, 2);

            entity.Property(entry => entry.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(entry => entry.Reference)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(entry => entry.Description)
                .IsRequired()
                .HasMaxLength(1000);

            entity.HasIndex(entry => entry.CompoundId);
            entity.HasIndex(entry => entry.ResidentProfileId);
            entity.HasIndex(entry => entry.Direction);
            entity.HasIndex(entry => entry.SourceType);
            entity.HasIndex(entry => entry.SourceId);
            entity.HasIndex(entry => entry.OccurredAtUtc);
            entity.HasIndex(entry => new { entry.CompoundId, entry.ResidentProfileId, entry.OccurredAtUtc });
            entity.HasIndex(entry => entry.FinancialAdjustmentId)
                .IsUnique()
                .HasFilter("[FinancialAdjustmentId] IS NOT NULL");

            entity.HasOne(entry => entry.Compound)
                .WithMany()
                .HasForeignKey(entry => entry.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(entry => entry.ResidentProfile)
                .WithMany()
                .HasForeignKey(entry => entry.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(entry => entry.FinancialAdjustment)
                .WithMany(adjustment => adjustment.LedgerEntries)
                .HasForeignKey(entry => entry.FinancialAdjustmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(entry => entry.CreatedByUser)
                .WithMany()
                .HasForeignKey(entry => entry.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditLogEntry(ModelBuilder builder)
    {
        builder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("AuditLogEntries");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Id)
                .ValueGeneratedNever();

            entity.Property(log => log.ActorRole)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(log => log.SourceModule)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(log => log.Description)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(log => log.Reason)
                .HasMaxLength(1000);

            entity.Property(log => log.IpAddress)
                .HasMaxLength(64);

            entity.Property(log => log.UserAgent)
                .HasMaxLength(512);

            entity.Property(log => log.CorrelationId)
                .HasMaxLength(120);

            entity.Property(log => log.BeforeValuesJson)
                .HasMaxLength(8000);

            entity.Property(log => log.AfterValuesJson)
                .HasMaxLength(8000);

            entity.Property(log => log.MetadataJson)
                .HasMaxLength(8000);

            entity.HasIndex(log => log.CompoundId);
            entity.HasIndex(log => log.ResidentProfileId);
            entity.HasIndex(log => log.ActorUserId);
            entity.HasIndex(log => log.ActionType);
            entity.HasIndex(log => log.EntityType);
            entity.HasIndex(log => log.EntityId);
            entity.HasIndex(log => log.Severity);
            entity.HasIndex(log => log.SourceModule);
            entity.HasIndex(log => log.CreatedAtUtc);
            entity.HasIndex(log => new { log.CompoundId, log.CreatedAtUtc });
            entity.HasIndex(log => new { log.EntityType, log.EntityId, log.CreatedAtUtc });
            entity.HasIndex(log => new { log.ResidentProfileId, log.CreatedAtUtc });

            entity.HasOne(log => log.Compound)
                .WithMany()
                .HasForeignKey(log => log.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(log => log.ResidentProfile)
                .WithMany()
                .HasForeignKey(log => log.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(log => log.ActorUser)
                .WithMany()
                .HasForeignKey(log => log.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditLogChange(ModelBuilder builder)
    {
        builder.Entity<AuditLogChange>(entity =>
        {
            entity.ToTable("AuditLogChanges");
            entity.HasKey(change => change.Id);
            entity.Property(change => change.Id)
                .ValueGeneratedNever();

            entity.Property(change => change.PropertyName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(change => change.OldValue)
                .HasMaxLength(2000);

            entity.Property(change => change.NewValue)
                .HasMaxLength(2000);

            entity.HasIndex(change => change.AuditLogEntryId);
            entity.HasIndex(change => change.PropertyName);

            entity.HasOne(change => change.AuditLogEntry)
                .WithMany(log => log.Changes)
                .HasForeignKey(change => change.AuditLogEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCommunityPoll(ModelBuilder builder)
    {
        builder.Entity<CommunityPoll>(entity =>
        {
            entity.ToTable("CommunityPolls");
            entity.HasKey(poll => poll.Id);

            entity.Property(poll => poll.Question)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(poll => poll.Description)
                .HasMaxLength(1000);

            entity.HasIndex(poll => poll.Status);
            entity.HasIndex(poll => poll.CompoundId);
            entity.HasIndex(poll => poll.StartsAt);
            entity.HasIndex(poll => poll.EndsAt);

            entity.HasOne(poll => poll.Compound)
                .WithMany()
                .HasForeignKey(poll => poll.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(poll => poll.CreatedByUser)
                .WithMany()
                .HasForeignKey(poll => poll.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCommunityPollOption(ModelBuilder builder)
    {
        builder.Entity<CommunityPollOption>(entity =>
        {
            entity.ToTable("CommunityPollOptions");
            entity.HasKey(option => option.Id);

            entity.Property(option => option.Text)
                .IsRequired()
                .HasMaxLength(300);

            entity.HasIndex(option => option.PollId);
            entity.HasIndex(option => new { option.PollId, option.DisplayOrder });

            entity.HasOne(option => option.Poll)
                .WithMany(poll => poll.Options)
                .HasForeignKey(option => option.PollId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCommunityPollVote(ModelBuilder builder)
    {
        builder.Entity<CommunityPollVote>(entity =>
        {
            entity.ToTable("CommunityPollVotes");
            entity.HasKey(vote => vote.Id);

            entity.HasIndex(vote => vote.PollId);
            entity.HasIndex(vote => vote.UserId);
            entity.HasIndex(vote => new { vote.PollId, vote.UserId });
            entity.HasIndex(vote => new
                {
                    vote.PollId,
                    vote.PollOptionId,
                    vote.UserId
                })
                .IsUnique();

            entity.HasOne(vote => vote.Poll)
                .WithMany(poll => poll.Votes)
                .HasForeignKey(vote => vote.PollId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(vote => vote.PollOption)
                .WithMany(option => option.Votes)
                .HasForeignKey(vote => vote.PollOptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(vote => vote.User)
                .WithMany()
                .HasForeignKey(vote => vote.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


    private static void ConfigureConversation(ModelBuilder builder)
    {
        builder.Entity<Conversation>(entity =>
        {
            entity.ToTable("Conversations");
            entity.HasKey(conversation => conversation.Id);

            entity.Property(conversation => conversation.Id)
                .ValueGeneratedNever();

            entity.Property(conversation => conversation.LastAssignmentReason)
                .HasMaxLength(500);

            entity.Property(conversation => conversation.EscalationReason)
                .HasMaxLength(500);

            entity.Property(conversation => conversation.LastReopenReason)
                .HasMaxLength(500);

            entity.HasIndex(conversation => conversation.CompoundId);
            entity.HasIndex(conversation => conversation.ResidentProfileId);
            entity.HasIndex(conversation => conversation.PropertyUnitId);
            entity.HasIndex(conversation => conversation.Status);
            entity.HasIndex(conversation => conversation.Priority);
            entity.HasIndex(conversation => conversation.Topic);
            entity.HasIndex(conversation => conversation.IssueType);
            entity.HasIndex(conversation => conversation.AssignedToUserId);
            entity.HasIndex(conversation => conversation.EscalationLevel);
            entity.HasIndex(conversation => conversation.CreatedAtUtc);
            entity.HasIndex(conversation => conversation.LastMessageAtUtc);
            entity.HasIndex(conversation => new { conversation.LinkedEntityType, conversation.LinkedEntityId });

            entity.HasOne(conversation => conversation.Compound)
                .WithMany()
                .HasForeignKey(conversation => conversation.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(conversation => conversation.ResidentProfile)
                .WithMany()
                .HasForeignKey(conversation => conversation.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(conversation => conversation.PropertyUnit)
                .WithMany()
                .HasForeignKey(conversation => conversation.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(conversation => conversation.AssignedToUser)
                .WithMany()
                .HasForeignKey(conversation => conversation.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(conversation => conversation.AssignedByUser)
                .WithMany()
                .HasForeignKey(conversation => conversation.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(conversation => conversation.EscalatedByUser)
                .WithMany()
                .HasForeignKey(conversation => conversation.EscalatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(conversation => conversation.ReopenedByResident)
                .WithMany()
                .HasForeignKey(conversation => conversation.ReopenedByResidentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureConversationMessage(ModelBuilder builder)
    {
        builder.Entity<ConversationMessage>(entity =>
        {
            entity.ToTable("ConversationMessages");
            entity.HasKey(message => message.Id);

            entity.Property(message => message.Id)
                .ValueGeneratedNever();

            entity.Property(message => message.Body)
                .IsRequired()
                .HasMaxLength(4000);

            entity.HasIndex(message => message.ConversationId);
            entity.HasIndex(message => message.SenderUserId);
            entity.HasIndex(message => message.MessageType);
            entity.HasIndex(message => message.Visibility);
            entity.HasIndex(message => message.CreatedAtUtc);

            entity.HasOne(message => message.Conversation)
                .WithMany(conversation => conversation.Messages)
                .HasForeignKey(message => message.ConversationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(message => message.SenderUser)
                .WithMany()
                .HasForeignKey(message => message.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureActivityEvent(ModelBuilder builder)
    {
        builder.Entity<ActivityEvent>(entity =>
        {
            entity.ToTable("ActivityEvents");
            entity.HasKey(activityEvent => activityEvent.Id);

            entity.Property(activityEvent => activityEvent.Id)
                .ValueGeneratedNever();

            entity.Property(activityEvent => activityEvent.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(activityEvent => activityEvent.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(activityEvent => activityEvent.MetadataJson)
                .HasMaxLength(4000);

            entity.HasIndex(activityEvent => activityEvent.CompoundId);
            entity.HasIndex(activityEvent => activityEvent.ResidentProfileId);
            entity.HasIndex(activityEvent => activityEvent.PropertyUnitId);
            entity.HasIndex(activityEvent => activityEvent.ActorUserId);
            entity.HasIndex(activityEvent => activityEvent.EventType);
            entity.HasIndex(activityEvent => new { activityEvent.EntityType, activityEvent.EntityId });
            entity.HasIndex(activityEvent => activityEvent.CreatedAtUtc);
            entity.HasIndex(activityEvent => new { activityEvent.CompoundId, activityEvent.CreatedAtUtc });
            entity.HasIndex(activityEvent => new { activityEvent.ResidentProfileId, activityEvent.CreatedAtUtc });
            entity.HasIndex(activityEvent => new { activityEvent.EntityType, activityEvent.EntityId, activityEvent.CreatedAtUtc });

            entity.HasOne(activityEvent => activityEvent.Compound)
                .WithMany()
                .HasForeignKey(activityEvent => activityEvent.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(activityEvent => activityEvent.ResidentProfile)
                .WithMany()
                .HasForeignKey(activityEvent => activityEvent.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(activityEvent => activityEvent.PropertyUnit)
                .WithMany()
                .HasForeignKey(activityEvent => activityEvent.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(activityEvent => activityEvent.ActorUser)
                .WithMany()
                .HasForeignKey(activityEvent => activityEvent.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureResidentProfile(ModelBuilder builder)
    {
        builder.Entity<ResidentProfile>(entity =>
        {
            entity.ToTable("ResidentProfiles");
            entity.HasKey(residentProfile => residentProfile.Id);

            entity.Property(residentProfile => residentProfile.FullName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(residentProfile => residentProfile.NationalId)
                .HasMaxLength(50);

            entity.Property(residentProfile => residentProfile.PhoneNumber)
                .HasMaxLength(30);

            entity.Property(residentProfile => residentProfile.AlternativePhoneNumber)
                .HasMaxLength(30);

            entity.Property(residentProfile => residentProfile.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(residentProfile => new { residentProfile.UserId, residentProfile.CompoundId })
                .IsUnique();

            entity.HasIndex(residentProfile => residentProfile.CompoundId);

            entity.HasOne(residentProfile => residentProfile.User)
                .WithMany()
                .HasForeignKey(residentProfile => residentProfile.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(residentProfile => residentProfile.Compound)
                .WithMany()
                .HasForeignKey(residentProfile => residentProfile.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOccupancyRecord(ModelBuilder builder)
    {
        builder.Entity<OccupancyRecord>(entity =>
        {
            entity.ToTable("OccupancyRecords");
            entity.HasKey(occupancyRecord => occupancyRecord.Id);

            entity.Property(occupancyRecord => occupancyRecord.ContractNumber)
                .HasMaxLength(100);

            entity.Property(occupancyRecord => occupancyRecord.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(occupancyRecord => occupancyRecord.ResidentProfileId);
            entity.HasIndex(occupancyRecord => occupancyRecord.PropertyUnitId);
            entity.HasIndex(occupancyRecord => occupancyRecord.CompoundId);

            entity.HasIndex(occupancyRecord => occupancyRecord.PropertyUnitId)
                .IsUnique()
                .HasFilter("[OccupancyStatus] = 0");

            entity.HasOne(occupancyRecord => occupancyRecord.ResidentProfile)
                .WithMany(residentProfile => residentProfile.OccupancyRecords)
                .HasForeignKey(occupancyRecord => occupancyRecord.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(occupancyRecord => occupancyRecord.Compound)
                .WithMany()
                .HasForeignKey(occupancyRecord => occupancyRecord.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(occupancyRecord => occupancyRecord.PropertyUnit)
                .WithMany()
                .HasForeignKey(occupancyRecord => occupancyRecord.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureFamilyMember(ModelBuilder builder)
    {
        builder.Entity<FamilyMember>(entity =>
        {
            entity.ToTable("FamilyMembers");
            entity.HasKey(familyMember => familyMember.Id);

            entity.Property(familyMember => familyMember.FullName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(familyMember => familyMember.Relationship)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(familyMember => familyMember.PhoneNumber)
                .HasMaxLength(30);

            entity.HasIndex(familyMember => familyMember.ResidentProfileId);

            entity.HasOne(familyMember => familyMember.ResidentProfile)
                .WithMany(residentProfile => residentProfile.FamilyMembers)
                .HasForeignKey(familyMember => familyMember.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureEmergencyContact(ModelBuilder builder)
    {
        builder.Entity<EmergencyContact>(entity =>
        {
            entity.ToTable("EmergencyContacts");
            entity.HasKey(emergencyContact => emergencyContact.Id);

            entity.Property(emergencyContact => emergencyContact.FullName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(emergencyContact => emergencyContact.Relationship)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(emergencyContact => emergencyContact.PhoneNumber)
                .IsRequired()
                .HasMaxLength(30);

            entity.HasIndex(emergencyContact => emergencyContact.ResidentProfileId);

            entity.HasOne(emergencyContact => emergencyContact.ResidentProfile)
                .WithMany(residentProfile => residentProfile.EmergencyContacts)
                .HasForeignKey(emergencyContact => emergencyContact.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCompoundService(ModelBuilder builder)
    {
        builder.Entity<CompoundService>(entity =>
        {
            entity.ToTable("CompoundServices");
            entity.HasKey(compoundService => compoundService.Id);

            entity.Property(compoundService => compoundService.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(compoundService => compoundService.Description)
                .HasMaxLength(1000);

            entity.Property(compoundService => compoundService.DefaultMonthlyFee)
                .HasPrecision(18, 2);

            entity.HasIndex(compoundService => new { compoundService.CompoundId, compoundService.Name })
                .IsUnique();

            entity.HasIndex(compoundService => new
            {
                compoundService.CompoundId,
                compoundService.ServiceType
            });

            entity.HasOne(compoundService => compoundService.Compound)
                .WithMany()
                .HasForeignKey(compoundService => compoundService.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureBillingCycle(ModelBuilder builder)
    {
        builder.Entity<BillingCycle>(entity =>
        {
            entity.ToTable("BillingCycles");
            entity.HasKey(billingCycle => billingCycle.Id);

            entity.HasIndex(billingCycle => new
            {
                billingCycle.CompoundId,
                billingCycle.Year,
                billingCycle.Month
            })
                .IsUnique();

            entity.HasOne(billingCycle => billingCycle.Compound)
                .WithMany()
                .HasForeignKey(billingCycle => billingCycle.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUtilityBill(ModelBuilder builder)
    {
        builder.Entity<UtilityBill>(entity =>
        {
            entity.ToTable("UtilityBills");
            entity.HasKey(utilityBill => utilityBill.Id);

            entity.Property(utilityBill => utilityBill.BillNumber)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(utilityBill => utilityBill.SubtotalAmount)
                .HasPrecision(18, 2);

            entity.Property(utilityBill => utilityBill.PreviousBalanceAmount)
                .HasPrecision(18, 2);

            entity.Property(utilityBill => utilityBill.LateFeeAmount)
                .HasPrecision(18, 2);

            entity.Property(utilityBill => utilityBill.DiscountAmount)
                .HasPrecision(18, 2);

            entity.Property(utilityBill => utilityBill.TotalAmount)
                .HasPrecision(18, 2);

            entity.Property(utilityBill => utilityBill.PaidAmount)
                .HasPrecision(18, 2);

            entity.Property(utilityBill => utilityBill.RowVersion)
                .IsRowVersion();

            entity.Property(utilityBill => utilityBill.Notes)
                .HasMaxLength(1000);

            entity.Property(utilityBill => utilityBill.CancellationReason)
                .HasMaxLength(500);

            entity.HasIndex(utilityBill => utilityBill.BillNumber)
                .IsUnique();

            entity.HasIndex(utilityBill => new { utilityBill.PropertyUnitId, utilityBill.BillingCycleId })
                .IsUnique();

            entity.HasIndex(utilityBill => utilityBill.CompoundId);
            entity.HasIndex(utilityBill => utilityBill.BillingCycleId);
            entity.HasIndex(utilityBill => utilityBill.PropertyUnitId);
            entity.HasIndex(utilityBill => utilityBill.ResidentProfileId);
            entity.HasIndex(utilityBill => utilityBill.BillStatus);

            entity.HasOne(utilityBill => utilityBill.Compound)
                .WithMany()
                .HasForeignKey(utilityBill => utilityBill.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(utilityBill => utilityBill.PropertyUnit)
                .WithMany()
                .HasForeignKey(utilityBill => utilityBill.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(utilityBill => utilityBill.ResidentProfile)
                .WithMany()
                .HasForeignKey(utilityBill => utilityBill.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(utilityBill => utilityBill.BillingCycle)
                .WithMany(billingCycle => billingCycle.UtilityBills)
                .HasForeignKey(utilityBill => utilityBill.BillingCycleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUtilityBillLine(ModelBuilder builder)
    {
        builder.Entity<UtilityBillLine>(entity =>
        {
            entity.ToTable("UtilityBillLines");
            entity.HasKey(utilityBillLine => utilityBillLine.Id);

            entity.Property(utilityBillLine => utilityBillLine.Description)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(utilityBillLine => utilityBillLine.Quantity)
                .HasPrecision(18, 4);

            entity.Property(utilityBillLine => utilityBillLine.UnitPrice)
                .HasPrecision(18, 2);

            entity.Property(utilityBillLine => utilityBillLine.LineTotal)
                .HasPrecision(18, 2);

            entity.HasIndex(utilityBillLine => utilityBillLine.UtilityBillId);
            entity.HasIndex(utilityBillLine => utilityBillLine.CompoundServiceId);

            entity.HasOne(utilityBillLine => utilityBillLine.UtilityBill)
                .WithMany(utilityBill => utilityBill.Lines)
                .HasForeignKey(utilityBillLine => utilityBillLine.UtilityBillId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(utilityBillLine => utilityBillLine.CompoundService)
                .WithMany(compoundService => compoundService.UtilityBillLines)
                .HasForeignKey(utilityBillLine => utilityBillLine.CompoundServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMeter(ModelBuilder builder)
    {
        builder.Entity<Meter>(entity =>
        {
            entity.ToTable("Meters");
            entity.HasKey(meter => meter.Id);

            entity.Property(meter => meter.MeterNumber)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(meter => meter.RatePerUnit)
                .HasPrecision(18, 2);

            entity.HasIndex(meter => new { meter.CompoundId, meter.MeterNumber })
                .IsUnique();

            entity.HasIndex(meter => new { meter.PropertyUnitId, meter.MeterType })
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            entity.HasIndex(meter => meter.CompoundId);
            entity.HasIndex(meter => meter.PropertyUnitId);
            entity.HasIndex(meter => meter.MeterType);

            entity.HasOne(meter => meter.Compound)
                .WithMany()
                .HasForeignKey(meter => meter.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(meter => meter.PropertyUnit)
                .WithMany()
                .HasForeignKey(meter => meter.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMeterReading(ModelBuilder builder)
    {
        builder.Entity<MeterReading>(entity =>
        {
            entity.ToTable("MeterReadings");
            entity.HasKey(reading => reading.Id);

            entity.Property(reading => reading.PreviousReading)
                .HasPrecision(18, 4);

            entity.Property(reading => reading.CurrentReading)
                .HasPrecision(18, 4);

            entity.Property(reading => reading.Consumption)
                .HasPrecision(18, 4);

            entity.Property(reading => reading.RatePerUnit)
                .HasPrecision(18, 2);

            entity.Property(reading => reading.Amount)
                .HasPrecision(18, 2);

            entity.Property(reading => reading.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(reading => new
                {
                    reading.MeterId,
                    reading.Year,
                    reading.Month
                })
                .IsUnique();

            entity.HasIndex(reading => reading.CompoundId);
            entity.HasIndex(reading => reading.PropertyUnitId);
            entity.HasIndex(reading => reading.IsBilled);
            entity.HasIndex(reading => reading.UtilityBillId);
            entity.HasIndex(reading => reading.UtilityBillLineId);

            entity.HasOne(reading => reading.Compound)
                .WithMany()
                .HasForeignKey(reading => reading.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(reading => reading.Meter)
                .WithMany(meter => meter.Readings)
                .HasForeignKey(reading => reading.MeterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(reading => reading.PropertyUnit)
                .WithMany()
                .HasForeignKey(reading => reading.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(reading => reading.UtilityBill)
                .WithMany()
                .HasForeignKey(reading => reading.UtilityBillId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(reading => reading.UtilityBillLine)
                .WithMany()
                .HasForeignKey(reading => reading.UtilityBillLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }



    private static void ConfigureSmartMeterDevice(ModelBuilder builder)
    {
        builder.Entity<SmartMeterDevice>(entity =>
        {
            entity.ToTable("SmartMeterDevices");
            entity.HasKey(device => device.Id);

            entity.Property(device => device.DeviceIdentifier)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(device => device.ProviderName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(device => device.FirmwareVersion)
                .HasMaxLength(100);

            entity.Property(device => device.SuspiciousConsumptionThreshold)
                .HasPrecision(18, 2);

            entity.Property(device => device.LastReadingValue)
                .HasPrecision(18, 2);

            entity.HasIndex(device => new { device.CompoundId, device.DeviceIdentifier })
                .IsUnique();

            entity.HasIndex(device => device.MeterId);
            entity.HasIndex(device => device.Status);
            entity.HasIndex(device => device.HealthStatus);
            entity.HasIndex(device => device.LastSeenAtUtc);

            entity.HasOne(device => device.Compound)
                .WithMany()
                .HasForeignKey(device => device.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(device => device.Meter)
                .WithMany()
                .HasForeignKey(device => device.MeterId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSmartMeterReadingIngestion(ModelBuilder builder)
    {
        builder.Entity<SmartMeterReadingIngestion>(entity =>
        {
            entity.ToTable("SmartMeterReadingIngestions");
            entity.HasKey(ingestion => ingestion.Id);

            entity.Property(ingestion => ingestion.PreviousReading)
                .HasPrecision(18, 2);

            entity.Property(ingestion => ingestion.CurrentReading)
                .HasPrecision(18, 2);

            entity.Property(ingestion => ingestion.Consumption)
                .HasPrecision(18, 2);

            entity.Property(ingestion => ingestion.ProviderReference)
                .HasMaxLength(128);

            entity.Property(ingestion => ingestion.Message)
                .HasMaxLength(1000);

            entity.Property(ingestion => ingestion.RawPayload)
                .HasMaxLength(4000);

            entity.HasIndex(ingestion => ingestion.CompoundId);
            entity.HasIndex(ingestion => ingestion.SmartMeterDeviceId);
            entity.HasIndex(ingestion => ingestion.MeterId);
            entity.HasIndex(ingestion => ingestion.Status);
            entity.HasIndex(ingestion => ingestion.AnomalyType);
            entity.HasIndex(ingestion => ingestion.BillingHoldRecommended);
            entity.HasIndex(ingestion => new { ingestion.MeterId, ingestion.Year, ingestion.Month });

            entity.HasOne(ingestion => ingestion.Compound)
                .WithMany()
                .HasForeignKey(ingestion => ingestion.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ingestion => ingestion.SmartMeterDevice)
                .WithMany(device => device.Ingestions)
                .HasForeignKey(ingestion => ingestion.SmartMeterDeviceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ingestion => ingestion.Meter)
                .WithMany()
                .HasForeignKey(ingestion => ingestion.MeterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ingestion => ingestion.PropertyUnit)
                .WithMany()
                .HasForeignKey(ingestion => ingestion.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ingestion => ingestion.MeterReading)
                .WithMany()
                .HasForeignKey(ingestion => ingestion.MeterReadingId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePayment(ModelBuilder builder)
    {
        builder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(payment => payment.Id);

            entity.Property(payment => payment.Amount)
                .HasPrecision(18, 2);

            entity.Property(payment => payment.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(payment => payment.IdempotencyKey)
                .HasMaxLength(120);

            entity.Property(payment => payment.PaymentReference)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(payment => payment.FailureReason)
                .HasMaxLength(500);

            entity.Property(payment => payment.RowVersion)
                .IsRowVersion();

            entity.HasIndex(payment => payment.PaymentReference)
                .IsUnique();

            entity.HasIndex(payment => payment.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");

            entity.HasIndex(payment => payment.CompoundId);
            entity.HasIndex(payment => payment.ResidentProfileId);
            entity.HasIndex(payment => payment.TargetId);
            entity.HasIndex(payment => new { payment.TargetType, payment.TargetId });
            entity.HasIndex(payment => payment.PaymentStatus);
            entity.HasIndex(payment => new { payment.CompoundId, payment.PaymentStatus, payment.CreatedAt });
            entity.HasIndex(payment => new { payment.ResidentProfileId, payment.PaymentStatus, payment.CreatedAt });
            entity.HasIndex(payment => new { payment.TargetType, payment.TargetId, payment.PaymentStatus });

            entity.HasOne(payment => payment.Compound)
                .WithMany()
                .HasForeignKey(payment => payment.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(payment => payment.ResidentProfile)
                .WithMany()
                .HasForeignKey(payment => payment.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePaymentAttempt(ModelBuilder builder)
    {
        builder.Entity<PaymentAttempt>(entity =>
        {
            entity.ToTable("PaymentAttempts");
            entity.HasKey(paymentAttempt => paymentAttempt.Id);

            entity.Property(paymentAttempt => paymentAttempt.Provider)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(paymentAttempt => paymentAttempt.ProviderTransactionId)
                .HasMaxLength(120);

            entity.Property(paymentAttempt => paymentAttempt.Message)
                .HasMaxLength(500);

            entity.HasIndex(paymentAttempt => paymentAttempt.PaymentId);
            entity.HasIndex(paymentAttempt => new { paymentAttempt.Provider, paymentAttempt.ProviderTransactionId })
                .IsUnique()
                .HasFilter("[ProviderTransactionId] IS NOT NULL");

            entity.HasOne(paymentAttempt => paymentAttempt.Payment)
                .WithMany(payment => payment.Attempts)
                .HasForeignKey(paymentAttempt => paymentAttempt.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePaymentReconciliationBatch(ModelBuilder builder)
    {
        builder.Entity<PaymentReconciliationBatch>(entity =>
        {
            entity.ToTable("PaymentReconciliationBatches");
            entity.HasKey(batch => batch.Id);

            entity.Property(batch => batch.Provider)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(batch => batch.StatementReference)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(batch => batch.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(batch => batch.CompoundId);
            entity.HasIndex(batch => batch.Provider);
            entity.HasIndex(batch => batch.Status);
            entity.HasIndex(batch => batch.StatementDate);
            entity.HasIndex(batch => new
                {
                    batch.CompoundId,
                    batch.Provider,
                    batch.StatementReference
                })
                .IsUnique();

            entity.HasOne(batch => batch.Compound)
                .WithMany()
                .HasForeignKey(batch => batch.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(batch => batch.CreatedByUser)
                .WithMany()
                .HasForeignKey(batch => batch.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(batch => batch.ClosedByUser)
                .WithMany()
                .HasForeignKey(batch => batch.ClosedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePaymentReconciliationItem(ModelBuilder builder)
    {
        builder.Entity<PaymentReconciliationItem>(entity =>
        {
            entity.ToTable("PaymentReconciliationItems");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.ProviderTransactionId)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(item => item.ProviderAmount)
                .HasPrecision(18, 2);

            entity.Property(item => item.DifferenceAmount)
                .HasPrecision(18, 2);

            entity.Property(item => item.IssueReason)
                .HasMaxLength(500);

            entity.Property(item => item.ReviewNotes)
                .HasMaxLength(1000);

            entity.HasIndex(item => item.PaymentReconciliationBatchId);
            entity.HasIndex(item => item.MatchStatus);
            entity.HasIndex(item => item.ReviewDecision);
            entity.HasIndex(item => item.ReviewedAtUtc);
            entity.HasIndex(item => item.MatchedPaymentId);
            entity.HasIndex(item => item.MatchedPaymentAttemptId);
            entity.HasIndex(item => new
                {
                    item.PaymentReconciliationBatchId,
                    item.ProviderTransactionId
                })
                .IsUnique();

            entity.HasOne(item => item.Batch)
                .WithMany(batch => batch.Items)
                .HasForeignKey(item => item.PaymentReconciliationBatchId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.MatchedPayment)
                .WithMany()
                .HasForeignKey(item => item.MatchedPaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.MatchedPaymentAttempt)
                .WithMany()
                .HasForeignKey(item => item.MatchedPaymentAttemptId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ReviewedByUser)
                .WithMany()
                .HasForeignKey(item => item.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureReceipt(ModelBuilder builder)
    {
        builder.Entity<Receipt>(entity =>
        {
            entity.ToTable("Receipts");
            entity.HasKey(receipt => receipt.Id);

            entity.Property(receipt => receipt.ReceiptNumber)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(receipt => receipt.Amount)
                .HasPrecision(18, 2);

            entity.HasIndex(receipt => receipt.PaymentId)
                .IsUnique();

            entity.HasIndex(receipt => receipt.ReceiptNumber)
                .IsUnique();

            entity.HasOne(receipt => receipt.Payment)
                .WithOne(payment => payment.Receipt)
                .HasForeignKey<Receipt>(receipt => receipt.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePropertySaleContract(ModelBuilder builder)
    {
        builder.Entity<PropertySaleContract>(entity =>
        {
            entity.ToTable("PropertySaleContracts");
            entity.HasKey(contract => contract.Id);

            entity.Property(contract => contract.ContractNumber)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(contract => contract.PropertyPrice)
                .HasPrecision(18, 2);

            entity.Property(contract => contract.DownPaymentAmount)
                .HasPrecision(18, 2);

            entity.Property(contract => contract.Notes)
                .HasMaxLength(1000);

            entity.Property(contract => contract.CancellationReason)
                .HasMaxLength(500);

            entity.HasIndex(contract => contract.ContractNumber)
                .IsUnique();

            entity.HasIndex(contract => contract.CompoundId);
            entity.HasIndex(contract => contract.PropertyUnitId);
            entity.HasIndex(contract => contract.ResidentProfileId);
            entity.HasIndex(contract => contract.ContractStatus);

            entity.HasIndex(contract => contract.PropertyUnitId)
                .IsUnique()
                .HasFilter("[ContractStatus] = 0");

            entity.HasOne(contract => contract.Compound)
                .WithMany()
                .HasForeignKey(contract => contract.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(contract => contract.PropertyUnit)
                .WithMany()
                .HasForeignKey(contract => contract.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(contract => contract.ResidentProfile)
                .WithMany()
                .HasForeignKey(contract => contract.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInstallmentScheduleItem(ModelBuilder builder)
    {
        builder.Entity<InstallmentScheduleItem>(entity =>
        {
            entity.ToTable("InstallmentScheduleItems");
            entity.HasKey(installment => installment.Id);

            entity.Property(installment => installment.Amount)
                .HasPrecision(18, 2);

            entity.Property(installment => installment.PaidAmount)
                .HasPrecision(18, 2);

            entity.Property(installment => installment.RowVersion)
                .IsRowVersion();

            entity.Property(installment => installment.CancellationReason)
                .HasMaxLength(500);

            entity.HasIndex(installment => installment.PropertySaleContractId);
            entity.HasIndex(installment => installment.CompoundId);
            entity.HasIndex(installment => installment.PropertyUnitId);
            entity.HasIndex(installment => installment.ResidentProfileId);
            entity.HasIndex(installment => installment.InstallmentStatus);

            entity.HasIndex(installment => new
                {
                    installment.PropertySaleContractId,
                    installment.InstallmentNumber
                })
                .IsUnique();

            entity.HasOne(installment => installment.PropertySaleContract)
                .WithMany(contract => contract.Installments)
                .HasForeignKey(installment => installment.PropertySaleContractId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(installment => installment.Compound)
                .WithMany()
                .HasForeignKey(installment => installment.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(installment => installment.PropertyUnit)
                .WithMany()
                .HasForeignKey(installment => installment.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(installment => installment.ResidentProfile)
                .WithMany()
                .HasForeignKey(installment => installment.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRentContract(ModelBuilder builder)
    {
        builder.Entity<RentContract>(entity =>
        {
            entity.ToTable("RentContracts");
            entity.HasKey(contract => contract.Id);

            entity.Property(contract => contract.ContractNumber)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(contract => contract.MonthlyRentAmount)
                .HasPrecision(18, 2);

            entity.Property(contract => contract.DepositAmount)
                .HasPrecision(18, 2);

            entity.Property(contract => contract.Notes)
                .HasMaxLength(1000);

            entity.Property(contract => contract.TerminationReason)
                .HasMaxLength(500);

            entity.Property(contract => contract.CancellationReason)
                .HasMaxLength(500);

            entity.HasIndex(contract => contract.ContractNumber)
                .IsUnique();

            entity.HasIndex(contract => contract.CompoundId);
            entity.HasIndex(contract => contract.PropertyUnitId);
            entity.HasIndex(contract => contract.ResidentProfileId);
            entity.HasIndex(contract => contract.ContractStatus);

            entity.HasIndex(contract => contract.PropertyUnitId)
                .IsUnique()
                .HasFilter("[ContractStatus] = 0");

            entity.HasOne(contract => contract.Compound)
                .WithMany()
                .HasForeignKey(contract => contract.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(contract => contract.PropertyUnit)
                .WithMany()
                .HasForeignKey(contract => contract.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(contract => contract.ResidentProfile)
                .WithMany()
                .HasForeignKey(contract => contract.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRentInvoice(ModelBuilder builder)
    {
        builder.Entity<RentInvoice>(entity =>
        {
            entity.ToTable("RentInvoices");
            entity.HasKey(invoice => invoice.Id);

            entity.Property(invoice => invoice.InvoiceNumber)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(invoice => invoice.RentAmount)
                .HasPrecision(18, 2);

            entity.Property(invoice => invoice.PreviousBalanceAmount)
                .HasPrecision(18, 2);

            entity.Property(invoice => invoice.LateFeeAmount)
                .HasPrecision(18, 2);

            entity.Property(invoice => invoice.DiscountAmount)
                .HasPrecision(18, 2);

            entity.Property(invoice => invoice.TotalAmount)
                .HasPrecision(18, 2);

            entity.Property(invoice => invoice.PaidAmount)
                .HasPrecision(18, 2);

            entity.Property(invoice => invoice.RowVersion)
                .IsRowVersion();

            entity.Property(invoice => invoice.Notes)
                .HasMaxLength(1000);

            entity.Property(invoice => invoice.CancellationReason)
                .HasMaxLength(500);

            entity.HasIndex(invoice => invoice.InvoiceNumber)
                .IsUnique();

            entity.HasIndex(invoice => new
                {
                    invoice.RentContractId,
                    invoice.Year,
                    invoice.Month
                })
                .IsUnique();

            entity.HasIndex(invoice => invoice.CompoundId);
            entity.HasIndex(invoice => invoice.PropertyUnitId);
            entity.HasIndex(invoice => invoice.ResidentProfileId);
            entity.HasIndex(invoice => invoice.RentInvoiceStatus);

            entity.HasOne(invoice => invoice.RentContract)
                .WithMany(contract => contract.RentInvoices)
                .HasForeignKey(invoice => invoice.RentContractId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(invoice => invoice.Compound)
                .WithMany()
                .HasForeignKey(invoice => invoice.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(invoice => invoice.PropertyUnit)
                .WithMany()
                .HasForeignKey(invoice => invoice.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(invoice => invoice.ResidentProfile)
                .WithMany()
                .HasForeignKey(invoice => invoice.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureVisitorPass(ModelBuilder builder)
    {
        builder.Entity<VisitorPass>(entity =>
        {
            entity.ToTable("VisitorPasses");
            entity.HasKey(visitorPass => visitorPass.Id);

            entity.Property(visitorPass => visitorPass.VisitorName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(visitorPass => visitorPass.VisitorPhoneNumber)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(visitorPass => visitorPass.VisitReason)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(visitorPass => visitorPass.AccessCode)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(visitorPass => visitorPass.DenialReason)
                .HasMaxLength(500);
            entity.Property(visitorPass => visitorPass.RowVersion)
                .IsRowVersion();

            entity.HasIndex(visitorPass => visitorPass.AccessCode)
                .IsUnique();

            entity.HasIndex(visitorPass => visitorPass.ResidentProfileId);
            entity.HasIndex(visitorPass => visitorPass.CompoundId);
            entity.HasIndex(visitorPass => visitorPass.PropertyUnitId);
            entity.HasIndex(visitorPass => visitorPass.Status);
            entity.HasIndex(visitorPass => new { visitorPass.ValidFrom, visitorPass.ValidUntil });

            entity.HasOne(visitorPass => visitorPass.ResidentProfile)
                .WithMany()
                .HasForeignKey(visitorPass => visitorPass.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(visitorPass => visitorPass.Compound)
                .WithMany()
                .HasForeignKey(visitorPass => visitorPass.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(visitorPass => visitorPass.PropertyUnit)
                .WithMany()
                .HasForeignKey(visitorPass => visitorPass.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureVisitorAccessLog(ModelBuilder builder)
    {
        builder.Entity<VisitorAccessLog>(entity =>
        {
            entity.ToTable("VisitorAccessLogs");
            entity.HasKey(accessLog => accessLog.Id);

            entity.Property(accessLog => accessLog.Notes)
                .HasMaxLength(500);

            entity.HasIndex(accessLog => accessLog.VisitorPassId);
            entity.HasIndex(accessLog => accessLog.GuardUserId);
            entity.HasIndex(accessLog => accessLog.Action);
            entity.HasIndex(accessLog => accessLog.CreatedAt);

            entity.HasOne(accessLog => accessLog.VisitorPass)
                .WithMany(visitorPass => visitorPass.AccessLogs)
                .HasForeignKey(accessLog => accessLog.VisitorPassId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(accessLog => accessLog.GuardUser)
                .WithMany()
                .HasForeignKey(accessLog => accessLog.GuardUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


    private static void ConfigureContractorWorkPermit(ModelBuilder builder)
    {
        builder.Entity<ContractorWorkPermit>(entity =>
        {
            entity.ToTable("ContractorWorkPermits");
            entity.HasKey(permit => permit.Id);

            entity.Property(permit => permit.Purpose)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(permit => permit.WorkArea)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(permit => permit.EquipmentList)
                .HasMaxLength(1000);

            entity.Property(permit => permit.DenialReason)
                .HasMaxLength(500);

            entity.Property(permit => permit.GuardNotes)
                .HasMaxLength(1000);

            entity.Property(permit => permit.RowVersion)
                .IsRowVersion();

            entity.HasIndex(permit => permit.CompoundId);
            entity.HasIndex(permit => permit.VendorId);
            entity.HasIndex(permit => permit.RelatedWorkOrderId);
            entity.HasIndex(permit => permit.Status);
            entity.HasIndex(permit => permit.RiskLevel);
            entity.HasIndex(permit => new { permit.AllowedFromUtc, permit.AllowedUntilUtc });

            entity.HasOne(permit => permit.Compound)
                .WithMany()
                .HasForeignKey(permit => permit.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(permit => permit.Vendor)
                .WithMany()
                .HasForeignKey(permit => permit.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(permit => permit.RelatedWorkOrder)
                .WithMany()
                .HasForeignKey(permit => permit.RelatedWorkOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureContractorAccessLog(ModelBuilder builder)
    {
        builder.Entity<ContractorAccessLog>(entity =>
        {
            entity.ToTable("ContractorAccessLogs");
            entity.HasKey(log => log.Id);

            entity.Property(log => log.Notes)
                .HasMaxLength(500);

            entity.HasIndex(log => log.ContractorWorkPermitId);
            entity.HasIndex(log => log.GuardUserId);
            entity.HasIndex(log => log.Action);
            entity.HasIndex(log => log.CreatedAtUtc);

            entity.HasOne(log => log.ContractorWorkPermit)
                .WithMany(permit => permit.AccessLogs)
                .HasForeignKey(log => log.ContractorWorkPermitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(log => log.GuardUser)
                .WithMany()
                .HasForeignKey(log => log.GuardUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAccessCredential(ModelBuilder builder)
    {
        builder.Entity<AccessCredential>(entity =>
        {
            entity.ToTable("AccessCredentials");
            entity.HasKey(credential => credential.Id);

            entity.Property(credential => credential.OwnerDisplayName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(credential => credential.CredentialCode)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(credential => credential.RevocationReason)
                .HasMaxLength(500);

            entity.Property(credential => credential.Notes)
                .HasMaxLength(1000);

            entity.Property(credential => credential.RowVersion)
                .IsRowVersion();

            entity.HasIndex(credential => credential.CompoundId);
            entity.HasIndex(credential => credential.CredentialType);
            entity.HasIndex(credential => credential.Status);
            entity.HasIndex(credential => credential.OwnerType);
            entity.HasIndex(credential => credential.OwnerEntityId);
            entity.HasIndex(credential => credential.SourceVisitorPassId);
            entity.HasIndex(credential => credential.SourceContractorWorkPermitId);
            entity.HasIndex(credential => credential.CredentialCode).IsUnique();

            entity.HasOne(credential => credential.Compound)
                .WithMany()
                .HasForeignKey(credential => credential.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(credential => credential.SourceVisitorPass)
                .WithMany()
                .HasForeignKey(credential => credential.SourceVisitorPassId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(credential => credential.SourceContractorWorkPermit)
                .WithMany()
                .HasForeignKey(credential => credential.SourceContractorWorkPermitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMaintenanceRequest(ModelBuilder builder)
    {
        builder.Entity<MaintenanceRequest>(entity =>
        {
            entity.ToTable("MaintenanceRequests");
            entity.HasKey(request => request.Id);

            entity.Property(request => request.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(request => request.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(request => request.CostEstimate)
                .HasPrecision(18, 2);

            entity.Property(request => request.ActualCost)
                .HasPrecision(18, 2);

            entity.Property(request => request.ResolutionNotes)
                .HasMaxLength(2000);

            entity.HasIndex(request => request.ResidentProfileId);
            entity.HasIndex(request => request.CompoundId);
            entity.HasIndex(request => request.PropertyUnitId);
            entity.HasIndex(request => request.AssignedToUserId);
            entity.HasIndex(request => request.Status);
            entity.HasIndex(request => request.Priority);

            entity.HasOne(request => request.ResidentProfile)
                .WithMany()
                .HasForeignKey(request => request.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(request => request.Compound)
                .WithMany()
                .HasForeignKey(request => request.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(request => request.PropertyUnit)
                .WithMany()
                .HasForeignKey(request => request.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(request => request.AssignedToUser)
                .WithMany()
                .HasForeignKey(request => request.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMaintenanceStatusHistory(ModelBuilder builder)
    {
        builder.Entity<MaintenanceStatusHistory>(entity =>
        {
            entity.ToTable("MaintenanceStatusHistories");
            entity.HasKey(history => history.Id);

            entity.Property(history => history.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(history => history.MaintenanceRequestId);
            entity.HasIndex(history => history.ChangedByUserId);
            entity.HasIndex(history => history.NewStatus);
            entity.HasIndex(history => history.CreatedAt);

            entity.HasOne(history => history.MaintenanceRequest)
                .WithMany(request => request.StatusHistory)
                .HasForeignKey(history => history.MaintenanceRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(history => history.ChangedByUser)
                .WithMany()
                .HasForeignKey(history => history.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureComplaint(ModelBuilder builder)
    {
        builder.Entity<Complaint>(entity =>
        {
            entity.ToTable("Complaints");
            entity.HasKey(complaint => complaint.Id);

            entity.Property(complaint => complaint.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(complaint => complaint.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(complaint => complaint.AdminResponse)
                .HasMaxLength(2000);

            entity.HasIndex(complaint => complaint.ResidentProfileId);
            entity.HasIndex(complaint => complaint.CompoundId);
            entity.HasIndex(complaint => complaint.PropertyUnitId);
            entity.HasIndex(complaint => complaint.Status);

            entity.HasOne(complaint => complaint.ResidentProfile)
                .WithMany()
                .HasForeignKey(complaint => complaint.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(complaint => complaint.Compound)
                .WithMany()
                .HasForeignKey(complaint => complaint.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(complaint => complaint.PropertyUnit)
                .WithMany()
                .HasForeignKey(complaint => complaint.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureViolation(ModelBuilder builder)
    {
        builder.Entity<Violation>(entity =>
        {
            entity.ToTable("Violations");
            entity.HasKey(violation => violation.Id);

            entity.Property(violation => violation.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(violation => violation.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.HasIndex(violation => violation.CompoundId);
            entity.HasIndex(violation => violation.ResidentProfileId);
            entity.HasIndex(violation => violation.PropertyUnitId);
            entity.HasIndex(violation => violation.ComplaintId);
            entity.HasIndex(violation => violation.ViolationType);
            entity.HasIndex(violation => violation.CreatedByUserId);

            entity.HasOne(violation => violation.Compound)
                .WithMany()
                .HasForeignKey(violation => violation.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(violation => violation.ResidentProfile)
                .WithMany()
                .HasForeignKey(violation => violation.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(violation => violation.PropertyUnit)
                .WithMany()
                .HasForeignKey(violation => violation.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(violation => violation.Complaint)
                .WithMany(complaint => complaint.Violations)
                .HasForeignKey(violation => violation.ComplaintId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(violation => violation.CreatedByUser)
                .WithMany()
                .HasForeignKey(violation => violation.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureViolationFine(ModelBuilder builder)
    {
        builder.Entity<ViolationFine>(entity =>
        {
            entity.ToTable("ViolationFines");
            entity.HasKey(fine => fine.Id);

            entity.Property(fine => fine.Amount)
                .HasPrecision(18, 2);

            entity.Property(fine => fine.PaidAmount)
                .HasPrecision(18, 2);

            entity.Property(fine => fine.RowVersion)
                .IsRowVersion();

            entity.Property(fine => fine.Reason)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(fine => fine.CancellationReason)
                .HasMaxLength(500);

            entity.HasIndex(fine => fine.ViolationId);
            entity.HasIndex(fine => fine.CompoundId);
            entity.HasIndex(fine => fine.ResidentProfileId);
            entity.HasIndex(fine => fine.Status);
            entity.HasIndex(fine => new { fine.CompoundId, fine.ResidentProfileId, fine.Status, fine.DueDate });

            entity.HasIndex(fine => fine.ViolationId)
                .IsUnique()
                .HasFilter("[Status] <> 3");

            entity.HasOne(fine => fine.Violation)
                .WithMany(violation => violation.Fines)
                .HasForeignKey(fine => fine.ViolationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(fine => fine.Compound)
                .WithMany()
                .HasForeignKey(fine => fine.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(fine => fine.ResidentProfile)
                .WithMany()
                .HasForeignKey(fine => fine.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


    private static void ConfigureViolationAppeal(ModelBuilder builder)
    {
        builder.Entity<ViolationAppeal>(entity =>
        {
            entity.ToTable("ViolationAppeals");
            entity.HasKey(appeal => appeal.Id);

            entity.Property(appeal => appeal.Reason)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(appeal => appeal.ResidentMessage)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(appeal => appeal.AdminDecisionNotes)
                .HasMaxLength(2000);

            entity.Property(appeal => appeal.ReducedFineAmount)
                .HasPrecision(18, 2);

            entity.Property(appeal => appeal.RowVersion)
                .IsRowVersion();

            entity.HasIndex(appeal => appeal.CompoundId);
            entity.HasIndex(appeal => appeal.ResidentProfileId);
            entity.HasIndex(appeal => appeal.Status);
            entity.HasIndex(appeal => appeal.FinancialAdjustmentId)
                .HasFilter("[FinancialAdjustmentId] IS NOT NULL");
            entity.HasIndex(appeal => appeal.ViolationId);
            entity.HasIndex(appeal => appeal.ViolationFineId);
            entity.HasIndex(appeal => new { appeal.CompoundId, appeal.Status, appeal.CreatedAtUtc });
            entity.HasIndex(appeal => new { appeal.CompoundId, appeal.ResidentProfileId, appeal.ViolationId, appeal.ViolationFineId, appeal.Status });

            entity.HasOne(appeal => appeal.Compound)
                .WithMany()
                .HasForeignKey(appeal => appeal.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(appeal => appeal.ResidentProfile)
                .WithMany()
                .HasForeignKey(appeal => appeal.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(appeal => appeal.Violation)
                .WithMany()
                .HasForeignKey(appeal => appeal.ViolationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(appeal => appeal.ViolationFine)
                .WithMany()
                .HasForeignKey(appeal => appeal.ViolationFineId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(appeal => appeal.FinancialAdjustment)
                .WithMany()
                .HasForeignKey(appeal => appeal.FinancialAdjustmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(appeal => appeal.CreatedByUser)
                .WithMany()
                .HasForeignKey(appeal => appeal.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(appeal => appeal.ReviewedByUser)
                .WithMany()
                .HasForeignKey(appeal => appeal.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDocumentFile(ModelBuilder builder)
    {
        builder.Entity<DocumentFile>(entity =>
        {
            entity.ToTable("DocumentFiles");
            entity.HasKey(document => document.Id);

            entity.Property(document => document.OriginalFileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(document => document.StoredFileName)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(document => document.ContentType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(document => document.Extension)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(document => document.StoragePath)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(document => document.RelatedEntityType)
                .HasMaxLength(100);

            entity.Property(document => document.Description)
                .HasMaxLength(1000);

            entity.Property(document => document.ReviewReason)
                .HasMaxLength(1000);

            entity.HasIndex(document => document.Category);
            entity.HasIndex(document => document.Visibility);
            entity.HasIndex(document => document.OwnerUserId);
            entity.HasIndex(document => document.UploadedByUserId);
            entity.HasIndex(document => document.CompoundId);
            entity.HasIndex(document => new { document.RelatedEntityType, document.RelatedEntityId });
            entity.HasIndex(document => document.PropertyUnitId);
            entity.HasIndex(document => document.IsDeleted);
            entity.HasIndex(document => document.ApprovalStatus);
            entity.HasIndex(document => document.ExpiresAtUtc);
            entity.HasIndex(document => document.RootDocumentFileId);
            entity.HasIndex(document => document.PreviousVersionDocumentFileId);
            entity.HasIndex(document => document.CreatedAtUtc);

            entity.HasOne(document => document.UploadedByUser)
                .WithMany()
                .HasForeignKey(document => document.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(document => document.OwnerUser)
                .WithMany()
                .HasForeignKey(document => document.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(document => document.Compound)
                .WithMany()
                .HasForeignKey(document => document.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(document => document.PropertyUnit)
                .WithMany()
                .HasForeignKey(document => document.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(document => document.ReviewedByUser)
                .WithMany()
                .HasForeignKey(document => document.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(document => document.RootDocumentFile)
                .WithMany(document => document.Versions)
                .HasForeignKey(document => document.RootDocumentFileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(document => document.PreviousVersionDocumentFile)
                .WithMany()
                .HasForeignKey(document => document.PreviousVersionDocumentFileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDocumentRequirement(ModelBuilder builder)
    {
        builder.Entity<DocumentRequirement>(entity =>
        {
            entity.ToTable("DocumentRequirements");
            entity.HasKey(requirement => requirement.Id);

            entity.Property(requirement => requirement.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(requirement => requirement.Description)
                .HasMaxLength(1000);

            entity.HasIndex(requirement => requirement.CompoundId);
            entity.HasIndex(requirement => requirement.Category);
            entity.HasIndex(requirement => requirement.AppliesTo);
            entity.HasIndex(requirement => requirement.IsMandatory);
            entity.HasIndex(requirement => requirement.IsActive);
            entity.HasIndex(requirement => new { requirement.CompoundId, requirement.Category, requirement.AppliesTo, requirement.IsActive });

            entity.HasOne(requirement => requirement.Compound)
                .WithMany()
                .HasForeignKey(requirement => requirement.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(requirement => requirement.CreatedByUser)
                .WithMany()
                .HasForeignKey(requirement => requirement.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDocumentAccessLog(ModelBuilder builder)
    {
        builder.Entity<DocumentAccessLog>(entity =>
        {
            entity.ToTable("DocumentAccessLogs");
            entity.HasKey(accessLog => accessLog.Id);

            entity.Property(accessLog => accessLog.IpAddress)
                .HasMaxLength(128);

            entity.Property(accessLog => accessLog.UserAgent)
                .HasMaxLength(500);

            entity.HasIndex(accessLog => accessLog.DocumentFileId);
            entity.HasIndex(accessLog => accessLog.UserId);
            entity.HasIndex(accessLog => accessLog.Action);
            entity.HasIndex(accessLog => accessLog.CreatedAtUtc);

            entity.HasOne(accessLog => accessLog.DocumentFile)
                .WithMany(document => document.AccessLogs)
                .HasForeignKey(accessLog => accessLog.DocumentFileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(accessLog => accessLog.User)
                .WithMany()
                .HasForeignKey(accessLog => accessLog.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureStaffMember(ModelBuilder builder)
    {
        builder.Entity<StaffMember>(entity =>
        {
            entity.ToTable("StaffMembers");
            entity.HasKey(staffMember => staffMember.Id);

            entity.Property(staffMember => staffMember.FullName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(staffMember => staffMember.PhoneNumber)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(staffMember => staffMember.Email)
                .HasMaxLength(256);

            entity.Property(staffMember => staffMember.Specialization)
                .HasMaxLength(150);

            entity.Property(staffMember => staffMember.NationalId)
                .HasMaxLength(50);

            entity.Property(staffMember => staffMember.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(staffMember => staffMember.FullName);
            entity.HasIndex(staffMember => staffMember.CompoundId);
            entity.HasIndex(staffMember => staffMember.PhoneNumber);
            entity.HasIndex(staffMember => staffMember.StaffType);
            entity.HasIndex(staffMember => staffMember.Status);
            entity.HasIndex(staffMember => staffMember.UserId);

            entity.HasOne(staffMember => staffMember.Compound)
                .WithMany()
                .HasForeignKey(staffMember => staffMember.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(staffMember => staffMember.User)
                .WithMany()
                .HasForeignKey(staffMember => staffMember.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureServiceVendor(ModelBuilder builder)
    {
        builder.Entity<ServiceVendor>(entity =>
        {
            entity.ToTable("ServiceVendors");
            entity.HasKey(vendor => vendor.Id);

            entity.Property(vendor => vendor.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(vendor => vendor.ContactPersonName)
                .HasMaxLength(150);

            entity.Property(vendor => vendor.PhoneNumber)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(vendor => vendor.Email)
                .HasMaxLength(256);

            entity.Property(vendor => vendor.Address)
                .HasMaxLength(300);

            entity.Property(vendor => vendor.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(vendor => vendor.Name);
            entity.HasIndex(vendor => vendor.CompoundId);
            entity.HasIndex(vendor => vendor.ServiceType);
            entity.HasIndex(vendor => vendor.Status);

            entity.HasOne(vendor => vendor.Compound)
                .WithMany()
                .HasForeignKey(vendor => vendor.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureStockItem(ModelBuilder builder)
    {
        builder.Entity<StockItem>(entity =>
        {
            entity.ToTable("StockItems");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(item => item.Sku)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(item => item.Category)
                .HasMaxLength(100);

            entity.Property(item => item.UnitOfMeasure)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(item => item.CurrentQuantity)
                .HasPrecision(18, 4);

            entity.Property(item => item.MinimumQuantity)
                .HasPrecision(18, 4);

            entity.Property(item => item.AverageUnitCost)
                .HasPrecision(18, 2);

            entity.Property(item => item.Notes)
                .HasMaxLength(1000);

            entity.Property(item => item.RowVersion)
                .IsRowVersion();

            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => new { item.CompoundId, item.Sku }).IsUnique();
            entity.HasIndex(item => item.Name);

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInventoryMovement(ModelBuilder builder)
    {
        builder.Entity<InventoryMovement>(entity =>
        {
            entity.ToTable("InventoryMovements");
            entity.HasKey(movement => movement.Id);

            entity.Property(movement => movement.Quantity)
                .HasPrecision(18, 4);

            entity.Property(movement => movement.UnitCost)
                .HasPrecision(18, 2);

            entity.Property(movement => movement.Reference)
                .HasMaxLength(120);

            entity.Property(movement => movement.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(movement => movement.CompoundId);
            entity.HasIndex(movement => movement.StockItemId);
            entity.HasIndex(movement => movement.WorkOrderId);
            entity.HasIndex(movement => movement.PurchaseOrderItemId);
            entity.HasIndex(movement => movement.MovementType);
            entity.HasIndex(movement => movement.CreatedAtUtc);
            entity.HasIndex(movement => new { movement.CompoundId, movement.Reference })
                .IsUnique()
                .HasFilter("[Reference] IS NOT NULL");

            entity.HasOne(movement => movement.Compound)
                .WithMany()
                .HasForeignKey(movement => movement.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(movement => movement.StockItem)
                .WithMany(item => item.InventoryMovements)
                .HasForeignKey(movement => movement.StockItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(movement => movement.WorkOrder)
                .WithMany()
                .HasForeignKey(movement => movement.WorkOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(movement => movement.PurchaseOrderItem)
                .WithMany(item => item.InventoryMovements)
                .HasForeignKey(movement => movement.PurchaseOrderItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(movement => movement.CreatedByUser)
                .WithMany()
                .HasForeignKey(movement => movement.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProcurementRequest(ModelBuilder builder)
    {
        builder.Entity<ProcurementRequest>(entity =>
        {
            entity.ToTable("ProcurementRequests");
            entity.HasKey(request => request.Id);

            entity.Property(request => request.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(request => request.Reason)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(request => request.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(request => request.CompoundId);
            entity.HasIndex(request => request.Status);
            entity.HasIndex(request => request.Priority);
            entity.HasIndex(request => request.RelatedWorkOrderId);
            entity.HasIndex(request => request.CreatedAtUtc);

            entity.HasOne(request => request.Compound)
                .WithMany()
                .HasForeignKey(request => request.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(request => request.RequestedByUser)
                .WithMany()
                .HasForeignKey(request => request.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(request => request.ApprovedByUser)
                .WithMany()
                .HasForeignKey(request => request.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(request => request.RelatedWorkOrder)
                .WithMany()
                .HasForeignKey(request => request.RelatedWorkOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProcurementRequestItem(ModelBuilder builder)
    {
        builder.Entity<ProcurementRequestItem>(entity =>
        {
            entity.ToTable("ProcurementRequestItems");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Description)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(item => item.Quantity)
                .HasPrecision(18, 4);

            entity.Property(item => item.EstimatedUnitCost)
                .HasPrecision(18, 2);

            entity.HasIndex(item => item.ProcurementRequestId);
            entity.HasIndex(item => item.StockItemId);

            entity.HasOne(item => item.ProcurementRequest)
                .WithMany(request => request.Items)
                .HasForeignKey(item => item.ProcurementRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.StockItem)
                .WithMany()
                .HasForeignKey(item => item.StockItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePurchaseOrder(ModelBuilder builder)
    {
        builder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("PurchaseOrders");
            entity.HasKey(order => order.Id);

            entity.Property(order => order.OrderNumber)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(order => order.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(order => order.CompoundId);
            entity.HasIndex(order => order.VendorId);
            entity.HasIndex(order => order.ProcurementRequestId);
            entity.HasIndex(order => order.Status);
            entity.HasIndex(order => new { order.CompoundId, order.OrderNumber }).IsUnique();
            entity.HasIndex(order => order.CreatedAtUtc);

            entity.HasOne(order => order.Compound)
                .WithMany()
                .HasForeignKey(order => order.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(order => order.ProcurementRequest)
                .WithMany(request => request.PurchaseOrders)
                .HasForeignKey(order => order.ProcurementRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(order => order.Vendor)
                .WithMany()
                .HasForeignKey(order => order.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(order => order.CreatedByUser)
                .WithMany()
                .HasForeignKey(order => order.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePurchaseOrderItem(ModelBuilder builder)
    {
        builder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.ToTable("PurchaseOrderItems");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Description)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(item => item.QuantityOrdered)
                .HasPrecision(18, 4);

            entity.Property(item => item.QuantityReceived)
                .HasPrecision(18, 4);

            entity.Property(item => item.UnitCost)
                .HasPrecision(18, 2);

            entity.Property(item => item.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(item => item.PurchaseOrderId);
            entity.HasIndex(item => item.StockItemId);

            entity.HasOne(item => item.PurchaseOrder)
                .WithMany(order => order.Items)
                .HasForeignKey(item => item.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.StockItem)
                .WithMany()
                .HasForeignKey(item => item.StockItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWorkOrder(ModelBuilder builder)
    {
        builder.Entity<WorkOrder>(entity =>
        {
            entity.ToTable("WorkOrders");
            entity.HasKey(workOrder => workOrder.Id);

            entity.Property(workOrder => workOrder.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(workOrder => workOrder.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(workOrder => workOrder.EstimatedCost)
                .HasPrecision(18, 2);

            entity.Property(workOrder => workOrder.ActualCost)
                .HasPrecision(18, 2);

            entity.Property(workOrder => workOrder.CompletionNotes)
                .HasMaxLength(2000);

            entity.Property(workOrder => workOrder.CancellationReason)
                .HasMaxLength(500);

            entity.Property(workOrder => workOrder.SlaBreachReason)
                .HasMaxLength(1000);

            entity.Property(workOrder => workOrder.PreventiveMaintenanceOccurrenceKey)
                .HasMaxLength(100);

            entity.Property(workOrder => workOrder.RowVersion)
                .IsRowVersion();

            entity.HasIndex(workOrder => workOrder.Status);
            entity.HasIndex(workOrder => workOrder.CompoundId);
            entity.HasIndex(workOrder => workOrder.Priority);
            entity.HasIndex(workOrder => new { workOrder.SourceType, workOrder.SourceEntityId });
            entity.HasIndex(workOrder => workOrder.AssignedStaffMemberId);
            entity.HasIndex(workOrder => workOrder.AssignedVendorId);
            entity.HasIndex(workOrder => workOrder.PropertyUnitId);
            entity.HasIndex(workOrder => workOrder.DueAtUtc);
            entity.HasIndex(workOrder => workOrder.CreatedAtUtc);
            entity.HasIndex(workOrder => workOrder.MaintenanceAssetId);
            entity.HasIndex(workOrder => workOrder.MaintenanceSlaPolicyId);
            entity.HasIndex(workOrder => workOrder.SlaStatus);
            entity.HasIndex(workOrder => workOrder.ResponseDueAtUtc);
            entity.HasIndex(workOrder => workOrder.ResolutionDueAtUtc);
            entity.HasIndex(workOrder => workOrder.SlaEscalatedAtUtc);
            entity.HasIndex(workOrder => new { workOrder.CompoundId, workOrder.SourceType, workOrder.SourceEntityId, workOrder.PreventiveMaintenanceOccurrenceKey })
                .IsUnique()
                .HasFilter("[PreventiveMaintenanceOccurrenceKey] IS NOT NULL");

            entity.HasOne(workOrder => workOrder.AssignedStaffMember)
                .WithMany(staffMember => staffMember.AssignedWorkOrders)
                .HasForeignKey(workOrder => workOrder.AssignedStaffMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(workOrder => workOrder.AssignedVendor)
                .WithMany(vendor => vendor.AssignedWorkOrders)
                .HasForeignKey(workOrder => workOrder.AssignedVendorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(workOrder => workOrder.CreatedByUser)
                .WithMany()
                .HasForeignKey(workOrder => workOrder.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(workOrder => workOrder.Compound)
                .WithMany()
                .HasForeignKey(workOrder => workOrder.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(workOrder => workOrder.PropertyUnit)
                .WithMany()
                .HasForeignKey(workOrder => workOrder.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(workOrder => workOrder.MaintenanceAsset)
                .WithMany(asset => asset.WorkOrders)
                .HasForeignKey(workOrder => workOrder.MaintenanceAssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(workOrder => workOrder.MaintenanceSlaPolicy)
                .WithMany(policy => policy.WorkOrders)
                .HasForeignKey(workOrder => workOrder.MaintenanceSlaPolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWorkOrderCostItem(ModelBuilder builder)
    {
        builder.Entity<WorkOrderCostItem>(entity =>
        {
            entity.ToTable("WorkOrderCostItems");
            entity.HasKey(costItem => costItem.Id);

            entity.Property(costItem => costItem.Description)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(costItem => costItem.Amount)
                .HasPrecision(18, 2);

            entity.HasIndex(costItem => costItem.WorkOrderId);

            entity.HasOne(costItem => costItem.WorkOrder)
                .WithMany(workOrder => workOrder.CostItems)
                .HasForeignKey(costItem => costItem.WorkOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWorkOrderStatusHistory(ModelBuilder builder)
    {
        builder.Entity<WorkOrderStatusHistory>(entity =>
        {
            entity.ToTable("WorkOrderStatusHistories");
            entity.HasKey(history => history.Id);

            entity.Property(history => history.Note)
                .HasMaxLength(1000);

            entity.HasIndex(history => history.WorkOrderId);
            entity.HasIndex(history => history.ChangedByUserId);
            entity.HasIndex(history => history.NewStatus);
            entity.HasIndex(history => history.CreatedAtUtc);

            entity.HasOne(history => history.WorkOrder)
                .WithMany(workOrder => workOrder.StatusHistory)
                .HasForeignKey(history => history.WorkOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(history => history.ChangedByUser)
                .WithMany()
                .HasForeignKey(history => history.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWorkOrderRating(ModelBuilder builder)
    {
        builder.Entity<WorkOrderRating>(entity =>
        {
            entity.ToTable("WorkOrderRatings");
            entity.HasKey(rating => rating.Id);

            entity.Property(rating => rating.Comment)
                .HasMaxLength(1000);

            entity.HasIndex(rating => rating.WorkOrderId);
            entity.HasIndex(rating => rating.UserId);
            entity.HasIndex(rating => new { rating.WorkOrderId, rating.UserId })
                .IsUnique();

            entity.HasOne(rating => rating.WorkOrder)
                .WithMany(workOrder => workOrder.Ratings)
                .HasForeignKey(rating => rating.WorkOrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(rating => rating.User)
                .WithMany()
                .HasForeignKey(rating => rating.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }



    private static void ConfigureMaintenanceAsset(ModelBuilder builder)
    {
        builder.Entity<MaintenanceAsset>(entity =>
        {
            entity.ToTable("MaintenanceAssets");
            entity.HasKey(asset => asset.Id);

            entity.Property(asset => asset.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(asset => asset.Code)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(asset => asset.LocationDescription)
                .HasMaxLength(300);

            entity.Property(asset => asset.Manufacturer)
                .HasMaxLength(150);

            entity.Property(asset => asset.Model)
                .HasMaxLength(150);

            entity.Property(asset => asset.SerialNumber)
                .HasMaxLength(150);

            entity.Property(asset => asset.Notes)
                .HasMaxLength(1000);

            entity.Property(asset => asset.RowVersion)
                .IsRowVersion();

            entity.HasIndex(asset => asset.CompoundId);
            entity.HasIndex(asset => asset.BuildingId);
            entity.HasIndex(asset => asset.FloorId);
            entity.HasIndex(asset => asset.PropertyUnitId);
            entity.HasIndex(asset => asset.AssetType);
            entity.HasIndex(asset => asset.Status);
            entity.HasIndex(asset => asset.NextServiceDueAtUtc);
            entity.HasIndex(asset => new { asset.CompoundId, asset.Code }).IsUnique();

            entity.HasOne(asset => asset.Compound)
                .WithMany()
                .HasForeignKey(asset => asset.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(asset => asset.Building)
                .WithMany()
                .HasForeignKey(asset => asset.BuildingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(asset => asset.Floor)
                .WithMany()
                .HasForeignKey(asset => asset.FloorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(asset => asset.PropertyUnit)
                .WithMany()
                .HasForeignKey(asset => asset.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMaintenanceSlaPolicy(ModelBuilder builder)
    {
        builder.Entity<MaintenanceSlaPolicy>(entity =>
        {
            entity.ToTable("MaintenanceSlaPolicies");
            entity.HasKey(policy => policy.Id);

            entity.Property(policy => policy.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(policy => policy.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(policy => policy.CompoundId);
            entity.HasIndex(policy => policy.Priority);
            entity.HasIndex(policy => policy.SourceType);
            entity.HasIndex(policy => policy.IsActive);

            entity.HasOne(policy => policy.Compound)
                .WithMany()
                .HasForeignKey(policy => policy.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePreventiveMaintenancePlan(ModelBuilder builder)
    {
        builder.Entity<PreventiveMaintenancePlan>(entity =>
        {
            entity.ToTable("PreventiveMaintenancePlans");
            entity.HasKey(plan => plan.Id);

            entity.Property(plan => plan.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(plan => plan.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(plan => plan.Notes)
                .HasMaxLength(1000);

            entity.Property(plan => plan.LastGeneratedOccurrenceKey)
                .HasMaxLength(100);

            entity.HasIndex(plan => plan.CompoundId);
            entity.HasIndex(plan => plan.MaintenanceAssetId);
            entity.HasIndex(plan => plan.NextDueAtUtc);
            entity.HasIndex(plan => plan.IsActive);
            entity.HasIndex(plan => plan.AssignedStaffMemberId);
            entity.HasIndex(plan => plan.AssignedVendorId);

            entity.HasOne(plan => plan.Compound)
                .WithMany()
                .HasForeignKey(plan => plan.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(plan => plan.MaintenanceAsset)
                .WithMany(asset => asset.PreventiveMaintenancePlans)
                .HasForeignKey(plan => plan.MaintenanceAssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(plan => plan.AssignedStaffMember)
                .WithMany()
                .HasForeignKey(plan => plan.AssignedStaffMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(plan => plan.AssignedVendor)
                .WithMany()
                .HasForeignKey(plan => plan.AssignedVendorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOperationalChecklistTemplate(ModelBuilder builder)
    {
        builder.Entity<OperationalChecklistTemplate>(entity =>
        {
            entity.ToTable("OperationalChecklistTemplates");
            entity.HasKey(template => template.Id);

            entity.Property(template => template.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(template => template.Description)
                .HasMaxLength(1000);

            entity.HasIndex(template => template.CompoundId);
            entity.HasIndex(template => template.IsActive);

            entity.HasOne(template => template.Compound)
                .WithMany()
                .HasForeignKey(template => template.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOperationalChecklistTemplateItem(ModelBuilder builder)
    {
        builder.Entity<OperationalChecklistTemplateItem>(entity =>
        {
            entity.ToTable("OperationalChecklistTemplateItems");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(item => item.Description)
                .HasMaxLength(1000);

            entity.HasIndex(item => item.OperationalChecklistTemplateId);

            entity.HasOne(item => item.Template)
                .WithMany(template => template.Items)
                .HasForeignKey(item => item.OperationalChecklistTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureOperationalChecklistRun(ModelBuilder builder)
    {
        builder.Entity<OperationalChecklistRun>(entity =>
        {
            entity.ToTable("OperationalChecklistRuns");
            entity.HasKey(run => run.Id);

            entity.Property(run => run.SummaryNotes)
                .HasMaxLength(1000);

            entity.HasIndex(run => run.CompoundId);
            entity.HasIndex(run => run.OperationalChecklistTemplateId);
            entity.HasIndex(run => new { run.TargetType, run.TargetId });
            entity.HasIndex(run => run.Status);
            entity.HasIndex(run => run.StartedAtUtc);

            entity.HasOne(run => run.Compound)
                .WithMany()
                .HasForeignKey(run => run.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(run => run.Template)
                .WithMany()
                .HasForeignKey(run => run.OperationalChecklistTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(run => run.StartedByUser)
                .WithMany()
                .HasForeignKey(run => run.StartedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(run => run.CompletedByUser)
                .WithMany()
                .HasForeignKey(run => run.CompletedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOperationalChecklistRunItem(ModelBuilder builder)
    {
        builder.Entity<OperationalChecklistRunItem>(entity =>
        {
            entity.ToTable("OperationalChecklistRunItems");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(item => item.Description)
                .HasMaxLength(1000);

            entity.Property(item => item.Notes)
                .HasMaxLength(1000);

            entity.HasIndex(item => item.OperationalChecklistRunId);
            entity.HasIndex(item => item.Status);

            entity.HasOne(item => item.Run)
                .WithMany(run => run.Items)
                .HasForeignKey(item => item.OperationalChecklistRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureOperationalTask(ModelBuilder builder)
    {
        builder.Entity<OperationalTask>(entity =>
        {
            entity.ToTable("OperationalTasks");
            entity.HasKey(task => task.Id);

            entity.Property(task => task.Title)
                .IsRequired()
                .HasMaxLength(160);

            entity.Property(task => task.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(task => task.CompletionNotes)
                .HasMaxLength(1000);

            entity.Property(task => task.CancellationReason)
                .HasMaxLength(1000);

            entity.Property(task => task.RowVersion)
                .IsRowVersion();

            entity.HasIndex(task => task.CompoundId);
            entity.HasIndex(task => task.TaskType);
            entity.HasIndex(task => task.Priority);
            entity.HasIndex(task => task.Status);
            entity.HasIndex(task => task.AssignedToUserId);
            entity.HasIndex(task => task.CreatedByUserId);
            entity.HasIndex(task => task.DueAtUtc);
            entity.HasIndex(task => task.CreatedAtUtc);
            entity.HasIndex(task => new { task.RelatedEntityType, task.RelatedEntityId });
            entity.HasIndex(task => new { task.CompoundId, task.Status, task.Priority, task.DueAtUtc });

            entity.HasOne(task => task.Compound)
                .WithMany()
                .HasForeignKey(task => task.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(task => task.AssignedToUser)
                .WithMany()
                .HasForeignKey(task => task.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(task => task.CreatedByUser)
                .WithMany()
                .HasForeignKey(task => task.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(task => task.CompletedByUser)
                .WithMany()
                .HasForeignKey(task => task.CompletedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(task => task.CancelledByUser)
                .WithMany()
                .HasForeignKey(task => task.CancelledByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }



    private static void ConfigureBillingRule(ModelBuilder builder)
    {
        builder.Entity<BillingRule>(entity =>
        {
            entity.ToTable("BillingRules");
            entity.HasKey(rule => rule.Id);

            entity.Property(rule => rule.Name).IsRequired().HasMaxLength(150);
            entity.Property(rule => rule.Description).HasMaxLength(1000);
            entity.Property(rule => rule.FixedChargeAmount).HasPrecision(18, 2);
            entity.Property(rule => rule.RatePerUnit).HasPrecision(18, 4);
            entity.Property(rule => rule.MinimumChargeAmount).HasPrecision(18, 2);
            entity.Property(rule => rule.LateFeeFlatAmount).HasPrecision(18, 2);
            entity.Property(rule => rule.LateFeePercentage).HasPrecision(9, 4);
            entity.Property(rule => rule.Notes).HasMaxLength(1000);

            entity.HasIndex(rule => rule.CompoundId);
            entity.HasIndex(rule => rule.CompoundServiceId);
            entity.HasIndex(rule => rule.Status);
            entity.HasIndex(rule => rule.ChargeMode);
            entity.HasIndex(rule => new { rule.CompoundId, rule.Status, rule.EffectiveFrom });

            entity.HasOne(rule => rule.Compound)
                .WithMany()
                .HasForeignKey(rule => rule.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(rule => rule.CompoundService)
                .WithMany()
                .HasForeignKey(rule => rule.CompoundServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureBillingRuleTier(ModelBuilder builder)
    {
        builder.Entity<BillingRuleTier>(entity =>
        {
            entity.ToTable("BillingRuleTiers");
            entity.HasKey(tier => tier.Id);

            entity.Property(tier => tier.FromQuantity).HasPrecision(18, 4);
            entity.Property(tier => tier.ToQuantity).HasPrecision(18, 4);
            entity.Property(tier => tier.RatePerUnit).HasPrecision(18, 4);
            entity.Property(tier => tier.FixedAmount).HasPrecision(18, 2);

            entity.HasIndex(tier => tier.BillingRuleId);
            entity.HasIndex(tier => new { tier.BillingRuleId, tier.SortOrder });

            entity.HasOne(tier => tier.BillingRule)
                .WithMany(rule => rule.Tiers)
                .HasForeignKey(tier => tier.BillingRuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMeterReadingCorrection(ModelBuilder builder)
    {
        builder.Entity<MeterReadingCorrection>(entity =>
        {
            entity.ToTable("MeterReadingCorrections");
            entity.HasKey(correction => correction.Id);

            entity.Property(correction => correction.OriginalPreviousReading).HasPrecision(18, 4);
            entity.Property(correction => correction.OriginalCurrentReading).HasPrecision(18, 4);
            entity.Property(correction => correction.OriginalConsumption).HasPrecision(18, 4);
            entity.Property(correction => correction.OriginalAmount).HasPrecision(18, 2);
            entity.Property(correction => correction.CorrectedPreviousReading).HasPrecision(18, 4);
            entity.Property(correction => correction.CorrectedCurrentReading).HasPrecision(18, 4);
            entity.Property(correction => correction.CorrectedConsumption).HasPrecision(18, 4);
            entity.Property(correction => correction.CorrectedAmount).HasPrecision(18, 2);
            entity.Property(correction => correction.Reason).IsRequired().HasMaxLength(1000);
            entity.Property(correction => correction.DecisionReason).HasMaxLength(1000);

            entity.HasIndex(correction => correction.CompoundId);
            entity.HasIndex(correction => correction.MeterReadingId);
            entity.HasIndex(correction => correction.Status);
            entity.HasIndex(correction => new { correction.CompoundId, correction.Status, correction.RequestedAtUtc });

            entity.HasOne(correction => correction.Compound)
                .WithMany()
                .HasForeignKey(correction => correction.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(correction => correction.MeterReading)
                .WithMany()
                .HasForeignKey(correction => correction.MeterReadingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(correction => correction.Meter)
                .WithMany()
                .HasForeignKey(correction => correction.MeterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(correction => correction.PropertyUnit)
                .WithMany()
                .HasForeignKey(correction => correction.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureContractLifecycleEvent(ModelBuilder builder)
    {
        builder.Entity<ContractLifecycleEvent>(entity =>
        {
            entity.ToTable("ContractLifecycleEvents");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Reason).IsRequired().HasMaxLength(1000);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.MetadataJson).HasMaxLength(4000);

            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => new { item.ContractType, item.ContractId });
            entity.HasIndex(item => item.EventType);
            entity.HasIndex(item => item.EffectiveDate);
            entity.HasIndex(item => item.CreatedAtUtc);

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PropertyUnit)
                .WithMany()
                .HasForeignKey(item => item.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ResidentProfile)
                .WithMany()
                .HasForeignKey(item => item.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUnitHandoverChecklist(ModelBuilder builder)
    {
        builder.Entity<UnitHandoverChecklist>(entity =>
        {
            entity.ToTable("UnitHandoverChecklists");
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.PropertyUnitId);
            entity.HasIndex(item => item.ResidentProfileId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => new { item.CompoundId, item.Status, item.ScheduledDate });

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PropertyUnit)
                .WithMany()
                .HasForeignKey(item => item.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ResidentProfile)
                .WithMany()
                .HasForeignKey(item => item.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUnitHandoverChecklistItem(ModelBuilder builder)
    {
        builder.Entity<UnitHandoverChecklistItem>(entity =>
        {
            entity.ToTable("UnitHandoverChecklistItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).IsRequired().HasMaxLength(150);
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => item.UnitHandoverChecklistId);
            entity.HasIndex(item => new { item.UnitHandoverChecklistId, item.SortOrder });

            entity.HasOne(item => item.Checklist)
                .WithMany(checklist => checklist.Items)
                .HasForeignKey(item => item.UnitHandoverChecklistId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureResidentLifecycleProcess(ModelBuilder builder)
    {
        builder.Entity<ResidentLifecycleProcess>(entity =>
        {
            entity.ToTable("ResidentLifecycleProcesses");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.FinancialClearanceNotes).HasMaxLength(1000);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.PropertyUnitId);
            entity.HasIndex(item => item.ResidentProfileId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => new { item.CompoundId, item.Status, item.TargetDate });

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PropertyUnit)
                .WithMany()
                .HasForeignKey(item => item.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ResidentProfile)
                .WithMany()
                .HasForeignKey(item => item.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureResidentCustodyItem(ModelBuilder builder)
    {
        builder.Entity<ResidentCustodyItem>(entity =>
        {
            entity.ToTable("ResidentCustodyItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Identifier).IsRequired().HasMaxLength(120);
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.ReplacementFeeAmount).HasPrecision(18, 2);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.PropertyUnitId);
            entity.HasIndex(item => item.ResidentProfileId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => new { item.CompoundId, item.Identifier, item.Status });

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PropertyUnit)
                .WithMany()
                .HasForeignKey(item => item.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ResidentProfile)
                .WithMany()
                .HasForeignKey(item => item.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMoveLogisticsPermit(ModelBuilder builder)
    {
        builder.Entity<MoveLogisticsPermit>(entity =>
        {
            entity.ToTable("MoveLogisticsPermits");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TruckInfo).HasMaxLength(300);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.Property(item => item.DecisionReason).HasMaxLength(1000);
            entity.Property(item => item.CompletionNotes).HasMaxLength(1000);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.PropertyUnitId);
            entity.HasIndex(item => item.ResidentProfileId);
            entity.HasIndex(item => item.ResidentLifecycleProcessId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => new { item.CompoundId, item.Status, item.ScheduledStartAtUtc });

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PropertyUnit)
                .WithMany()
                .HasForeignKey(item => item.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ResidentProfile)
                .WithMany()
                .HasForeignKey(item => item.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ResidentLifecycleProcess)
                .WithMany(process => process.MoveLogisticsPermits)
                .HasForeignKey(item => item.ResidentLifecycleProcessId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUnitReadinessRecord(ModelBuilder builder)
    {
        builder.Entity<UnitReadinessRecord>(entity =>
        {
            entity.ToTable("UnitReadinessRecords");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.PropertyUnitId);
            entity.HasIndex(item => item.ResidentLifecycleProcessId);
            entity.HasIndex(item => item.OperationalChecklistRunId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => new { item.CompoundId, item.Status });

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PropertyUnit)
                .WithMany()
                .HasForeignKey(item => item.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ResidentLifecycleProcess)
                .WithMany(process => process.UnitReadinessRecords)
                .HasForeignKey(item => item.ResidentLifecycleProcessId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.OperationalChecklistRun)
                .WithMany()
                .HasForeignKey(item => item.OperationalChecklistRunId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureUnitDamageLiability(ModelBuilder builder)
    {
        builder.Entity<UnitDamageLiability>(entity =>
        {
            entity.ToTable("UnitDamageLiabilities");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EstimatedAmount).HasPrecision(18, 2);
            entity.Property(item => item.Description).IsRequired().HasMaxLength(1000);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.PropertyUnitId);
            entity.HasIndex(item => item.ResidentProfileId);
            entity.HasIndex(item => item.ResidentLifecycleProcessId);
            entity.HasIndex(item => item.Status);

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PropertyUnit)
                .WithMany()
                .HasForeignKey(item => item.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ResidentProfile)
                .WithMany()
                .HasForeignKey(item => item.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ResidentLifecycleProcess)
                .WithMany(process => process.DamageLiabilities)
                .HasForeignKey(item => item.ResidentLifecycleProcessId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.FinancialAdjustment)
                .WithMany()
                .HasForeignKey(item => item.FinancialAdjustmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.WorkOrder)
                .WithMany()
                .HasForeignKey(item => item.WorkOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOwnershipTransferRequest(ModelBuilder builder)
    {
        builder.Entity<OwnershipTransferRequest>(entity =>
        {
            entity.ToTable("OwnershipTransferRequests");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Reason).IsRequired().HasMaxLength(1000);
            entity.Property(item => item.DecisionReason).HasMaxLength(1000);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.PropertyUnitId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => new { item.CompoundId, item.Status, item.RequestedAtUtc });

            entity.HasIndex(item => item.PropertyUnitId, "IX_OwnershipTransferRequests_PropertyUnitId_PendingApproval")
                .IsUnique()
                .HasFilter("[Status] = 1");

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PropertyUnit)
                .WithMany()
                .HasForeignKey(item => item.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.CurrentOwnerResidentProfile)
                .WithMany()
                .HasForeignKey(item => item.CurrentOwnerResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.NewOwnerResidentProfile)
                .WithMany()
                .HasForeignKey(item => item.NewOwnerResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInstallmentRescheduleRequest(ModelBuilder builder)
    {
        builder.Entity<InstallmentRescheduleRequest>(entity =>
        {
            entity.ToTable("InstallmentRescheduleRequests");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OriginalAmount).HasPrecision(18, 2);
            entity.Property(item => item.RequestedAmount).HasPrecision(18, 2);
            entity.Property(item => item.Reason).IsRequired().HasMaxLength(1000);
            entity.Property(item => item.DecisionReason).HasMaxLength(1000);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.InstallmentScheduleItemId);
            entity.HasIndex(item => item.PropertySaleContractId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => new { item.CompoundId, item.Status, item.RequestedAtUtc });

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.InstallmentScheduleItem)
                .WithMany()
                .HasForeignKey(item => item.InstallmentScheduleItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PropertySaleContract)
                .WithMany()
                .HasForeignKey(item => item.PropertySaleContractId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ResidentProfile)
                .WithMany()
                .HasForeignKey(item => item.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


    private static void ConfigureSupportCase(ModelBuilder builder)
    {
        builder.Entity<SupportCase>(entity =>
        {
            entity.ToTable("SupportCases");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).IsRequired().HasMaxLength(200);
            entity.Property(item => item.Description).IsRequired().HasMaxLength(4000);
            entity.Property(item => item.AssignmentNote).HasMaxLength(1000);
            entity.Property(item => item.EscalationReason).HasMaxLength(1000);
            entity.Property(item => item.ResolutionSummary).HasMaxLength(2000);
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.ResidentProfileId);
            entity.HasIndex(item => item.PropertyUnitId);
            entity.HasIndex(item => item.AssignedToUserId);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.Priority);
            entity.HasIndex(item => item.Category);
            entity.HasIndex(item => item.DueAtUtc);
            entity.HasIndex(item => item.CreatedAtUtc);
            entity.HasIndex(item => new { item.CompoundId, item.Status, item.Priority, item.DueAtUtc });
            entity.HasIndex(item => new { item.SourceType, item.SourceEntityId });

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ResidentProfile)
                .WithMany()
                .HasForeignKey(item => item.ResidentProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.PropertyUnit)
                .WithMany()
                .HasForeignKey(item => item.PropertyUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.AssignedToUser)
                .WithMany()
                .HasForeignKey(item => item.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSupportCaseEvent(ModelBuilder builder)
    {
        builder.Entity<SupportCaseEvent>(entity =>
        {
            entity.ToTable("SupportCaseEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Description).IsRequired().HasMaxLength(500);
            entity.Property(item => item.InternalNote).HasMaxLength(2000);
            entity.HasIndex(item => item.SupportCaseId);
            entity.HasIndex(item => item.ActorUserId);
            entity.HasIndex(item => item.EventType);
            entity.HasIndex(item => item.CreatedAtUtc);

            entity.HasOne(item => item.SupportCase)
                .WithMany(supportCase => supportCase.Events)
                .HasForeignKey(item => item.SupportCaseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.ActorUser)
                .WithMany()
                .HasForeignKey(item => item.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSupportSlaPolicy(ModelBuilder builder)
    {
        builder.Entity<SupportSlaPolicy>(entity =>
        {
            entity.ToTable("SupportSlaPolicies");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => new { item.CompoundId, item.Category, item.Priority }).IsUnique();

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSavedReport(ModelBuilder builder)
    {
        builder.Entity<SavedReport>(entity =>
        {
            entity.ToTable("SavedReports");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).IsRequired().HasMaxLength(150);
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.Property(item => item.FilterJson).IsRequired().HasMaxLength(4000);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.CreatedByUserId);
            entity.HasIndex(item => item.ReportType);
            entity.HasIndex(item => item.IsActive);
            entity.HasIndex(item => new { item.CompoundId, item.ReportType, item.IsActive });

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.CreatedByUser)
                .WithMany()
                .HasForeignKey(item => item.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureReportExportJob(ModelBuilder builder)
    {
        builder.Entity<ReportExportJob>(entity =>
        {
            entity.ToTable("ReportExportJobs");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FilterJson).IsRequired().HasMaxLength(4000);
            entity.Property(item => item.FileName).HasMaxLength(300);
            entity.Property(item => item.DownloadPath).HasMaxLength(1000);
            entity.Property(item => item.FailureReason).HasMaxLength(1000);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.RequestedByUserId);
            entity.HasIndex(item => item.ReportType);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.RequestedAtUtc);
            entity.HasIndex(item => new { item.CompoundId, item.ReportType, item.Status });

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.RequestedByUser)
                .WithMany()
                .HasForeignKey(item => item.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }


    private static void ConfigureSystemSetting(ModelBuilder builder)
    {
        builder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Key).IsRequired().HasMaxLength(150);
            entity.Property(item => item.Value).IsRequired().HasMaxLength(4000);
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.HasIndex(item => item.CompoundId);
            entity.HasIndex(item => item.Key);
            entity.HasIndex(item => new { item.CompoundId, item.Key }).IsUnique();

            entity.HasOne(item => item.Compound)
                .WithMany()
                .HasForeignKey(item => item.CompoundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.UpdatedByUser)
                .WithMany()
                .HasForeignKey(item => item.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLicenseProfile(ModelBuilder builder)
    {
        builder.Entity<LicenseProfile>(entity =>
        {
            entity.ToTable("LicenseProfiles");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.LicensedTo).IsRequired().HasMaxLength(200);
            entity.Property(item => item.LicenseKeyFingerprint).IsRequired().HasMaxLength(128);
            entity.Property(item => item.Notes).HasMaxLength(1000);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.Plan);
            entity.HasIndex(item => item.ExpiresAtUtc);

            entity.HasOne(item => item.UpdatedByUser)
                .WithMany()
                .HasForeignKey(item => item.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureBackgroundJobRun(ModelBuilder builder)
    {
        builder.Entity<BackgroundJobRun>(entity =>
        {
            entity.ToTable("BackgroundJobRuns");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.JobName).IsRequired().HasMaxLength(150);
            entity.Property(item => item.WorkerName).HasMaxLength(150);
            entity.Property(item => item.ErrorMessage).HasMaxLength(1000);
            entity.Property(item => item.MetadataJson).HasMaxLength(4000);
            entity.HasIndex(item => item.JobName);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.StartedAtUtc);
            entity.HasIndex(item => new { item.JobName, item.Status, item.StartedAtUtc });
        });
    }

    private static void ConfigureSystemHealthSnapshot(ModelBuilder builder)
    {
        builder.Entity<SystemHealthSnapshot>(entity =>
        {
            entity.ToTable("SystemHealthSnapshots");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Summary).IsRequired().HasMaxLength(1000);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.CapturedAtUtc);
        });
    }

    private static void ConfigureIntegrationFailureEvent(ModelBuilder builder)
    {
        builder.Entity<IntegrationFailureEvent>(entity =>
        {
            entity.ToTable("IntegrationFailureEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.IntegrationName).IsRequired().HasMaxLength(150);
            entity.Property(item => item.OperationName).IsRequired().HasMaxLength(150);
            entity.Property(item => item.ErrorMessage).IsRequired().HasMaxLength(1000);
            entity.Property(item => item.ResolutionNote).HasMaxLength(1000);
            entity.Property(item => item.MetadataJson).HasMaxLength(4000);
            entity.HasIndex(item => item.IntegrationName);
            entity.HasIndex(item => item.OperationName);
            entity.HasIndex(item => item.Status);
            entity.HasIndex(item => item.LastOccurredAtUtc);
            entity.HasIndex(item => new { item.IntegrationName, item.OperationName, item.Status });

            entity.HasOne(item => item.ResolvedByUser)
                .WithMany()
                .HasForeignKey(item => item.ResolvedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

}
