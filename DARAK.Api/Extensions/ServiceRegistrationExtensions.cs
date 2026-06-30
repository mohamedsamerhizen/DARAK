using DARAK.Api.Interfaces;
using DARAK.Api.Services;
using DARAK.Api.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace DARAK.Api.Extensions;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddDarakApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IAccessCodeHasher, AccessCodeHasher>();
        services.AddScoped<ICompoundAccessService, CompoundAccessService>();
        services.AddScoped<IUserCompoundAssignmentService, UserCompoundAssignmentService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<ICompoundStructureService, CompoundStructureService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IResidentService, ResidentService>();
        services.AddScoped<IOccupancyService, OccupancyService>();
        services.AddScoped<ICompoundServiceCatalogService, CompoundServiceCatalogService>();
        services.AddScoped<IBillingCycleService, BillingCycleService>();
        services.AddScoped<IUtilityBillService, UtilityBillService>();
        services.AddScoped<IUtilityBillingService, UtilityBillingService>();
        services.AddScoped<IOverdueStatusService, OverdueStatusService>();
        services.AddScoped<IResidentFinancialHealthService, ResidentFinancialHealthService>();
        services.AddScoped<IMeterService, MeterService>();
        services.AddScoped<ISmartMeterOperationsService, SmartMeterOperationsService>();
        services.AddScoped<IResidentPortalService, ResidentPortalService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();
        services.AddScoped<IPropertySaleService, PropertySaleService>();
        services.AddScoped<IRentContractService, RentContractService>();
        services.AddScoped<IRentInvoiceService, RentInvoiceService>();
        services.AddScoped<IPropertyContractsService, PropertyContractsService>();
        services.AddScoped<IAdminPortalService, AdminPortalService>();
        services.AddScoped<IVisitorPassService, VisitorPassService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IComplaintViolationService, ComplaintViolationService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<ICommercialCommunicationService, CommercialCommunicationService>();
        services.AddScoped<IResidentCommunicationOperationsService, ResidentCommunicationOperationsService>();
        services.AddScoped<IResidentNotificationService, ResidentNotificationService>();
        services.AddScoped<ICommunityPollService, CommunityPollService>();
        services.AddScoped<ICommunicationService, CommunicationService>();
        services.AddScoped<INotificationOutboxService, NotificationOutboxService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddHttpClient<ISmsSender, HttpSmsSender>();
        services.AddHostedService<NotificationDeliveryWorker>();
        services.AddScoped<IConversationAdvisoryService, ConversationAdvisoryService>();
        services.AddScoped<IActivityTimelineService, ActivityTimelineService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentManagementService, DocumentManagementService>();
        services.AddScoped<IStaffMemberService, StaffMemberService>();
        services.AddScoped<IServiceVendorService, ServiceVendorService>();
        services.AddScoped<IOperationsService, OperationsService>();
        services.AddScoped<IMaintenanceReliabilityService, MaintenanceReliabilityService>();
        services.AddScoped<IProcurementInventoryService, ProcurementInventoryService>();
        services.AddScoped<IAccessControlOperationsService, AccessControlOperationsService>();
        services.AddScoped<IResidentLifecycleService, ResidentLifecycleService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IResidentRiskFlagService, ResidentRiskFlagService>();
        services.AddScoped<IFinancialControlService, FinancialControlService>();
        services.AddScoped<IFinancialGovernanceService, FinancialGovernanceService>();
        services.AddScoped<ICollectionsLegalComplianceService, CollectionsLegalComplianceService>();
        services.AddScoped<IGovernanceArbitrationService, GovernanceArbitrationService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IOperationalCommandCenterService, OperationalCommandCenterService>();
        services.AddScoped<ICommercialEngineService, CommercialEngineService>();
        services.AddScoped<ISupportCaseService, SupportCaseService>();
        services.AddScoped<IManagementReportService, ManagementReportService>();
        services.AddScoped<ISystemAdministrationService, SystemAdministrationService>();
        services.AddScoped<IComplianceReleaseGovernanceService, ComplianceReleaseGovernanceService>();
        services.AddScoped<ICommercialProductizationService, CommercialProductizationService>();
        services.AddScoped<ICommercialPresentationService, CommercialPresentationService>();
        services.AddScoped<IDarak360ProfileService, Darak360ProfileService>();
        services.AddScoped<IIntelligenceEscalationService, IntelligenceEscalationService>();
        services.AddScoped<ISaasTenantIntelligenceService, SaasTenantIntelligenceService>();

        return services;
    }
}
