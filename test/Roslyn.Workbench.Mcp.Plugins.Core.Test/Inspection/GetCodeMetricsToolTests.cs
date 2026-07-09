namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetCodeMetricsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        GetCodeMetricsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<GetCodeMetricsRequest, CodeMetricsData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "get-code-metrics"
                && metadata.Title == "Get Code Metrics"
                && metadata.Description == "Returns projected code metrics for a scope or symbol."),
            It.IsAny<IQueryToolHandler<GetCodeMetricsRequest, CodeMetricsData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetCodeMetricsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<CodeMetricsData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CodeMetricsData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CodeMetricsData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetCodeMetricsRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_SelectedMetadataSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyMetrics()
    {
        using var document = RoslynTestFactory.CreateDocument("class Formatter {}");

        var target = new GetCodeMetricsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilation = await document.Solution.Projects.Single().GetCompilationAsync(TestContext.Current.CancellationToken);
        var metadataSymbol = compilation!.GetSpecialType(SpecialType.System_String);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CodeMetricsData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CodeMetricsData>
            {
                Value = metadataSymbol,
            });

        var result = await target.ExecuteAsync(new GetCodeMetricsRequest
        {
            Symbol = new SymbolSelector(),
            IncludeChildren = false,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Metrics.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_TypeSymbolAndIncludeChildrenIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnTypeAndMemberMetrics()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            interface IFormatter
            {
            }

            class Formatter : IFormatter
            {
                private string _value = string.Empty;

                string Value
                {
                    get
                    {
                        return _value;
                    }
                }

                event System.EventHandler Changed
                {
                    add
                    {
                    }
                    remove
                    {
                    }
                }

                string Format(string value)
                {
                    if (value is null)
                    {
                        return string.Empty;
                    }

                    return value ?? string.Empty;
                }
            }
            """);

        var target = new GetCodeMetricsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CodeMetricsData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CodeMetricsData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => new SymbolReference
            {
                DisplayName = item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                Kind = item.Kind.ToString(),
                DocumentationCommentId = item.GetDocumentationCommentId(),
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetCodeMetricsRequest
        {
            Symbol = new SymbolSelector(),
            IncludeChildren = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Metrics.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Formatter");
        result.Data.Metrics.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Formatter.Format(string)");
        result.Data.Metrics.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Formatter.Value");
        result.Data.Metrics.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Formatter.Changed");
        result.Data.Metrics.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Formatter._value");
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetCodeMetricsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<CodeMetricsData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<CodeMetricsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, CodeMetricsData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetCodeMetricsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_DocumentWithoutSyntaxOrSemanticModel_WHEN_CallingExecuteAsync_THEN_ShouldSkipDocument()
    {
        using var document = RoslynTestFactory.CreateUnsupportedDocument();

        var target = new GetCodeMetricsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<CodeMetricsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, CodeMetricsData>
            {
                Value = [document.Document],
            });

        var result = await target.ExecuteAsync(new GetCodeMetricsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Metrics.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_DocumentsContainDuplicateAndUnsupportedMetricDeclarations_WHEN_CallingExecuteAsync_THEN_ShouldReturnDistinctBoundedMetrics()
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
                        Name = "B.cs",
                        Source = """
                            public partial class Formatter
                            {
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "A.cs",
                        Source = """
                            using System;

                            public partial class Formatter
                            {
                                public delegate void Notify();

                                public event EventHandler Changed
                                {
                                    add
                                    {
                                    }
                                    remove
                                    {
                                    }
                                }

                                public int Value
                                {
                                    get
                                    {
                                        return _field;
                                    }
                                }

                                public int _field = 1;

                                public string Format(string value)
                                {
                                    int local = 0;

                                    string Nested(string text)
                                    {
                                        return text;
                                    }

                                    if (value is null || local == 0)
                                    {
                                        return Nested(string.Empty);
                                    }

                                    return value ?? string.Empty;
                                }
                            }
                            """,
                    },
                ],
            },
        ]);
        using var unsupportedDocument = RoslynTestFactory.CreateUnsupportedDocument();

        var target = new GetCodeMetricsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<CodeMetricsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, CodeMetricsData>
            {
                Value = [solution.GetDocument("B.cs"), solution.GetDocument("A.cs"), unsupportedDocument.Document],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => new SymbolReference
            {
                DisplayName = item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                Kind = item.Kind.ToString(),
                DocumentationCommentId = item.GetDocumentationCommentId(),
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, Path.GetFileName(item.SourceTree!.FilePath!)));

        var result = await target.ExecuteAsync(new GetCodeMetricsRequest
        {
            MetricsLimit = new CollectionLimit
            {
                MaxResults = 6,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Metrics.Items.Should().HaveCount(6);
        result.Data.Metrics.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Formatter");
        result.Data.Metrics.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Formatter.Changed");
        result.Data.Metrics.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Formatter._field");
        result.Data.Metrics.Items.Select(item => item.Symbol!.DisplayName).Should().NotContain("local");
    }
}
