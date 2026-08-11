namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class AnalyzeAsyncToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var asyncAnalyzerDiagnosticService = new Mock<IAsyncAnalyzerDiagnosticService>();
        var target = new AnalyzeAsyncTool(asyncAnalyzerDiagnosticService.Object);
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<AsyncAnalysisData>(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Rejected<IReadOnlyList<Document>, AsyncAnalysisData>(expected));

        var result = await target.ExecuteAsync(
            new AnalyzeAsyncRequest(),
            queryContextMocks.QueryContext.Object,
            TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        queryContextMocks.ToolExecutionServices.VerifyGet(
            item => item.CompilerDiagnosticService,
            Times.Never);

        asyncAnalyzerDiagnosticService.Verify(
            item => item.GetAsyncAnalyzerDiagnosticsAsync(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_CompilerDiagnosticsAreEmpty_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyFindings()
    {
        using var document = RoslynTestFactory.CreateDocument("class Sample { }");
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var asyncAnalyzerDiagnosticService = new Mock<IAsyncAnalyzerDiagnosticService>();
        var target = new AnalyzeAsyncTool(asyncAnalyzerDiagnosticService.Object);
        IReadOnlyList<Document> documents = [document.Document];

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, AsyncAnalysisData>(documents));

        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                documents,
                TestContext.Current.CancellationToken))
            .ReturnsAsync([]);

        asyncAnalyzerDiagnosticService
            .Setup(item => item.GetAsyncAnalyzerDiagnosticsAsync(
                documents,
                TestContext.Current.CancellationToken))
            .ReturnsAsync([]);

        var result = await target.ExecuteAsync(
            new AnalyzeAsyncRequest(),
            queryContextMocks.QueryContext.Object,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
        result.Data.Findings.HasMore.Should().BeFalse();
        result.Data.Findings.TotalCount.Should().Be(0);
        queryContextMocks.WorkspaceResolver.Verify(
            item => item.CreateResolvedLocation(It.IsAny<Location>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_MixedCompilerAndAnalyzerDiagnostics_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedBoundedSupportedDiagnostics()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition { Name = "B.cs", Source = "class B { }" },
                    new InMemoryRoslynDocumentDefinition { Name = "A.cs", Source = "class A { }" },
                ],
            },
        ]);

        var firstDocument = solution.GetDocument("A.cs");
        var secondDocument = solution.GetDocument("B.cs");
        var firstSyntaxTree = await firstDocument.GetSyntaxTreeAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The first test document did not provide a syntax tree.");

        var secondSyntaxTree = await secondDocument.GetSyntaxTreeAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The second test document did not provide a syntax tree.");

        var analyzerDiagnostics = Enumerable
            .Range(1, 6)
            .Select(index => RoslynTestFactory.CreateDiagnostic($"AsyncFixer0{index}", firstSyntaxTree, index, 1))
            .ToArray();

        var ignoredCompilerDiagnostic = RoslynTestFactory.CreateDiagnostic("CS1998", firstSyntaxTree, 0, 1);
        var compilerDiagnostic = RoslynTestFactory.CreateDiagnostic("CS4014", secondSyntaxTree, 2, 1);
        IReadOnlyList<Document> documents = [secondDocument, firstDocument];
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var asyncAnalyzerDiagnosticService = new Mock<IAsyncAnalyzerDiagnosticService>();
        var target = new AnalyzeAsyncTool(asyncAnalyzerDiagnosticService.Object);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, AsyncAnalysisData>(documents));

        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                documents,
                TestContext.Current.CancellationToken))
            .ReturnsAsync([ignoredCompilerDiagnostic, compilerDiagnostic]);

        asyncAnalyzerDiagnosticService
            .Setup(item => item.GetAsyncAnalyzerDiagnosticsAsync(
                documents,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(analyzerDiagnostics);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(analyzerDiagnostics[0].Location))
            .Returns(SelectorTestFactory.CreateResolvedLocation(analyzerDiagnostics[0].Location, "A.cs"));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(compilerDiagnostic.Location))
            .Returns(SelectorTestFactory.CreateResolvedLocation(compilerDiagnostic.Location, "B.cs"));

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest
        {
            FindingsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().ContainSingle();
        var findingDiagnostic = result.Data.Findings.Items[0].Diagnostic.Should().BeAssignableTo<DiagnosticInfo>().Which;
        findingDiagnostic.Id.Should().Be("AsyncFixer01");
        findingDiagnostic.Location!.Document!.Path.Should().Be("A.cs");
        result.Data.Findings.HasMore.Should().BeTrue();
        result.Data.Findings.TotalCount.Should().Be(7);
        queryContextMocks.WorkspaceResolver.Verify(
            item => item.CreateResolvedLocation(compilerDiagnostic.Location),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_FindingsLimitIsZeroAndAsyncDiagnosticExists_WHEN_CallingExecuteAsync_THEN_ShouldReturnTruncatedFindingsWithoutProjection()
    {
        using var document = RoslynTestFactory.CreateDocument("class Sample { }");
        var syntaxTree = await document.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The test document did not provide a syntax tree.");

        var diagnostic = RoslynTestFactory.CreateDiagnostic("CS4014", syntaxTree, 0, 1);
        IReadOnlyList<Document> documents = [document.Document];
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var asyncAnalyzerDiagnosticService = new Mock<IAsyncAnalyzerDiagnosticService>();
        var target = new AnalyzeAsyncTool(asyncAnalyzerDiagnosticService.Object);

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, AsyncAnalysisData>(documents));

        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                documents,
                TestContext.Current.CancellationToken))
            .ReturnsAsync([diagnostic]);

        asyncAnalyzerDiagnosticService
            .Setup(item => item.GetAsyncAnalyzerDiagnosticsAsync(
                documents,
                TestContext.Current.CancellationToken))
            .ReturnsAsync([]);

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest
        {
            FindingsLimit = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
        result.Data.Findings.HasMore.Should().BeTrue();
        result.Data.Findings.TotalCount.Should().Be(1);
        queryContextMocks.WorkspaceResolver.Verify(
            item => item.CreateResolvedLocation(It.IsAny<Location>()),
            Times.Never);
    }
}
