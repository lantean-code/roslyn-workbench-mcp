namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class GetCodeMetricsToolTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnMetrics()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetCodeMetricsTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-code-metrics", target, new GetCodeMetricsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
            },
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.ConditionalSamples.DescribeValue(System.Int32)",
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Metrics.Items.Should().Contain(static metric => metric.Symbol!.DisplayName.Contains("ConditionalSamples.DescribeValue", StringComparison.Ordinal));
        result.Data.Metrics.Items.Should().Contain(static metric => metric.CyclomaticComplexity >= 3);
        result.Data.Metrics.Items.Should().Contain(static metric => metric.LogicalLines >= 5);
    }
}
