namespace DARAK.Api.Helpers;

public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";

    public const string StructureAdministrators = "SuperAdmin,CompoundAdmin";

    public const string StructureReaders = "SuperAdmin,CompoundAdmin,Accountant";

    public const string ResidentManagementAdministrators = "SuperAdmin,CompoundAdmin";

    public const string ResidentManagementReaders = "SuperAdmin,CompoundAdmin,Accountant";

    public const string BillingManagers = "SuperAdmin,CompoundAdmin,Accountant";

    public const string PaymentManagers = "SuperAdmin,CompoundAdmin,Accountant";

    public const string PaymentRefundManagers = "SuperAdmin,CompoundAdmin";

    public const string ContractManagers = "SuperAdmin,CompoundAdmin,Accountant";

    public const string ContractRefundManagers = "SuperAdmin,CompoundAdmin";

    public const string Resident = "Resident";

    public const string Guard = "Guard";

    public const string MaintenanceStaff = "MaintenanceStaff";

    public const string VisitorPassAdministrators = "SuperAdmin,CompoundAdmin";

    public const string MaintenanceAdministrators = "SuperAdmin,CompoundAdmin";

    public const string ComplaintAdministrators = "SuperAdmin,CompoundAdmin";

    public const string ViolationAdministrators = "SuperAdmin,CompoundAdmin";

    public const string ViolationFineReaders = "SuperAdmin,CompoundAdmin,Accountant";

    public const string ViolationFineManagers = "SuperAdmin,CompoundAdmin";

    public const string CommunicationParticipants = "SuperAdmin,CompoundAdmin,Resident";

    public const string CommunicationManagers = "SuperAdmin,CompoundAdmin";

    public const string DocumentParticipants = "SuperAdmin,CompoundAdmin,Resident";

    public const string DocumentManagers = "SuperAdmin,CompoundAdmin";

    public const string OperationsParticipants = "SuperAdmin,CompoundAdmin,Resident";

    public const string OperationsManagers = "SuperAdmin,CompoundAdmin";

    public const string ApprovalManagers = "SuperAdmin,CompoundAdmin,Accountant";

    public const string ApprovalDecisionManagers = "SuperAdmin,CompoundAdmin";

    public const string RiskFlagReaders = "SuperAdmin,CompoundAdmin,Accountant";

    public const string RiskFlagManagers = "SuperAdmin,CompoundAdmin";

    public const string RiskFlagClosureManagers = "SuperAdmin,CompoundAdmin";

    public const string FinanceReaders = "SuperAdmin,CompoundAdmin,Accountant";

    public const string FinanceManagers = "SuperAdmin,CompoundAdmin,Accountant";

    public const string FinancialAdjustmentManagers = "SuperAdmin,CompoundAdmin";

    public const string AuditReaders = "SuperAdmin,CompoundAdmin,Accountant";

    public const string OperationalCommandCenterReaders = "SuperAdmin,CompoundAdmin,Accountant";

    public const string OperationalCommandCenterManagers = "SuperAdmin,CompoundAdmin";

    public const string SupportCaseReaders = "SuperAdmin,CompoundAdmin,Accountant";

    public const string SupportCaseManagers = "SuperAdmin,CompoundAdmin";

    public const string ManagementReportReaders = "SuperAdmin,CompoundAdmin,Accountant";

    public const string ManagementReportManagers = "SuperAdmin,CompoundAdmin,Accountant";

    public const string SystemReaders = "SuperAdmin,CompoundAdmin,Accountant";

    public const string SystemManagers = "SuperAdmin,CompoundAdmin";
}


