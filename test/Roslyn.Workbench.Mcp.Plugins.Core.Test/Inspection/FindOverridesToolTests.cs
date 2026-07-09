using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindOverridesToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        FindOverridesTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<FindOverridesRequest, OverrideSearchData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "find-overrides"
                && metadata.Title == "Find Overrides"
                && metadata.Description == "Finds overrides of a virtual or abstract member."),
            It.IsAny<IQueryToolHandler<FindOverridesRequest, OverrideSearchData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindOverridesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<OverrideSearchData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<OverrideSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, OverrideSearchData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindOverridesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolvedSymbolIsNotOverridableMember_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
            }
            """);

        var target = new FindOverridesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<OverrideSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, OverrideSearchData>
            {
                Value = symbol,
            });

        var result = await target.ExecuteAsync(new FindOverridesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "InvalidRequest",
            Message = "Find overrides requires a virtual, abstract, property, or event member symbol.",
        });
    }

    [Fact]
    public async Task GIVEN_ResolveProjectsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class BaseType
            {
                public virtual void Run()
                {
                }
            }
            """);

        var target = new FindOverridesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Run",
            null,
            TestContext.Current.CancellationToken);
        var expected = PluginExecutionResult<OverrideSearchData>.Rejected(new ToolError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<OverrideSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, OverrideSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<OverrideSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, OverrideSearchData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindOverridesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_VirtualMethodHasOverrides_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedOverrides()
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
                                public virtual void Run()
                                {
                                }
                            }

                            class ZDerived : BaseType
                            {
                                public override void Run()
                                {
                                }
                            }

                            class ADerived : BaseType
                            {
                                public override void Run()
                                {
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new FindOverridesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            solution.GetDocument("Code.cs"),
            "Run",
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
            .Setup(item => item.ResolveSymbolAsync<OverrideSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, OverrideSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<OverrideSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, OverrideSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => new SymbolReference
            {
                DisplayName = item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                Kind = item.Kind.ToString(),
                DocumentationCommentId = item.GetDocumentationCommentId(),
            });

        var result = await target.ExecuteAsync(new FindOverridesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("BaseType.Run()");
        result.Data.Overrides.Items.Select(item => item.DisplayName).Should().Equal("ADerived.Run()", "ZDerived.Run()");
    }
}
