namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetTestImpactToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        GetTestImpactTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<GetTestImpactRequest, TestImpactData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "get-test-impact"
                && metadata.Title == "Get Test Impact"
                && metadata.Description == "Returns likely impacted tests for a resolved symbol."),
            It.IsAny<IQueryToolHandler<GetTestImpactRequest, TestImpactData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetTestImpactTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<TestImpactData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<TestImpactData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, TestImpactData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetTestImpactRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public sealed class Formatter
            {
            }
            """);

        var target = new GetTestImpactTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Formatter",
            TestContext.Current.CancellationToken);
        var expected = PluginExecutionResult<TestImpactData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<TestImpactData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, TestImpactData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<TestImpactData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, TestImpactData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetTestImpactRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolvedSymbolAndTestScope_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedImpactedTests()
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
                        Name = "App.cs",
                        Source = """
                            namespace Sample;

                            public sealed class Formatter
                            {
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "FormatterTests.cs",
                        Source = """
                            namespace Sample.Tests;

                            public static class FormatterTests
                            {
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new GetTestImpactTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("App.cs"),
            "Formatter",
            TestContext.Current.CancellationToken);
        var documents = solution.Solution.Projects.Single().Documents.Where(item => item.Name == "FormatterTests.cs").ToArray();
        var impactedTests = new[]
        {
            new TestImpactInfo
            {
                Test = SelectorTestFactory.CreateSymbolReference("AlphaTest", SymbolKind.Method, "AlphaTest"),
                Reasons = ["ReasonA"],
            },
            new TestImpactInfo
            {
                Test = SelectorTestFactory.CreateSymbolReference("BetaTest", SymbolKind.Method, "BetaTest"),
                Reasons = ["ReasonB"],
            },
        };

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.DependencyAnalysisService)
            .Returns(dependencyAnalysisService.Object);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(1);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<TestImpactData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, TestImpactData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<TestImpactData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, TestImpactData>
            {
                Value = documents,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));
        dependencyAnalysisService
            .Setup(item => item.FindTestImpactsAsync(
                symbol,
                documents,
                true,
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(impactedTests);

        var result = await target.ExecuteAsync(new GetTestImpactRequest
        {
            Symbol = new SymbolSelector(),
            TestScope = new ScopeSelector(),
            IncludeReasons = true,
            TestsLimit = new CollectionLimit
            {
                MaxResults = 1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("Formatter");
        result.Data.Tests.Items.Should().ContainSingle();
        result.Data.Tests.Items[0].Test!.DisplayName.Should().Be("AlphaTest");
        result.Data.Tests.HasMore.Should().BeTrue();
        dependencyAnalysisService.Verify(item => item.FindTestImpactsAsync(
            symbol,
            documents,
            true,
            queryContextMocks.QueryContext.Object,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
