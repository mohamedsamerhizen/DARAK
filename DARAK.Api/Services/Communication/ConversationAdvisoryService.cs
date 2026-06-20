using DARAK.Api.DTOs.Communication;
using DARAK.Api.Enums;
using DARAK.Api.Interfaces;

namespace DARAK.Api.Services;

public sealed class ConversationAdvisoryService : IConversationAdvisoryService
{
    public ConversationPriority GetDefaultPriority(ConversationIssueType issueType)
    {
        return issueType switch
        {
            ConversationIssueType.MaintenanceWaterLeak => ConversationPriority.High,
            ConversationIssueType.MaintenanceElectricityIssue => ConversationPriority.High,
            ConversationIssueType.VisitorDeniedEntry => ConversationPriority.High,
            ConversationIssueType.PaymentProofIssue => ConversationPriority.High,
            ConversationIssueType.BillingMeterReadingIssue => ConversationPriority.Normal,
            ConversationIssueType.BillingHighAmount => ConversationPriority.Normal,
            ConversationIssueType.ViolationObjection => ConversationPriority.Normal,
            ConversationIssueType.RentContractIssue => ConversationPriority.Normal,
            ConversationIssueType.DocumentAccessIssue => ConversationPriority.Normal,
            _ => ConversationPriority.Normal
        };
    }

    public IReadOnlyList<ConversationAdvisoryFlagResponse> GetAdvisoryFlags(
        ConversationIssueType issueType,
        ConversationLinkedEntityType linkedEntityType = ConversationLinkedEntityType.None)
    {
        var flags = new List<ConversationAdvisoryFlagResponse>();

        flags.AddRange(issueType switch
        {
            ConversationIssueType.MaintenanceWaterLeak =>
            [
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Critical,
                    "Potential urgent maintenance case",
                    "Water leak reports can affect the resident unit and nearby units if not triaged quickly.",
                    "Assign maintenance staff quickly and check whether nearby units may be affected.",
                    IsBlocking: false),
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Warning,
                    "Do not close before field review",
                    "A water leak conversation should stay open until an admin or maintenance note confirms the review.",
                    "Add an internal review note before resolving the conversation.",
                    IsBlocking: true)
            ],
            ConversationIssueType.MaintenanceElectricityIssue =>
            [
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Warning,
                    "Possible service interruption",
                    "Electricity issues may be isolated to the unit or connected to a wider building problem.",
                    "Check recent work orders and ask the resident for affected rooms/devices before closing.",
                    IsBlocking: false)
            ],
            ConversationIssueType.BillingMeterReadingIssue =>
            [
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Warning,
                    "Meter reading review required",
                    "The resident is challenging the bill through a meter-reading issue.",
                    "Check the previous meter reading, current reading, and average consumption before replying. Add an internal admin review note before resolving.",
                    IsBlocking: true),
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Info,
                    "Compare consumption pattern",
                    "A large difference from the resident's normal usage may indicate a reading or leak issue.",
                    "Compare this cycle against the last three available readings when possible.",
                    IsBlocking: false)
            ],
            ConversationIssueType.BillingHighAmount =>
            [
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Warning,
                    "High bill objection",
                    "The resident believes the amount is higher than expected.",
                    "Review bill lines, previous balance, late fees, discounts, and payment history before sending a final reply.",
                    IsBlocking: false)
            ],
            ConversationIssueType.PaymentProofIssue =>
            [
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Critical,
                    "Finance verification required",
                    "Payment proof issues can cause incorrect manual payment handling.",
                    "Verify payment reference number and payment attempts before marking anything as paid.",
                    IsBlocking: true),
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Warning,
                    "Avoid duplicate payment handling",
                    "The same payment reference may already exist under a pending or failed attempt.",
                    "Search existing payments and attempts by reference before creating manual adjustments.",
                    IsBlocking: false)
            ],
            ConversationIssueType.VisitorDeniedEntry =>
            [
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Warning,
                    "Entry denial review",
                    "Visitor access issues may involve guard logs and pass status.",
                    "Check visitor pass status and guard access records before replying.",
                    IsBlocking: false)
            ],
            ConversationIssueType.ViolationObjection =>
            [
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Warning,
                    "Violation objection",
                    "The resident is objecting to an administrative violation or fine.",
                    "Review the violation reason, evidence, fine status, and previous resident history before resolving.",
                    IsBlocking: false)
            ],
            ConversationIssueType.RentContractIssue =>
            [
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Info,
                    "Contract context required",
                    "Rent contract issues usually need contract dates and invoice status.",
                    "Check active rent contract and unpaid invoices before replying.",
                    IsBlocking: false)
            ],
            ConversationIssueType.DocumentAccessIssue =>
            [
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Info,
                    "Document access review",
                    "The resident may be missing access to a private or unit-related document.",
                    "Check document visibility, owner user, and related entity before changing access.",
                    IsBlocking: false)
            ],
            _ =>
            [
                new ConversationAdvisoryFlagResponse(
                    AdvisoryFlagSeverity.Info,
                    "General support conversation",
                    "No special operational rule is attached to this issue type yet.",
                    "Classify the problem and assign the right owner if it becomes operational.",
                    IsBlocking: false)
            ]
        });

        if (linkedEntityType != ConversationLinkedEntityType.None)
        {
            flags.Add(new ConversationAdvisoryFlagResponse(
                AdvisoryFlagSeverity.Info,
                "Linked entity context",
                $"This conversation is linked to {linkedEntityType}.",
                "Open the linked entity before sending a final administrative decision.",
                IsBlocking: false));
        }

        return flags;
    }
}
