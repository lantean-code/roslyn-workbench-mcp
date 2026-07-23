namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindDerivedTypesToolTests
{
    [Fact]
    public async Task GIVEN_MaxDepthIsLessThanOne_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new FindDerivedTypesTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        var result = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            MaxDepth = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "MaxDepth must be at least 1.",
        });
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindDerivedTypesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<DerivedTypesData>.Rejected(new PluginExecutionError
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
            .ReturnsAsync(ToolResolutionResult<ISymbol, DerivedTypesData>.Rejected(expected));

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
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Run",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<DerivedTypesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, DerivedTypesData>.Resolved(symbol));

        var result = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
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
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "BaseType",
            TestContext.Current.CancellationToken);

        var expected = PluginExecutionResult<DerivedTypesData>.Rejected(new PluginExecutionError
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
            .ReturnsAsync(ToolResolutionResult<ISymbol, DerivedTypesData>.Resolved(symbol));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DerivedTypesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Project>, DerivedTypesData>.Rejected(expected));

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
        var baseType = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("Code.cs"),
            "BaseType",
            TestContext.Current.CancellationToken);

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
            .ReturnsAsync(ToolResolutionResult<ISymbol, DerivedTypesData>.Resolved(baseType));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DerivedTypesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Project>, DerivedTypesData>.Resolved([project]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.BaseType!.DisplayName.Should().Be("BaseType");
        result.Data.DerivedTypes.Items.Select(item => item.Type!.DisplayName).Should().Equal("AlphaDerived", "NestedDerived", "ZDerived");
        result.Data.DerivedTypes.Items.Select(item => item.Depth).Should().Equal(1, 2, 1);

        var boundedResult = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector(),
            MaxDepth = 1,
            DerivedTypesLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        boundedResult.Data!.DerivedTypes.Items.Select(item => item.Type!.DisplayName).Should().Equal("AlphaDerived");
        boundedResult.Data.DerivedTypes.Items.Select(item => item.Depth).Should().Equal(1);
        boundedResult.Data.DerivedTypes.HasMore.Should().BeTrue();
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
            .Setup(item => item.ResolveSymbolAsync<DerivedTypesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, DerivedTypesData>.Resolved(symbol));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<DerivedTypesData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Project>, DerivedTypesData>.Resolved([project]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindDerivedTypesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.DerivedTypes.Items.Select(item => item.Type!.DisplayName).Should().Equal("AFormatter", "ZFormatter");
    }
}
