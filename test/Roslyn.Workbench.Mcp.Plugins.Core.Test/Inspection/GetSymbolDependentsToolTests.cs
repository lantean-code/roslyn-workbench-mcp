namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetSymbolDependentsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetSymbolDependentsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<SymbolDependentsData>.Rejected(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependentsData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependentsData>.Rejected(expected));

        var result = await target.ExecuteAsync(new GetSymbolDependentsRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public sealed class Formatter
            {
                public string Format(string value)
                {
                    return value;
                }
            }
            """);

        var target = new GetSymbolDependentsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Format",
            "Formatter",
            TestContext.Current.CancellationToken);

        var expected = PluginExecutionResult<SymbolDependentsData>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependentsData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependentsData>.Resolved(symbol));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<SymbolDependentsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, SymbolDependentsData>.Rejected(expected));

        var result = await target.ExecuteAsync(new GetSymbolDependentsRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_RecursiveSymbolAndExternalDependents_WHEN_CallingExecuteAsync_THEN_ShouldExcludeSelfAndReturnBoundedOrderedDependents()
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
                        Name = "Formatter.cs",
                        Source = """
                            namespace Sample;

                            public sealed class Formatter
                            {
                                public string Format(string value)
                                {
                                    if (value.Length == 0)
                                    {
                                        return Format("fallback");
                                    }

                                    return value;
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Callers.cs",
                        Source = """
                            namespace Sample;

                            public sealed class AlphaCaller
                            {
                                public string AlphaCall(Formatter formatter)
                                {
                                    return formatter.Format("a");
                                }
                            }

                            public sealed class BetaCaller
                            {
                                public string BetaCall(Formatter formatter)
                                {
                                    return formatter.Format("b");
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new GetSymbolDependentsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var formatterDocument = solution.GetDocument("Formatter.cs");
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            formatterDocument,
            "Format",
            "Formatter",
            TestContext.Current.CancellationToken);

        var documents = solution.Solution.Projects.Single().Documents.ToArray();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(1);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependentsData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependentsData>.Resolved(symbol));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<SymbolDependentsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, SymbolDependentsData>.Resolved(documents));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolDependentsRequest
        {
            Symbol = new SymbolSelector(),
            DependentsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("Format");
        result.Data.Dependents.Items.Should().ContainSingle();
        result.Data.Dependents.Items[0].DisplayName.Should().Be("AlphaCall");
        result.Data.Dependents.HasMore.Should().BeTrue();
    }
}
