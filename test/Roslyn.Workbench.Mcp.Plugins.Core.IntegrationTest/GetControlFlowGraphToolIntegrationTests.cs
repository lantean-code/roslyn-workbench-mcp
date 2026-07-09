using System.Text.Json;

using Roslyn.Workbench.Mcp.TestSupport;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

[Trait("Category", "Integration")]
public sealed class GetControlFlowGraphToolIntegrationTests
{
    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_RequestingControlFlowGraph_THEN_ShouldReturnProjectedRegions()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        plugin.Register(registry);

        var result = await PluginToolTestHarness.InvokeAsync<ControlFlowGraphData>(coordinator, registry, "get-control-flow-graph", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FlowSamples.AnalyseExceptional(System.String)",
            }),
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Regions.Select(static region => region.Kind).Should().Contain(static kind => kind.Contains("Try", StringComparison.Ordinal) || kind.Contains("Catch", StringComparison.Ordinal) || kind.Contains("Finally", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_RequestingBoundedControlFlowGraph_THEN_ShouldRespectRequestedLimits()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = WorkspaceCoordinatorFactory.Create(toolExecutionServices: BundledCoreToolExecutionServicesFactory.Create());
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var plugin = new BundledCorePlugin();
        var registry = new PluginRegistry(plugin.Metadata);
        plugin.Register(registry);

        var boundedBlocks = await PluginToolTestHarness.InvokeAsync<ControlFlowGraphData>(coordinator, registry, "get-control-flow-graph", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FlowSamples.Analyse(System.String)",
            }),
            ["maxBlocks"] = JsonSerializer.SerializeToElement(1),
        });
        var boundedRegions = await PluginToolTestHarness.InvokeAsync<ControlFlowGraphData>(coordinator, registry, "get-control-flow-graph", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FlowSamples.AnalyseExceptional(System.String)",
            }),
            ["maxRegions"] = JsonSerializer.SerializeToElement(1),
        });

        boundedBlocks.Outcome.Should().Be(ToolOutcome.Succeeded);
        boundedBlocks.Data!.Blocks.Should().HaveCount(1);
        boundedBlocks.Data.BlocksTruncated.Should().BeTrue();
        boundedRegions.Outcome.Should().Be(ToolOutcome.Succeeded);
        boundedRegions.Data!.Regions.Should().HaveCount(1);
        boundedRegions.Data.RegionsTruncated.Should().BeTrue();
    }

}
