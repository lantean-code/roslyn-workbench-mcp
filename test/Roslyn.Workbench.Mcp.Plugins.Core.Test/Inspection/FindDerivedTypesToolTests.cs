using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindDerivedTypesToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        FindDerivedTypesTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<FindDerivedTypesRequest, DerivedTypesData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "find-derived-types"
                && metadata.Title == "Find Derived Types"
                && metadata.Description == "Finds derived types for a resolved base type."),
            It.IsAny<IQueryToolHandler<FindDerivedTypesRequest, DerivedTypesData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindDerivedTypesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<DerivedTypesData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<DerivedTypesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, DerivedTypesData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolvedSymbolIsNotNamedType_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                }
            }
            """);

        var target = new FindDerivedTypesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await GetMethodSymbolAsync(document.Document, "Run");

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<DerivedTypesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, DerivedTypesData>
            {
                Value = symbol,
            });

        var result = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "InvalidRequest",
            Message = "Find derived types requires a named type symbol.",
        });
    }

    [Fact]
    public async Task GIVEN_ResolveProjectsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class BaseType
            {
            }
            """);

        var target = new FindDerivedTypesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await GetNamedTypeSymbolAsync(document.Document, "BaseType");
        var expected = PluginExecutionResult<DerivedTypesData>.Rejected(new ToolError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<DerivedTypesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, DerivedTypesData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DerivedTypesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, DerivedTypesData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolvedBaseType_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedDerivedTypesWithDepths()
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
                            class BaseType
                            {
                            }

                            class ZDerived : BaseType
                            {
                            }

                            class AlphaDerived : BaseType
                            {
                            }

                            class NestedDerived : AlphaDerived
                            {
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new FindDerivedTypesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var baseType = await GetNamedTypeSymbolAsync(solution.GetDocument("Code.cs"), "BaseType");
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<DerivedTypesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, DerivedTypesData>
            {
                Value = baseType,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DerivedTypesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, DerivedTypesData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.BaseType!.DisplayName.Should().Be("BaseType");
        result.Data.DerivedTypes.Items.Select(item => item.Type!.DisplayName).Should().Equal("AlphaDerived", "NestedDerived", "ZDerived");
        result.Data.DerivedTypes.Items.Select(item => item.Depth).Should().Equal(1, 2, 1);
    }

    [Fact]
    public async Task GIVEN_ResolvedInterface_WHEN_CallingExecuteAsync_THEN_ShouldReturnImplementingTypes()
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

        var target = new FindDerivedTypesTool();
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
            .Setup(item => item.ResolveSymbolAsync<DerivedTypesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, DerivedTypesData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DerivedTypesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, DerivedTypesData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.DerivedTypes.Items.Select(item => item.Type!.DisplayName).Should().Equal("AFormatter", "ZFormatter");
    }

    private static async Task<IMethodSymbol> GetMethodSymbolAsync(Document document, string methodName)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(TestContext.Current.CancellationToken);
        var method = syntaxRoot!.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(item => item.Identifier.ValueText == methodName);

        return (IMethodSymbol)(semanticModel!.GetDeclaredSymbol(method, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException($"The method '{methodName}' could not be resolved."));
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
