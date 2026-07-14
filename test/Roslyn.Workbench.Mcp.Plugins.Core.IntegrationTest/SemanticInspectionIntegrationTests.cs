using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class SemanticInspectionIntegrationTests
{
    [Fact]
    public async Task GIVEN_LoadedSemanticWorkspace_WHEN_InspectingDiagnosticsOperationsAndFlow_THEN_ShouldReturnRoslynProjections()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var registry = BundledPluginCatalogueFactory.CreateCatalogue();
        var snapshot = new SnapshotPrecondition
        {
            WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
        };

        var diagnostics = await PluginToolTestHarness.InvokeAsync<DiagnosticsData>(coordinator, registry, "get-diagnostics", new Dictionary<string, JsonElement>
        {
            ["scope"] = JsonSerializer.SerializeToElement(new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "Formatting.cs",
                },
            }),
            ["ids"] = JsonSerializer.SerializeToElement(new[] { "CS0219" }),
        });
        var operation = await PluginToolTestHarness.InvokeAsync<OperationTreeData>(coordinator, registry, "get-operation-tree", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("formatter.Format(\"hi\")")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(snapshot),
        });
        var flow = await PluginToolTestHarness.InvokeAsync<ControlFlowAnalysisData>(coordinator, registry, "analyze-control-flow", new Dictionary<string, JsonElement>
        {
            ["location"] = JsonSerializer.SerializeToElement(fixture.GetLocation("if (trimmed.Length == 0)")),
            ["expectedSnapshot"] = JsonSerializer.SerializeToElement(snapshot),
        });
        var exceptionalGraph = await PluginToolTestHarness.InvokeAsync<ControlFlowGraphData>(coordinator, registry, "get-control-flow-graph", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FlowSamples.AnalyseExceptional(System.String)",
            }),
        });
        var boundedGraph = await PluginToolTestHarness.InvokeAsync<ControlFlowGraphData>(coordinator, registry, "get-control-flow-graph", new Dictionary<string, JsonElement>
        {
            ["symbol"] = JsonSerializer.SerializeToElement(new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.FlowSamples.Analyse(System.String)",
            }),
            ["maxBlocks"] = JsonSerializer.SerializeToElement(1),
        });

        diagnostics.Data!.Diagnostics.Items.Should().ContainSingle(static diagnostic => diagnostic.Id == "CS0219");
        operation.Data!.Root!.Kind.Should().Contain("Invocation");
        flow.Data!.Exits.Should().NotBeEmpty();
        exceptionalGraph.Data!.Regions.Select(static region => region.Kind).Should().Contain(static kind => kind.Contains("Try", StringComparison.Ordinal) || kind.Contains("Catch", StringComparison.Ordinal) || kind.Contains("Finally", StringComparison.Ordinal));
        boundedGraph.Data!.Blocks.Should().HaveCount(1);
        boundedGraph.Data.BlocksTruncated.Should().BeTrue();
    }
}
