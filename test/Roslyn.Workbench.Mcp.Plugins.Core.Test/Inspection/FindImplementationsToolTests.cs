using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindImplementationsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        FindImplementationsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<FindImplementationsRequest, ImplementationSearchData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "find-implementations"
                && metadata.Title == "Find Implementations"
                && metadata.Description == "Finds implementations of an interface or abstract member."),
            It.IsAny<IQueryToolHandler<FindImplementationsRequest, ImplementationSearchData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindImplementationsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<ImplementationSearchData>.Rejected(new ToolError
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
            .ReturnsAsync(new ToolResolutionResult<ISymbol, ImplementationSearchData>
            {
                Rejection = expected,
            });

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
        var symbol = await GetNamedTypeSymbolAsync(document.Document, "IMessageFormatter");
        var expected = PluginExecutionResult<ImplementationSearchData>.Rejected(new ToolError
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
            .ReturnsAsync(new ToolResolutionResult<ISymbol, ImplementationSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<ImplementationSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, ImplementationSearchData>
            {
                Rejection = expected,
            });

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
        var symbol = await GetNamedTypeSymbolAsync(solution.GetDocument("Code.cs"), "IMessageFormatter");
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
            .ReturnsAsync(new ToolResolutionResult<ISymbol, ImplementationSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<ImplementationSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, ImplementationSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindImplementationsRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("IMessageFormatter");
        result.Data.Implementations.Items.Select(item => item.DisplayName).Should().Equal("AFormatter", "ZFormatter");
    }

    private static async Task<INamedTypeSymbol> GetNamedTypeSymbolAsync(Document document, string typeName)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(TestContext.Current.CancellationToken);
        var type = syntaxRoot!.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Single(item => item.Identifier.ValueText == typeName);

        return (INamedTypeSymbol)(semanticModel!.GetDeclaredSymbol(type, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException($"The type '{typeName}' could not be resolved."));
    }
}
