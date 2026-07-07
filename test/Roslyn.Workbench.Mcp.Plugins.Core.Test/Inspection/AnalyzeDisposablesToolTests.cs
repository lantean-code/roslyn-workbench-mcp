namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class AnalyzeDisposablesToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnDisposableFindings()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new AnalyzeDisposablesTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "analyze-disposables", target, new AnalyzeDisposablesRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
            },
        });

        result.Data!.Findings.Items.Select(static finding => finding.Kind).Should().Contain("UndisposedLocal");
    }
}
