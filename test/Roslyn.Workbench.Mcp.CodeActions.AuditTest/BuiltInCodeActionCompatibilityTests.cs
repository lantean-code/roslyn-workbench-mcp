namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionCompatibilityTests
{
    [Theory]
    [MemberData(nameof(GetSupportedCompatibilityProviderIds))]
    public async Task GIVEN_SupportedCompatibilityCase_WHEN_ProbingRuntime_THEN_ShouldRemainVisibleAndReplayable(string providerId)
    {
        var auditCase = BuiltInCodeActionAuditCases.SupportedCompatibilityCases.Single(item => item.ProviderId == providerId);
        var probe = await BuiltInCodeActionAuditHarness.ProbeAsync(auditCase);

        probe.LocationStatus.Should().Be(SelectorResolveStatus.Resolved);
        probe.RuntimeOutcome.Should().Be(BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable, probe.FailureMessage ?? string.Join(", ", probe.CandidateTitles));
        probe.IsVisibleInList.Should().BeTrue(auditCase.ToolName ?? auditCase.ProviderId);
    }

    public static TheoryData<string> GetSupportedCompatibilityProviderIds()
    {
        var data = new TheoryData<string>();

        foreach (var auditCase in BuiltInCodeActionAuditCases.SupportedCompatibilityCases)
        {
            data.Add(auditCase.ProviderId);
        }

        return data;
    }
}
