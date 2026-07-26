namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionRefactoringCompatibilityTests
{
    [Theory]
    [MemberData(nameof(GetSupportedRefactoringProviderIds))]
    public async Task GIVEN_SupportedRefactoring_WHEN_ProbingRuntime_THEN_ShouldRemainVisibleAndReplayable(
        string providerId,
        string? diagnosticId)
    {
        await BuiltInCodeActionCompatibilityAssertion.VerifyAsync(
            providerId,
            diagnosticId,
            requireSingleMatchingAction: false);
    }

    public static TheoryData<string, string?> GetSupportedRefactoringProviderIds()
    {
        return BuiltInCodeActionCompatibilityAssertion.GetProviderIds(BuiltInCodeActionAuditKind.Refactoring);
    }
}

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionCodeFixCompatibilityTests
{
    [Theory]
    [MemberData(nameof(GetSupportedCodeFixProviderIds))]
    public async Task GIVEN_SupportedCodeFix_WHEN_ProbingRuntime_THEN_ShouldRemainVisibleAndReplayable(
        string providerId,
        string? diagnosticId)
    {
        await BuiltInCodeActionCompatibilityAssertion.VerifyAsync(
            providerId,
            diagnosticId,
            requireSingleMatchingAction: true);
    }

    public static TheoryData<string, string?> GetSupportedCodeFixProviderIds()
    {
        return BuiltInCodeActionCompatibilityAssertion.GetProviderIds(BuiltInCodeActionAuditKind.CodeFix);
    }
}

internal static class BuiltInCodeActionCompatibilityAssertion
{
    internal static TheoryData<string, string?> GetProviderIds(BuiltInCodeActionAuditKind kind)
    {
        var data = new TheoryData<string, string?>();

        foreach (var auditCase in BuiltInCodeActionAuditCases.SupportedCompatibilityCases.Where(item => item.Kind == kind))
        {
            data.Add(auditCase.ProviderId, auditCase.ExpectedDiagnosticId);
        }

        return data;
    }

    internal static async Task VerifyAsync(
        string providerId,
        string? diagnosticId,
        bool requireSingleMatchingAction)
    {
        var auditCase = BuiltInCodeActionAuditCases.SupportedCompatibilityCases.Single(
            item => item.ProviderId == providerId
                && item.ExpectedDiagnosticId == diagnosticId);

        var probe = await BuiltInCodeActionAuditHarness.ProbeAsync(auditCase);

        probe.LocationStatus.Should().Be(SelectorResolveStatus.Resolved);
        probe.RuntimeOutcome.Should().Be(BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable, probe.FailureMessage ?? string.Join(", ", probe.CandidateTitles));
        if (requireSingleMatchingAction)
        {
            probe.MatchingActionCount.Should().Be(1, auditCase.SourceNote);
        }

        probe.IsVisibleInList.Should().BeTrue(auditCase.ToolName ?? auditCase.ProviderId);
    }
}
