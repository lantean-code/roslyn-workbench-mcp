namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindImplementationsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindImplementationsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<ImplementationSearchData>(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ImplementationSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Rejected<ISymbol, ImplementationSearchData>(expected));

        var result = await target.ExecuteAsync(new FindImplementationsRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveProjectsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            interface IMessageFormatter
            {
            }
            """);

        var target = new FindImplementationsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "IMessageFormatter",
            TestContext.Current.CancellationToken);

        var expected = PluginExecutionResult.Rejected<ImplementationSearchData>(new PluginExecutionError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ImplementationSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ImplementationSearchData>(symbol));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<ImplementationSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Rejected<IReadOnlyList<Project>, ImplementationSearchData>(expected));

        var result = await target.ExecuteAsync(new FindImplementationsRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_InterfaceHasImplementations_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedImplementations()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Code.cs",
                        Source = """
                            interface IMessageFormatter
                            {
                            }

                            class ZFormatter : IMessageFormatter
                            {
                            }

                            class AFormatter : IMessageFormatter
                            {
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new FindImplementationsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("Code.cs"),
            "IMessageFormatter",
            TestContext.Current.CancellationToken);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ImplementationSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ImplementationSearchData>(symbol));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<ImplementationSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, ImplementationSearchData>([project]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindImplementationsRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("IMessageFormatter");
        result.Data.Implementations.Items.Select(item => item.DisplayName).Should().Equal("AFormatter", "ZFormatter");

        var boundedResult = await target.ExecuteAsync(new FindImplementationsRequest
        {
            Symbol = new SymbolSelector(),
            ImplementationsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        boundedResult.Data!.Implementations.Items.Select(item => item.DisplayName).Should().Equal("AFormatter");
        boundedResult.Data.Implementations.HasMore.Should().BeTrue();
    }
}
