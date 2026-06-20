namespace DARAK.Api.DTOs.System;

public sealed class FinalDeliveryQuery
{
    public Guid? CompoundId { get; init; }

    public int Days { get; init; } = 30;

    public int ItemLimit { get; init; } = 20;
}

public sealed record CommercialModuleRegistryResponse(
    Guid? CompoundId,
    int TotalModules,
    int ReadyModules,
    int ConditionalModules,
    int BlockedModules,
    IReadOnlyCollection<CommercialModuleRegistryItemResponse> Modules,
    DateTime GeneratedAtUtc);

public sealed record CommercialModuleRegistryItemResponse(
    string ModuleKey,
    string DisplayName,
    string Area,
    bool HasConfiguration,
    bool HasOperationalEvidence,
    int EvidenceCount,
    string Status,
    string CommercialValue,
    string BuyerDemoPath);

public sealed record ProductCapabilityMapResponse(
    Guid? CompoundId,
    int TotalCapabilities,
    int ReleasedCapabilities,
    int ConditionalCapabilities,
    IReadOnlyCollection<ProductCapabilityItemResponse> Capabilities,
    DateTime GeneratedAtUtc);

public sealed record ProductCapabilityItemResponse(
    string Area,
    string Capability,
    string Status,
    string Evidence,
    string BuyerValue);

public sealed record BuyerDemoReadinessResponse(
    Guid? CompoundId,
    int DemoScore,
    string DemoStatus,
    int ReadyScenarioCount,
    int BlockedScenarioCount,
    IReadOnlyCollection<BuyerDemoScenarioResponse> Scenarios,
    IReadOnlyCollection<string> DemoWarnings,
    DateTime GeneratedAtUtc);

public sealed record BuyerDemoScenarioResponse(
    string ScenarioKey,
    string Title,
    string Module,
    bool IsReady,
    string Evidence,
    string DemoScript,
    string RiskIfMissing);

public sealed record ClientOnboardingReadinessResponse(
    Guid? CompoundId,
    int OnboardingScore,
    string OnboardingStatus,
    int ReadyStepCount,
    int BlockedStepCount,
    IReadOnlyCollection<ClientOnboardingStepResponse> Steps,
    IReadOnlyCollection<string> RequiredActions,
    DateTime GeneratedAtUtc);

public sealed record ClientOnboardingStepResponse(
    string StepKey,
    string Owner,
    string Title,
    bool IsReady,
    string Evidence,
    string RequiredAction);

public sealed record FinalCommercialDeliveryScorecardResponse(
    Guid? CompoundId,
    int FinalScore,
    string FinalStatus,
    int CriticalBlockers,
    int Warnings,
    int ReadyModules,
    int TotalModules,
    int ReadyDemoScenarios,
    int TotalDemoScenarios,
    CommercialDataFootprintResponse DataFootprint,
    IReadOnlyCollection<FinalDeliveryActionResponse> Actions,
    IReadOnlyCollection<string> ValueSummary,
    DateTime GeneratedAtUtc);

public sealed record CommercialDataFootprintResponse(
    int Compounds,
    int PropertyUnits,
    int Residents,
    int ActiveOccupancies,
    int FinancialRecords,
    int MaintenanceRecords,
    int AccessRecords,
    int CommunicationRecords,
    int GovernanceRecords,
    int ComplianceRecords);

public sealed record FinalDeliveryActionResponse(
    string Area,
    string Severity,
    string Action,
    string Owner,
    int PriorityRank);
