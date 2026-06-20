namespace DARAK.Api.DTOs.System;

public sealed class CommercialPresentationQuery
{
    public Guid? CompoundId { get; init; }

    public int ScenarioLimit { get; init; } = 12;
}

public sealed record DemoSeedBlueprintResponse(
    Guid? CompoundId,
    int ReadinessScore,
    string SeedStatus,
    IReadOnlyCollection<DemoSeedEntityPlanResponse> EntityPlans,
    IReadOnlyCollection<DemoSeedScenarioResponse> Scenarios,
    IReadOnlyCollection<string> MissingEvidence,
    IReadOnlyCollection<string> SafeSeedRules,
    DateTime GeneratedAtUtc);

public sealed record DemoSeedEntityPlanResponse(
    string EntityKey,
    string DisplayName,
    int ExistingCount,
    int RecommendedMinimum,
    int SuggestedToCreate,
    string Purpose);

public sealed record DemoSeedScenarioResponse(
    string ScenarioKey,
    string Title,
    string DemoStory,
    string RequiredData,
    bool HasEnoughData);

public sealed record CommercialDemoModeResponse(
    Guid? CompoundId,
    int DemoScore,
    string DemoStatus,
    string OpeningNarrative,
    IReadOnlyCollection<CommercialDemoSectionResponse> Sections,
    IReadOnlyCollection<CommercialDemoWalkthroughStepResponse> Walkthrough,
    IReadOnlyCollection<BuyerQuestionAnswerResponse> BuyerQuestions,
    DateTime GeneratedAtUtc);

public sealed record CommercialDemoSectionResponse(
    string SectionKey,
    string Title,
    string Status,
    string TalkTrack,
    IReadOnlyCollection<CommercialDemoSignalResponse> Signals);

public sealed record CommercialDemoSignalResponse(
    string Label,
    int Count,
    string Meaning);

public sealed record CommercialDemoWalkthroughStepResponse(
    int StepNumber,
    string Title,
    string ScreenOrEndpoint,
    string WhatToShow,
    string BuyerValue);

public sealed record BuyerQuestionAnswerResponse(
    string Question,
    string Answer,
    string EvidenceToShow);

public sealed record BuyerPresentationPackResponse(
    Guid? CompoundId,
    string ProductOneLiner,
    string ValueProposition,
    IReadOnlyCollection<string> PrimaryBuyerTypes,
    IReadOnlyCollection<BuyerDemoAgendaItemResponse> DemoAgenda,
    IReadOnlyCollection<BuyerFeatureBucketResponse> FeatureBuckets,
    IReadOnlyCollection<BuyerObjectionHandlingResponse> ObjectionHandling,
    IReadOnlyCollection<string> HandoffAssets,
    IReadOnlyCollection<string> NextActions,
    DateTime GeneratedAtUtc);

public sealed record BuyerDemoAgendaItemResponse(
    int Order,
    string Title,
    string Narrative,
    string SuccessSignal);

public sealed record BuyerFeatureBucketResponse(
    string Bucket,
    string BusinessValue,
    IReadOnlyCollection<string> Modules);

public sealed record BuyerObjectionHandlingResponse(
    string Objection,
    string Response,
    string ProofPoint);
