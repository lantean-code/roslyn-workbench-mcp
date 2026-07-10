using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class AnalyzeDisposablesToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        AnalyzeDisposablesTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<AnalyzeDisposablesRequest, DisposableAnalysisData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "analyze-disposables"
                && metadata.Title == "Analyze Disposables"
                && metadata.Description == "Returns advisory findings for undisposed local disposable values."),
            It.IsAny<IQueryToolHandler<AnalyzeDisposablesRequest, DisposableAnalysisData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new AnalyzeDisposablesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<DisposableAnalysisData>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DisposableAnalysisData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DocumentWithoutSyntaxOrSemanticModel_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyFindings()
    {
        using var document = RoslynTestFactory.CreateUnsupportedDocument();

        var target = new AnalyzeDisposablesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DisposableAnalysisData>
            {
                Value = [document.Document],
            });

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_LocalDeclarationUsesUsingKeyword_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyFindings()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                void Run()
                {
                    using var disposable = new Disposable();
                }

                private sealed class Disposable : IDisposable
                {
                    public void Dispose()
                    {
                    }
                }
            }
            """);

        var target = new AnalyzeDisposablesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DisposableAnalysisData>
            {
                Value = [document.Document],
            });

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_LocalDeclarationIsWithinUsingStatement_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyFindings()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                void Run()
                {
                    using (var disposable = new Disposable())
                    {
                    }
                }

                private sealed class Disposable : IDisposable
                {
                    public void Dispose()
                    {
                    }
                }
            }
            """);

        var target = new AnalyzeDisposablesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DisposableAnalysisData>
            {
                Value = [document.Document],
            });

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_LocalTypeDoesNotImplementDisposable_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyFindings()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    var value = string.Empty;
                }
            }
            """);

        var target = new AnalyzeDisposablesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DisposableAnalysisData>
            {
                Value = [document.Document],
            });

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_LocalDisposableIsDisposedByDisposeCall_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyFindings()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                void Run()
                {
                    var disposable = new Disposable();
                    disposable.Dispose();
                }

                private sealed class Disposable : IDisposable
                {
                    public void Dispose()
                    {
                    }
                }
            }
            """);

        var target = new AnalyzeDisposablesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DisposableAnalysisData>
            {
                Value = [document.Document],
            });

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_LocalAsyncDisposableIsDisposedByDisposeAsyncCall_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyFindings()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;
            using System.Threading.Tasks;

            class Formatter
            {
                async Task RunAsync()
                {
                    var disposable = new AsyncDisposable();
                    await disposable.DisposeAsync();
                }

                private sealed class AsyncDisposable : IAsyncDisposable
                {
                    public ValueTask DisposeAsync()
                    {
                        return ValueTask.CompletedTask;
                    }
                }
            }
            """);

        var target = new AnalyzeDisposablesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DisposableAnalysisData>
            {
                Value = [document.Document],
            });

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_UndisposedDisposableLocalsAcrossDocuments_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedBoundedFindings()
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
                            using System;
                            using System.Threading.Tasks;

                            class Container
                            {
                                AsyncDisposable Value
                                {
                                    get
                                    {
                                        var disposable = new AsyncDisposable();
                                        return disposable;
                                    }
                                }

                                private sealed class AsyncDisposable : IAsyncDisposable
                                {
                                    public ValueTask DisposeAsync()
                                    {
                                        return ValueTask.CompletedTask;
                                    }
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "A.cs",
                        Source = """
                            using System;

                            class Formatter
                            {
                                void Run()
                                {
                                    var disposable = new Disposable();
                                    var other = new Disposable();
                                    other.Dispose();
                                    Dispose(disposable);
                                    disposable.ToString();
                                }

                                void Dispose(Disposable value)
                                {
                                }

                                private sealed class Disposable : IDisposable
                                {
                                    public void Dispose()
                                    {
                                    }
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new AnalyzeDisposablesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var documents = solution.Solution.Projects.Single().Documents.OrderByDescending(item => item.Name, StringComparer.Ordinal).ToArray();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DisposableAnalysisData>
            {
                Value = documents,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, item.SourceTree?.FilePath is null ? "Code.cs" : Path.GetFileName(item.SourceTree.FilePath)));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest
        {
            FindingsLimit = new CollectionLimit
            {
                MaxResults = 1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().ContainSingle();
        result.Data.Findings.Items[0].Kind.Should().Be("UndisposedLocal");
        result.Data.Findings.Items[0].Symbol!.DisplayName.Should().Be("disposable");
        result.Data.Findings.Items[0].Location!.Document!.Path.Should().Be("A.cs");
        result.Data.Findings.Items[0].Type!.DisplayName.Should().Contain("Disposable");
    }

    [Fact]
    public async Task GIVEN_LocalDisposableIsDeclaredInLocalFunction_WHEN_CallingExecuteAsync_THEN_ShouldReturnUndisposedLocalFinding()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                void Run()
                {
                    void Local()
                    {
                        var disposable = new Disposable();
                    }

                    Local();
                }

                private sealed class Disposable : IDisposable
                {
                    public void Dispose()
                    {
                    }
                }
            }
            """);

        var target = new AnalyzeDisposablesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DisposableAnalysisData>
            {
                Value = [document.Document],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().ContainSingle();
        result.Data.Findings.Items[0].Symbol!.DisplayName.Should().Be("disposable");
    }
}
