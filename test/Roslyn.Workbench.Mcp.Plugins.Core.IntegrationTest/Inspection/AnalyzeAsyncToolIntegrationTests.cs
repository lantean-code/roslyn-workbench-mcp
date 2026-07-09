namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class AnalyzeAsyncToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnAsyncFindings()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new AnalyzeAsyncTool();

        openResult.Outcome.Should().Be(ToolOutcome.Succeeded);

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "analyze-async", target, new AnalyzeAsyncRequest
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

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Findings.Items.Select(static finding => finding.Kind).Should().Contain(["AsyncWithoutAwait", "UnawaitedTask"]);
    }
}
