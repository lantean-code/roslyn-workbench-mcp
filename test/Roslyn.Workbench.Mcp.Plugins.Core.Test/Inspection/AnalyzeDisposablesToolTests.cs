namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class AnalyzeDisposablesToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new AnalyzeDisposablesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<DisposableAnalysisData>(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Rejected<IReadOnlyList<Document>, DisposableAnalysisData>(expected));

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
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>([document.Document]));

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
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>([document.Document]));

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
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>([document.Document]));

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
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>([document.Document]));

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
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>([document.Document]));

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
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>([document.Document]));

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
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>(documents));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, item.SourceTree?.FilePath is null ? "Code.cs" : Path.GetFileName(item.SourceTree.FilePath)));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest
        {
            FindingsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().ContainSingle();
        result.Data.Findings.HasMore.Should().BeTrue();
        result.Data.Findings.Items[0].Kind.Should().Be("UndisposedLocal");
        result.Data.Findings.Items[0].Symbol!.DisplayName.Should().Be("disposable");
        result.Data.Findings.Items[0].Location!.Document!.Path.Should().Be("A.cs");
        result.Data.Findings.Items[0].Type!.DisplayName.Should().Contain("Disposable");
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Once);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Once);
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
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>([document.Document]));

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

    [Fact]
    public async Task GIVEN_DisposableLocalIsDisposedInFinally_WHEN_CallingExecuteAsync_THEN_ShouldReturnNoFindings()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                void Run()
                {
                    var disposable = new Disposable();
                    try
                    {
                        disposable.ToString();
                    }
                    finally
                    {
                        disposable.Dispose();
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
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>([document.Document]));

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_TopLevelLocalFunctionHasMoreDisposableFindingsThanLimit_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedTruncatedFindings()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            void Run()
            {
                var first = new Disposable();
                var second = new Disposable();
                var third = new Disposable();
            }

            Run();

            sealed class Disposable : IDisposable
            {
                public void Dispose()
                {
                }
            }
            """);

        var target = new AnalyzeDisposablesTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>([document.Document]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest
        {
            FindingsLimit = 2,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Select(item => item.Symbol!.DisplayName).Should().Equal("first", "second");
        result.Data.Findings.HasMore.Should().BeTrue();
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Exactly(2));
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GIVEN_TopLevelDisposableLocalIsNotDisposed_WHEN_CallingExecuteAsync_THEN_ShouldReturnUndisposedLocalFinding()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            var disposable = new Disposable();

            sealed class Disposable : IDisposable
            {
                public void Dispose()
                {
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
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>([document.Document]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().ContainSingle(item => item.Symbol!.DisplayName == "disposable");
    }

    [Fact]
    public async Task GIVEN_InterfaceTypedLocalIsDisposedOnlyConditionally_WHEN_CallingExecuteAsync_THEN_ShouldReturnUndisposedFinding()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                void Run(bool shouldDispose)
                {
                    IDisposable disposable = new Disposable();
                    if (shouldDispose)
                    {
                        disposable.Dispose();
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
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DisposableAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, DisposableAnalysisData>([document.Document]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeDisposablesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().ContainSingle(item => item.Symbol!.DisplayName == "disposable");
    }
}
