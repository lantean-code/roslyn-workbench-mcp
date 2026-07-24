namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GoToDefinitionToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GoToDefinitionTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<DefinitionData>.Rejected(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<DefinitionData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, DefinitionData>.Rejected(expected));

        var result = await target.ExecuteAsync(new GoToDefinitionRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_SourceSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedSourceDefinitionLocationsWithoutNullEntries()
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
                        Name = "C.cs",
                        Source = """
                            namespace Sample;

                            public partial class Formatter
                            {
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "A.cs",
                        Source = """
                            namespace Sample;

                            public partial class Formatter
                            {
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "B.cs",
                        Source = """
                            namespace Sample;

                            public partial class Formatter
                            {
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new GoToDefinitionTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("A.cs"),
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<DefinitionData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, DefinitionData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item =>
            {
                var path = Path.GetFileName(item.SourceTree!.FilePath!);
                if (path == "B.cs")
                {
                    return null;
                }

                return SelectorTestFactory.CreateResolvedLocation(
                    path!,
                    item.SourceSpan.Start,
                    item.SourceSpan.Length);
            });

        var result = await target.ExecuteAsync(new GoToDefinitionRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Definitions.Should().HaveCount(2);
        result.Data.Definitions.Select(item => item.Location!.Document!.Path).Should().Equal("A.cs", "C.cs");
        result.Data.Symbol!.DisplayName.Should().Be("Formatter");
    }

    [Fact]
    public async Task GIVEN_MetadataSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnMetadataDefinition()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public static class Formatter
            {
                public static string Format(string value)
                {
                    return value.ToUpperInvariant();
                }
            }
            """);

        var target = new GoToDefinitionTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredInvocationTargetSymbolAsync(
            document.Document,
            static item => item.ToString().Contains("ToUpperInvariant()", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<DefinitionData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, DefinitionData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GoToDefinitionRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Definitions.Should().ContainSingle();
        result.Data.Definitions[0].IsMetadata.Should().BeTrue();
        result.Data.Definitions[0].MetadataName.Should().Contain("string.ToUpperInvariant");
        result.Data.Symbol!.DisplayName.Should().Be("ToUpperInvariant");
    }
}
