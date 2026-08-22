namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class SemanticInspectionIntegrationTests
{
    [Fact]
    public async Task GIVEN_LoadedSemanticWorkspace_WHEN_InspectingDiagnosticsOperationsAndFlow_THEN_ShouldReturnRoslynProjections()
    {
        using var fixture = InspectionSampleFixture.Create();
        var asyncAnalysisPath = Path.Combine(fixture.WorkspaceRoot, "AsyncAnalysis.cs");
        await File.WriteAllTextAsync(
            asyncAnalysisPath,
            """
            using System.Threading.Tasks;

            namespace Sample;

            public static class AsyncAnalysisSamples
            {
                public static async Task DelayAsync()
                {
                    await Task.Delay(1);
                }

                public static async void FireAndForget()
                {
                    await Task.Delay(1);
                }
            }
            """,
            TestContext.Current.CancellationToken);

        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var session = new PluginComponentTestSession(coordinator, BundledPluginCatalogueFactory.CreateCatalogue());
        var snapshot = BundledComponentWorkspaceFactory.CreateSnapshot(openResult);

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

        var asyncDiagnostics = await session.ExecuteQueryAsync<AnalyzeAsyncRequest, AsyncAnalysisData>(
            "analyze-async",
            new AnalyzeAsyncRequest
            {
                Scope = new ScopeSelector
                {
                    Kind = ScopeKind.Document,
                    Document = new DocumentSelector
                    {
                        Path = "AsyncAnalysis.cs",
                    },
                },
            }, TestContext.Current.CancellationToken);

        var operation = await session.ExecuteQueryAsync<GetOperationTreeRequest, OperationTreeData>(
            "get-operation-tree",
            new GetOperationTreeRequest
            {
                Location = fixture.GetLocation("formatter.Format(\"hi\")"),
                ExpectedSnapshot = snapshot,
            }, TestContext.Current.CancellationToken);

        var controlFlowLocation = fixture.GetSpanSelection("if (trimmed.Length == 0)", "}");
        var flow = await session.ExecuteQueryAsync<AnalyzeControlFlowRequest, ControlFlowAnalysisData>(
            "analyze-control-flow",
            new AnalyzeControlFlowRequest
            {
                Location = controlFlowLocation,
                ExpectedSnapshot = snapshot,
            }, TestContext.Current.CancellationToken);

        var partialFlow = await session.ExecuteQueryAsync<AnalyzeControlFlowRequest, ControlFlowAnalysisData>(
            "analyze-control-flow",
            new AnalyzeControlFlowRequest
            {
                Location = fixture.GetLocation("if (trimmed.Length == 0)"),
                ExpectedSnapshot = snapshot,
            }, TestContext.Current.CancellationToken);

        var dataFlowLocation = fixture.GetLocation("trimmed.ToUpperInvariant()");
        var dataFlow = await session.ExecuteQueryAsync<AnalyzeDataFlowRequest, DataFlowAnalysisData>(
            "analyze-data-flow",
            new AnalyzeDataFlowRequest
            {
                Location = dataFlowLocation,
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

        var graphLocation = fixture.GetLocation("var upper = trimmed.ToUpperInvariant()");
        var locationGraphRequest = new GetControlFlowGraphRequest
        {
            Location = graphLocation,
            ExpectedSnapshot = snapshot,
        };

        var locationGraph = await session.ExecuteQueryAsync<GetControlFlowGraphRequest, ControlFlowGraphData>(
            "get-control-flow-graph",
            locationGraphRequest,
            TestContext.Current.CancellationToken);

        diagnostics.Data!.Diagnostics.Items.Should().ContainSingle(static diagnostic => diagnostic.Id == "CS0219");
        asyncDiagnostics.Data!.Findings.Items.Should().Contain(static finding => finding.Diagnostic!.Id == "AsyncFixer01");
        asyncDiagnostics.Data.Findings.Items.Should().Contain(static finding => finding.Diagnostic!.Id == "AsyncFixer03");
        operation.Data!.Root!.Kind.Should().Contain("Invocation");
        flow.Data!.Exits.Should().NotBeEmpty();
        flow.Data.Region!.Span!.Start.Should().Be(controlFlowLocation.Span!.Range.Start);
        flow.Data.Region.Span.Length.Should().Be(controlFlowLocation.Span.Range.Length);
        partialFlow.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        partialFlow.Error!.Code.Should().Be("InvalidRequest");
        dataFlow.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        dataFlow.Data!.Region!.Span!.Start.Should().Be(dataFlowLocation.Span!.Range.Start);
        dataFlow.Data.Region.Span.Length.Should().Be(dataFlowLocation.Span.Range.Length);
        dataFlow.Data.ReadInside.Select(static symbol => symbol.DisplayName).Should().Contain("trimmed");
        exceptionalGraph.Data!.Regions.Select(static region => region.Kind).Should().Contain(static kind => kind.Contains("Try", StringComparison.Ordinal) || kind.Contains("Catch", StringComparison.Ordinal) || kind.Contains("Finally", StringComparison.Ordinal));
        boundedGraph.Data!.Blocks.Should().HaveCount(1);
        boundedGraph.Data.BlocksTruncated.Should().BeTrue();
        locationGraph.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        locationGraph.Data!.Owner!.DisplayName.Should().Contain("Analyse");
        locationGraph.Data.Blocks.Should().NotBeEmpty();
    }
}
