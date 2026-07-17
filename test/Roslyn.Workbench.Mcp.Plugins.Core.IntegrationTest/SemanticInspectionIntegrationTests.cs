namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class SemanticInspectionIntegrationTests
{
    [Fact]
    public async Task GIVEN_LoadedSemanticWorkspace_WHEN_InspectingDiagnosticsOperationsAndFlow_THEN_ShouldReturnRoslynProjections()
    {
        await using var fixture = await InspectionSampleFixture.CreateAsync();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var session = new PluginComponentTestSession(coordinator, BundledPluginCatalogueFactory.CreateCatalogue());
        var snapshot = new SnapshotPrecondition
        {
            WorkspaceEpoch = openResult.Context.WorkspaceEpoch!.Value,
        };

        var diagnostics = await session.ExecuteQueryAsync<GetDiagnosticsRequest, DiagnosticsData>(
            "get-diagnostics",
            new GetDiagnosticsRequest
            {
                Scope = new ScopeSelector
                {
                    Kind = ScopeKind.Document,
                    Document = new DocumentSelector
                    {
                        Path = "Formatting.cs",
                    },
                },
                Ids = ["CS0219"],
            }, TestContext.Current.CancellationToken);
        var operation = await session.ExecuteQueryAsync<GetOperationTreeRequest, OperationTreeData>(
            "get-operation-tree",
            new GetOperationTreeRequest
            {
                Location = fixture.GetLocation("formatter.Format(\"hi\")"),
                ExpectedSnapshot = snapshot,
            }, TestContext.Current.CancellationToken);
        var flow = await session.ExecuteQueryAsync<AnalyzeControlFlowRequest, ControlFlowAnalysisData>(
            "analyze-control-flow",
            new AnalyzeControlFlowRequest
            {
                Location = fixture.GetLocation("if (trimmed.Length == 0)"),
                ExpectedSnapshot = snapshot,
            }, TestContext.Current.CancellationToken);
        var exceptionalGraph = await session.ExecuteQueryAsync<GetControlFlowGraphRequest, ControlFlowGraphData>(
            "get-control-flow-graph",
            new GetControlFlowGraphRequest
            {
                Symbol = new SymbolSelector
                {
                    DocumentationCommentId = "M:Sample.FlowSamples.AnalyseExceptional(System.String)",
                },
            }, TestContext.Current.CancellationToken);
        var boundedGraph = await session.ExecuteQueryAsync<GetControlFlowGraphRequest, ControlFlowGraphData>(
            "get-control-flow-graph",
            new GetControlFlowGraphRequest
            {
                Symbol = new SymbolSelector
                {
                    DocumentationCommentId = "M:Sample.FlowSamples.Analyse(System.String)",
                },
                MaxBlocks = 1,
            }, TestContext.Current.CancellationToken);

        diagnostics.Data!.Diagnostics.Items.Should().ContainSingle(static diagnostic => diagnostic.Id == "CS0219");
        operation.Data!.Root!.Kind.Should().Contain("Invocation");
        flow.Data!.Exits.Should().NotBeEmpty();
        exceptionalGraph.Data!.Regions.Select(static region => region.Kind).Should().Contain(static kind => kind.Contains("Try", StringComparison.Ordinal) || kind.Contains("Catch", StringComparison.Ordinal) || kind.Contains("Finally", StringComparison.Ordinal));
        boundedGraph.Data!.Blocks.Should().HaveCount(1);
        boundedGraph.Data.BlocksTruncated.Should().BeTrue();
    }
}
