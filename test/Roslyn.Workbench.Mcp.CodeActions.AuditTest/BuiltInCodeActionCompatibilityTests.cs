namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionRefactoringCompatibilityTests
{
    [Theory]
    [MemberData(nameof(GetSupportedRefactoringProviderIds))]
    public async Task GIVEN_SupportedRefactoring_WHEN_ProbingRuntime_THEN_ShouldRemainVisibleAndReplayable(string providerId)
    {
        await BuiltInCodeActionCompatibilityAssertion.VerifyAsync(providerId);
    }

    public static TheoryData<string> GetSupportedRefactoringProviderIds()
    {
        return BuiltInCodeActionCompatibilityAssertion.GetProviderIds(BuiltInCodeActionAuditKind.Refactoring);
    }
}

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionCodeFixCompatibilityTests
{
    [Theory]
    [MemberData(nameof(GetSupportedCodeFixProviderIds))]
    public async Task GIVEN_SupportedCodeFix_WHEN_ProbingRuntime_THEN_ShouldRemainVisibleAndReplayable(string providerId)
    {
        await BuiltInCodeActionCompatibilityAssertion.VerifyAsync(providerId);
    }

    public static TheoryData<string> GetSupportedCodeFixProviderIds()
    {
        return BuiltInCodeActionCompatibilityAssertion.GetProviderIds(BuiltInCodeActionAuditKind.CodeFix);
    }
}

internal static class BuiltInCodeActionCompatibilityAssertion
{
    internal static TheoryData<string> GetProviderIds(BuiltInCodeActionAuditKind kind)
    {
        var data = new TheoryData<string>();

        foreach (var auditCase in BuiltInCodeActionAuditCases.SupportedCompatibilityCases.Where(item => item.Kind == kind))
        {
            data.Add(auditCase.ProviderId);
        }

        return data;
    }

    internal static async Task VerifyAsync(string providerId)
    {
        var auditCase = BuiltInCodeActionAuditCases.SupportedCompatibilityCases.Single(item => item.ProviderId == providerId);
        var probe = await BuiltInCodeActionAuditHarness.ProbeAsync(auditCase);

        probe.LocationStatus.Should().Be(SelectorResolveStatus.Resolved);
        probe.RuntimeOutcome.Should().Be(BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable, probe.FailureMessage ?? string.Join(", ", probe.CandidateTitles));
        probe.IsVisibleInList.Should().BeTrue(auditCase.ToolName ?? auditCase.ProviderId);
    }
}
