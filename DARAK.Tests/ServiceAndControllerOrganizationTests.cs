using DARAK.Api.Controllers;
using DARAK.Api.Extensions;
using DARAK.Api.Interfaces;
using DARAK.Api.Services;
using DARAK.Api.Services.Notifications;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DARAK.Tests;

public sealed class ServiceAndControllerOrganizationTests
{
    [Fact]
    public void Phase13_ApplicationServices_ShouldBeRegisteredThroughSingleExtension()
    {
        var services = new ServiceCollection();

        services.AddDarakApplicationServices();

        var expectedScopedRegistrations = new Dictionary<Type, Type>
        {
            [typeof(ITokenService)] = typeof(TokenService),
            [typeof(IRefreshTokenService)] = typeof(RefreshTokenService),
            [typeof(ICompoundAccessService)] = typeof(CompoundAccessService),
            [typeof(IUserCompoundAssignmentService)] = typeof(UserCompoundAssignmentService),
            [typeof(IAdminUserService)] = typeof(AdminUserService),
            [typeof(ICompoundStructureService)] = typeof(CompoundStructureService),
            [typeof(ICurrentUserService)] = typeof(CurrentUserService),
            [typeof(IResidentService)] = typeof(ResidentService),
            [typeof(IOccupancyService)] = typeof(OccupancyService),
            [typeof(ICompoundServiceCatalogService)] = typeof(CompoundServiceCatalogService),
            [typeof(IBillingCycleService)] = typeof(BillingCycleService),
            [typeof(IUtilityBillService)] = typeof(UtilityBillService),
            [typeof(IUtilityBillingService)] = typeof(UtilityBillingService),
            [typeof(IOverdueStatusService)] = typeof(OverdueStatusService),
            [typeof(IResidentFinancialHealthService)] = typeof(ResidentFinancialHealthService),
            [typeof(IMeterService)] = typeof(MeterService),
            [typeof(IResidentPortalService)] = typeof(ResidentPortalService),
            [typeof(IPaymentService)] = typeof(PaymentService),
            [typeof(IPropertySaleService)] = typeof(PropertySaleService),
            [typeof(IRentContractService)] = typeof(RentContractService),
            [typeof(IRentInvoiceService)] = typeof(RentInvoiceService),
            [typeof(IPropertyContractsService)] = typeof(PropertyContractsService),
            [typeof(IAdminPortalService)] = typeof(AdminPortalService),
            [typeof(IVisitorPassService)] = typeof(VisitorPassService),
            [typeof(IMaintenanceService)] = typeof(MaintenanceService),
            [typeof(IComplaintViolationService)] = typeof(ComplaintViolationService),
            [typeof(IAnnouncementService)] = typeof(AnnouncementService),
            [typeof(ICommercialCommunicationService)] = typeof(CommercialCommunicationService),
            [typeof(IResidentNotificationService)] = typeof(ResidentNotificationService),
            [typeof(ICommunityPollService)] = typeof(CommunityPollService),
            [typeof(ICommunicationService)] = typeof(CommunicationService),
            [typeof(INotificationOutboxService)] = typeof(NotificationOutboxService),
            [typeof(IEmailSender)] = typeof(SmtpEmailSender),
            [typeof(IConversationAdvisoryService)] = typeof(ConversationAdvisoryService),
            [typeof(IActivityTimelineService)] = typeof(ActivityTimelineService),
            [typeof(IConversationService)] = typeof(ConversationService),
            [typeof(IDocumentService)] = typeof(DocumentService),
            [typeof(IDocumentManagementService)] = typeof(DocumentManagementService),
            [typeof(IStaffMemberService)] = typeof(StaffMemberService),
            [typeof(IServiceVendorService)] = typeof(ServiceVendorService),
            [typeof(IOperationsService)] = typeof(OperationsService),
            [typeof(IAnalyticsService)] = typeof(AnalyticsService),
            [typeof(IApprovalService)] = typeof(ApprovalService),
            [typeof(IResidentRiskFlagService)] = typeof(ResidentRiskFlagService),
            [typeof(IFinancialControlService)] = typeof(FinancialControlService),
            [typeof(IAuditLogService)] = typeof(AuditLogService),
            [typeof(IOperationalCommandCenterService)] = typeof(OperationalCommandCenterService),
            [typeof(ICommercialEngineService)] = typeof(CommercialEngineService),
            [typeof(ISupportCaseService)] = typeof(SupportCaseService),
            [typeof(IManagementReportService)] = typeof(ManagementReportService),
            [typeof(ISystemAdministrationService)] = typeof(SystemAdministrationService)
        };

        foreach (var (serviceType, implementationType) in expectedScopedRegistrations)
        {
            services.Should().ContainSingle(descriptor =>
                descriptor.ServiceType == serviceType &&
                descriptor.ImplementationType == implementationType &&
                descriptor.Lifetime == ServiceLifetime.Scoped,
                $"{serviceType.Name} should be registered as a scoped service through AddDarakApplicationServices");
        }

        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IHttpContextAccessor));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(ISmsSender));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void Phase13_CriticalControllerRoutes_ShouldRemainStableAfterOrganizationRefactor()
    {
        var routes = GetEndpointRoutes().ToHashSet(StringComparer.OrdinalIgnoreCase);

        routes.Should().Contain(new[]
        {
            "POST api/admin/approvals/requests",
            "GET api/admin/approvals/requests",
            "POST api/admin/approvals/requests/{id:guid}/approve",
            "GET api/admin/approvals/dashboard",
            "GET api/admin/risk-flags",
            "POST api/admin/risk-flags",
            "POST api/admin/risk-flags/{id:guid}/resolve",
            "GET api/admin/risk-flags/dashboard",
            "GET api/admin/notifications/outbox",
            "GET api/admin/finance/dashboard",
            "GET api/admin/finance/aging-report",
            "GET api/admin/finance/revenue-summary",
            "POST api/admin/finance/adjustments",
            "GET api/admin/audit/logs",
            "GET api/admin/audit/dashboard",
            "GET api/admin/audit/entities/{entityType}/{entityId:guid}",
            "GET api/admin/audit/residents/{residentProfileId:guid}",
            "GET api/admin/operations/command-center",
            "GET api/admin/operations/sla-breaches",
            "GET api/admin/operations/staff-performance",
            "GET api/admin/operations/compound-health",
            "POST api/admin/operations/tasks",
            "POST api/admin/communication-automation/campaigns",
            "POST api/admin/communication-automation/campaigns/{id:guid}/send",
            "GET api/admin/document-management/dashboard",
            "POST api/admin/document-management/requirements",
            "POST api/admin/document-management/documents/{id:guid}/approve",
            "POST api/admin/notifications/process-due",
            "GET api/admin/commercial-engine/dashboard",
            "POST api/admin/commercial-engine/billing-rules",
            "POST api/admin/commercial-engine/meter-corrections/{id:guid}/approve",
            "POST api/admin/commercial-engine/ownership-transfers/{id:guid}/approve",
            "POST api/admin/commercial-engine/installment-reschedules/{id:guid}/approve",
            "GET api/admin/support/cases",
            "POST api/admin/support/cases",
            "POST api/admin/support/cases/{id:guid}/escalate",
            "GET api/admin/support/dashboard",
            "GET api/admin/reports/financial",
            "GET api/admin/reports/occupancy",
            "GET api/admin/reports/risk-audit",
            "POST api/admin/reports/saved",
            "POST api/admin/reports/exports",
            "GET api/admin/system/settings",
            "PUT api/admin/system/settings",
            "GET api/admin/system/license",
            "PUT api/admin/system/license",
            "POST api/admin/system/maintenance-mode",
            "GET api/admin/system/health",
            "GET api/admin/system/background-jobs",
            "POST api/admin/system/background-jobs",
            "POST api/admin/system/integration-failures",
            "GET api/system/version",
            "GET api/admin/communication/conversations",
            "POST api/resident/communication/conversations",
            "POST api/resident/account/bills/{billId:guid}/dispute"
        });
    }

    private static IEnumerable<string> GetEndpointRoutes()
    {
        var controllerTypes = typeof(ApiControllerBase).Assembly
            .GetTypes()
            .Where(type => type.IsAssignableTo(typeof(ControllerBase)) && !type.IsAbstract);

        foreach (var controllerType in controllerTypes)
        {
            var controllerRoute = controllerType
                .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
                .OfType<RouteAttribute>()
                .SingleOrDefault()
                ?.Template;

            if (string.IsNullOrWhiteSpace(controllerRoute))
            {
                continue;
            }

            foreach (var method in controllerType.GetMethods())
            {
                foreach (var httpMethodAttribute in method
                    .GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true)
                    .OfType<HttpMethodAttribute>())
                {
                    var actionRoute = CombineRoutes(controllerRoute, httpMethodAttribute.Template);

                    foreach (var httpMethod in httpMethodAttribute.HttpMethods)
                    {
                        yield return $"{httpMethod} {actionRoute}";
                    }
                }
            }
        }
    }

    private static string CombineRoutes(string controllerRoute, string? actionRoute)
    {
        if (string.IsNullOrWhiteSpace(actionRoute))
        {
            return controllerRoute.Trim('/');
        }

        return $"{controllerRoute.TrimEnd('/')}/{actionRoute.TrimStart('/')}";
    }
}
