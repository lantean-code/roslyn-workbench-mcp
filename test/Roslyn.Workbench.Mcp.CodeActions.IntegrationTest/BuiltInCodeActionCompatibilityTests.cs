using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionCompatibilityTests
{
    [Theory]
    [MemberData(nameof(GetSupportedCompatibilityCases))]
    public async Task GIVEN_SupportedCompatibilityCase_WHEN_ProbingRuntime_THEN_ShouldRemainVisibleAndReplayable(BuiltInCodeActionAuditCase auditCase)
    {
        var probe = await BuiltInCodeActionAuditHarness.ProbeAsync(auditCase);

        probe.LocationStatus.Should().Be(SelectorResolveStatus.Resolved);
        probe.RuntimeOutcome.Should().Be(BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable, probe.FailureMessage ?? string.Join(", ", probe.CandidateTitles));
        probe.IsVisibleInList.Should().BeTrue(auditCase.ToolName ?? auditCase.ProviderId);
    }

    public static TheoryData<BuiltInCodeActionAuditCase> GetSupportedCompatibilityCases()
    {
        var data = new TheoryData<BuiltInCodeActionAuditCase>();

        foreach (var auditCase in BuiltInCodeActionAuditCases.SupportedCompatibilityCases)
        {
            data.Add(auditCase);
        }

        return data;
    }
}
