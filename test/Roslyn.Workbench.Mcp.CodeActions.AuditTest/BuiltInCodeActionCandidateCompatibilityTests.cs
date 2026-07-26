namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionCandidateCompatibilityTests
{
    [Fact]
    public void GIVEN_PendingCompilerBackedProviders_WHEN_ReadingCandidateCases_THEN_ShouldTrackEveryProvider()
    {
        var expectedProviderIds = new List<string>();
        foreach (var family in BuiltInCodeActionLedger.Families)
        {
            if (family.Kind == BuiltInCodeActionFamilyKind.CodeFix
                && family.AuditStatus == BuiltInCodeActionAuditStatus.PendingReplayValidation)
            {
                expectedProviderIds.Add(family.ProviderId);
            }
        }

        var candidateProviderIds = BuiltInCodeActionAuditCases.CandidateCompatibilityCases
            .Select(static auditCase => auditCase.ProviderId)
            .ToArray();

        candidateProviderIds.Should().Equal(expectedProviderIds);
    }

    [Theory]
    [MemberData(nameof(GetCandidateProviderIds))]
    public async Task GIVEN_HiddenReplayCandidate_WHEN_ProbingRuntime_THEN_ShouldBeDeterministicReplayableAndRemainHidden(string providerId)
    {
        var auditCase = BuiltInCodeActionAuditCases.CandidateCompatibilityCases.Single(
            item => string.Equals(item.ProviderId, providerId, StringComparison.Ordinal));

        var probe = await BuiltInCodeActionAuditHarness.ProbeAsync(auditCase);

        probe.LocationStatus.Should().Be(SelectorResolveStatus.Resolved);

        var expectedDiagnosticId = auditCase.ExpectedDiagnosticId;
        expectedDiagnosticId.Should().NotBeNullOrWhiteSpace();

        probe.DiagnosticIds.Should().Contain(expectedDiagnosticId);
        probe.MatchingActionCount.Should().Be(1, probe.FailureMessage ?? string.Join(", ", probe.CandidateTitles));
        probe.RuntimeOutcome.Should().Be(BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable, probe.FailureMessage ?? string.Join(", ", probe.CandidateTitles));
        probe.IsVisibleInList.Should().BeFalse(auditCase.ProviderId);
    }

    public static TheoryData<string> GetCandidateProviderIds()
    {
        var data = new TheoryData<string>();
        foreach (var auditCase in BuiltInCodeActionAuditCases.RunnableCandidateCompatibilityCases)
        {
            data.Add(auditCase.ProviderId);
        }

        return data;
    }
}
