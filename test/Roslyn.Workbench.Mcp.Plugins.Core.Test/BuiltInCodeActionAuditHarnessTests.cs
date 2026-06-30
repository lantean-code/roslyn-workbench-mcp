using AwesomeAssertions;

using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.TestSupport;

using Xunit;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class BuiltInCodeActionAuditHarnessTests
{
    [Fact]
    public void GIVEN_CurrentAuditLedger_WHEN_QueryingFailedDraftValidationCandidates_THEN_ShouldHaveNoResidualBacklog()
    {
        BuiltInCodeActionAuditCases.FailedDraftValidationCandidates.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_CurrentAuditLedger_WHEN_QueryingHiddenDraftValidationCandidates_THEN_ShouldHaveNoResidualHiddenBacklog()
    {
        BuiltInCodeActionAuditCases.HiddenReplayFamilies.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(GetPromotedDraftValidationCandidates))]
    public async Task GIVEN_PromotedDraftValidationCandidate_WHEN_ResolvingAndDiscovering_THEN_ShouldRemainReplayable(BuiltInCodeActionAuditCase auditCase)
    {
        var probe = await BuiltInCodeActionAuditHarness.ProbeAsync(auditCase);

        probe.LocationStatus.Should().Be(SelectorResolveStatus.Resolved);
        probe.RuntimeOutcome.Should().Be(BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable, probe.FailureMessage ?? string.Join(", ", probe.CandidateTitles));
    }

    [Theory]
    [MemberData(nameof(GetPromotedDraftValidationCandidates))]
    public async Task GIVEN_PromotedDraftValidationCandidate_WHEN_ListingVisibleActions_THEN_ShouldBeVisible(BuiltInCodeActionAuditCase auditCase)
    {
        var probe = await BuiltInCodeActionAuditHarness.ProbeAsync(auditCase);

        probe.LocationStatus.Should().Be(SelectorResolveStatus.Resolved);
        probe.IsVisibleInList.Should().BeTrue(auditCase.ToolName ?? auditCase.ProviderId);
    }

    [Theory]
    [MemberData(nameof(GetPendingPromotionCandidates))]
    public async Task GIVEN_PendingPromotionCandidate_WHEN_ResolvingAndDiscovering_THEN_ShouldBeReplayableInRuntime(BuiltInCodeActionAuditCase auditCase)
    {
        var probe = await BuiltInCodeActionAuditHarness.ProbeAsync(auditCase);

        probe.LocationStatus.Should().Be(SelectorResolveStatus.Resolved);
        probe.RuntimeOutcome.Should().Be(BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable, probe.FailureMessage ?? string.Join(", ", probe.CandidateTitles));
    }

    [Theory]
    [MemberData(nameof(GetPendingPromotionCandidates))]
    public async Task GIVEN_PendingPromotionCandidate_WHEN_ListingVisibleActions_THEN_ShouldBeVisible(BuiltInCodeActionAuditCase auditCase)
    {
        var probe = await BuiltInCodeActionAuditHarness.ProbeAsync(auditCase);

        probe.LocationStatus.Should().Be(SelectorResolveStatus.Resolved);
        probe.IsVisibleInList.Should().BeTrue(auditCase.ToolName ?? auditCase.ProviderId);
    }

    public static TheoryData<BuiltInCodeActionAuditCase> GetPromotedDraftValidationCandidates()
    {
        return CreateTheoryData(BuiltInCodeActionAuditCases.PromotedDraftValidationCandidates);
    }

    public static TheoryData<BuiltInCodeActionAuditCase> GetPendingPromotionCandidates()
    {
        return CreateTheoryData(BuiltInCodeActionAuditCases.PendingPromotionCandidates);
    }

    private static TheoryData<BuiltInCodeActionAuditCase> CreateTheoryData(IReadOnlyList<BuiltInCodeActionAuditCase> auditCases)
    {
        var data = new TheoryData<BuiltInCodeActionAuditCase>();

        foreach (var auditCase in auditCases)
        {
            data.Add(auditCase);
        }

        return data;
    }
}
