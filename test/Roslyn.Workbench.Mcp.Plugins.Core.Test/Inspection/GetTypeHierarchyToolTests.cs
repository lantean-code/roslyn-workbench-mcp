namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetTypeHierarchyToolTests
{
    [Fact]
    public async Task GIVEN_MaxDepthIsLessThanOne_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new GetTypeHierarchyTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        var result = await target.ExecuteAsync(new GetTypeHierarchyRequest
        {
            Symbol = new SymbolSelector(),
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
        var target = new GetTypeHierarchyTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<TypeHierarchyData>(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<TypeHierarchyData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Rejected<ISymbol, TypeHierarchyData>(expected));

        var result = await target.ExecuteAsync(new GetTypeHierarchyRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolvedSymbolIsNotNamedType_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }
            }
            """);

        var target = new GetTypeHierarchyTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Format",
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<TypeHierarchyData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, TypeHierarchyData>(symbol));

        var result = await target.ExecuteAsync(new GetTypeHierarchyRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        result.Error.Message.Should().Be("Get type hierarchy requires a named type symbol.");
    }

    [Fact]
    public async Task GIVEN_NamedTypeAndIncludeDerivedIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldReturnBaseTypesAndInterfacesWithoutDerivedTypes()
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
                        Name = "Hierarchy.cs",
                        Source = """
                            namespace Sample;

                            public interface IZetaFormatter
                            {
                            }

                            public interface IAlphaFormatter
                            {
                            }

                            public class FormatterBase
                            {
                            }

                            public class MidFormatter : FormatterBase, IZetaFormatter, IAlphaFormatter
                            {
                            }

                            public sealed class FinalFormatter : MidFormatter
                            {
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new GetTypeHierarchyTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("Hierarchy.cs"),
            "FinalFormatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<TypeHierarchyData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, TypeHierarchyData>(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetTypeHierarchyRequest
        {
            Symbol = new SymbolSelector(),
            IncludeDerived = false,
            BaseTypesLimit = 2,
            InterfacesLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Type!.DisplayName.Should().Be("FinalFormatter");
        result.Data.BaseTypes.Items.Select(item => item.DisplayName).Should().Equal("MidFormatter", "FormatterBase");
        result.Data.BaseTypes.HasMore.Should().BeTrue();
        result.Data.Interfaces.Items.Select(item => item.DisplayName).Should().Equal("IAlphaFormatter");
        result.Data.Interfaces.HasMore.Should().BeTrue();
        result.Data.DerivedTypes.Should().BeNull();

        var boundedResult = await target.ExecuteAsync(new GetTypeHierarchyRequest
        {
            Symbol = new SymbolSelector(),
            IncludeDerived = false,
            MaxDepth = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        boundedResult.Data!.BaseTypes.Items.Select(item => item.DisplayName).Should().Equal("MidFormatter");
    }

    [Fact]
    public async Task GIVEN_ClassTypeAndIncludeDerivedIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedDerivedClassesWithDepths()
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
                        Name = "Hierarchy.cs",
                        Source = """
                            namespace Sample;

                            public class FormatterBase
                            {
                            }

                            public class ZetaFormatter : FormatterBase
                            {
                            }

                            public class AlphaFormatter : FormatterBase
                            {
                            }

                            public sealed class LeafFormatter : AlphaFormatter
                            {
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new GetTypeHierarchyTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("Hierarchy.cs"),
            "FormatterBase",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<TypeHierarchyData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, TypeHierarchyData>(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetTypeHierarchyRequest
        {
            Symbol = new SymbolSelector(),
            IncludeDerived = true,
            DerivedTypesLimit = 2,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.DerivedTypes!.Items.Should().HaveCount(2);
        result.Data.DerivedTypes.Items[0].Type!.DisplayName.Should().Be("AlphaFormatter");
        result.Data.DerivedTypes.Items[0].Depth.Should().Be(1);
        result.Data.DerivedTypes.Items[1].Type!.DisplayName.Should().Be("LeafFormatter");
        result.Data.DerivedTypes.Items[1].Depth.Should().Be(2);
        result.Data.DerivedTypes.HasMore.Should().BeTrue();

        var boundedResult = await target.ExecuteAsync(new GetTypeHierarchyRequest
        {
            Symbol = new SymbolSelector(),
            IncludeDerived = true,
            MaxDepth = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        boundedResult.Data!.DerivedTypes!.Items.Select(item => item.Type!.DisplayName).Should().Equal("AlphaFormatter", "ZetaFormatter");
        boundedResult.Data.DerivedTypes.Items.Select(item => item.Depth).Should().Equal(1, 1);
    }

    [Fact]
    public async Task GIVEN_InterfaceTypeAndIncludeDerivedIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedImplementationsWithBaseDepths()
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
                        Name = "Hierarchy.cs",
                        Source = """
                            namespace Sample;

                            public interface IFormatter
                            {
                            }

                            public class FormatterBase : IFormatter
                            {
                            }

                            public sealed class AdvancedFormatter : FormatterBase
                            {
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new GetTypeHierarchyTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("Hierarchy.cs"),
            "IFormatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<TypeHierarchyData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, TypeHierarchyData>(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetTypeHierarchyRequest
        {
            Symbol = new SymbolSelector(),
            IncludeDerived = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.DerivedTypes!.Items.Should().HaveCount(2);
        result.Data.DerivedTypes.Items[0].Type!.DisplayName.Should().Be("AdvancedFormatter");
        result.Data.DerivedTypes.Items[0].Depth.Should().Be(2);
        result.Data.DerivedTypes.Items[1].Type!.DisplayName.Should().Be("FormatterBase");
        result.Data.DerivedTypes.Items[1].Depth.Should().Be(1);
        result.Data.DerivedTypes.HasMore.Should().BeFalse();
    }
}
