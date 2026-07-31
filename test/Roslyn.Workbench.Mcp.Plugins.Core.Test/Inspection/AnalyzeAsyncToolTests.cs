namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class AnalyzeAsyncToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new AnalyzeAsyncTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<AsyncAnalysisData>(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Rejected<IReadOnlyList<Document>, AsyncAnalysisData>(expected));

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DocumentWithoutSyntaxOrSemanticModel_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyFindings()
    {
        using var document = RoslynTestFactory.CreateUnsupportedDocument();

        var target = new AnalyzeAsyncTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, AsyncAnalysisData>([document.Document]));

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_MethodIsNotAsync_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyFindings()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System.Threading.Tasks;

            class Formatter
            {
                public Task FormatAsync()
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var target = new AnalyzeAsyncTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, AsyncAnalysisData>([document.Document]));

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_AsyncMethodHasNoExecutableNode_WHEN_CallingExecuteAsync_THEN_ShouldReturnAsyncWithoutAwaitFindingOnly()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System.Threading.Tasks;

            class Formatter
            {
                public async Task FormatAsync();
            }
            """);

        var target = new AnalyzeAsyncTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, AsyncAnalysisData>([document.Document]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().ContainSingle();
        result.Data.Findings.Items[0].Kind.Should().Be("AsyncWithoutAwait");
        result.Data.Findings.Items[0].Symbol!.DisplayName.Should().Be("FormatAsync");
    }

    [Fact]
    public async Task GIVEN_AsyncMethodHasOnlyAwaitedOrNonTaskInvocations_WHEN_CallingExecuteAsync_THEN_ShouldNotReturnInvocationFindings()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System.Threading.Tasks;

            class Formatter
            {
                public async Task FormatAsync()
                {
                    ReturnString();
                    await ReturnTask();
                }

                private Task ReturnTask()
                {
                    return Task.CompletedTask;
                }

                private string ReturnString()
                {
                    return string.Empty;
                }
            }
            """);

        var target = new AnalyzeAsyncTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, AsyncAnalysisData>([document.Document]));

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_FindingsLimitIsZeroAndFindingExists_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyTruncatedFindingsWithoutProjection()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System.Threading.Tasks;

            class Formatter
            {
                public async Task FormatAsync()
                {
                }
            }
            """);

        var target = new AnalyzeAsyncTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, AsyncAnalysisData>([document.Document]));

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest
        {
            FindingsLimit = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().BeEmpty();
        result.Data.Findings.HasMore.Should().BeTrue();
        result.Data.Findings.TotalCount.Should().BeNull();
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Never);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_AsyncMethodHasUnawaitedTaskLikeInvocationsAcrossDocuments_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedBoundedFindings()
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
                            using System.Threading.Tasks;

                            class BFormatter
                            {
                                public async Task NoAwaitAsync()
                                {
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "A.cs",
                        Source = """
                            using System.Threading.Tasks;

                            class AFormatter
                            {
                                public async Task CallerAsync()
                                {
                                    ReturnTask();
                                    ReturnTaskOfInt();
                                    ReturnValueTask();
                                    ReturnValueTaskOfInt();
                                    ReturnTask();
                                }

                                private Task ReturnTask()
                                {
                                    return Task.CompletedTask;
                                }

                                private Task<int> ReturnTaskOfInt()
                                {
                                    return Task.FromResult(1);
                                }

                                private ValueTask ReturnValueTask()
                                {
                                    return ValueTask.CompletedTask;
                                }

                                private ValueTask<int> ReturnValueTaskOfInt()
                                {
                                    return ValueTask.FromResult(1);
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new AnalyzeAsyncTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var documents = solution.Solution.Projects.Single().Documents.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, AsyncAnalysisData>(documents));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, item.SourceTree?.FilePath is null ? "Code.cs" : Path.GetFileName(item.SourceTree.FilePath)));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest
        {
            FindingsLimit = 5,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().HaveCount(5);
        result.Data.Findings.HasMore.Should().BeTrue();
        result.Data.Findings.Items.Select(item => item.Kind).Should().Equal(
        [
            "AsyncWithoutAwait",
            "UnawaitedTask",
            "UnawaitedTask",
            "UnawaitedTask",
            "UnawaitedTask",
        ]);

        result.Data.Findings.Items[0].Symbol!.DisplayName.Should().Be("CallerAsync");
        result.Data.Findings.Items.Count(item => item.Kind == "UnawaitedTask").Should().Be(4);
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Exactly(5));
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Exactly(5));
    }

    [Fact]
    public async Task GIVEN_AwaitExistsOnlyInNestedFunctionAndStoredTaskIsAwaited_WHEN_CallingExecuteAsync_THEN_ShouldRespectExecutableBoundaries()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;
            using System.Threading.Tasks;

            class Formatter
            {
                public async Task NestedOnlyAsync()
                {
                    Func<Task> nested = async () => await SaveAsync();
                }

                public async Task StoredAsync()
                {
                    var pending = SaveAsync();
                    await pending;
                }

                private Task SaveAsync()
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var target = new AnalyzeAsyncTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, AsyncAnalysisData>([document.Document]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().ContainSingle();
        result.Data.Findings.Items[0].Kind.Should().Be("AsyncWithoutAwait");
        result.Data.Findings.Items[0].Symbol!.DisplayName.Should().Be("NestedOnlyAsync");
    }

    [Fact]
    public async Task GIVEN_TaskInvocationIsAssignedToDiscard_WHEN_CallingExecuteAsync_THEN_ShouldReturnUnawaitedTaskFinding()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System.Threading.Tasks;

            class Formatter
            {
                public async Task Run()
                {
                    _ = SaveAsync();
                    await Task.CompletedTask;
                }

                private Task SaveAsync()
                {
                    return Task.CompletedTask;
                }
            }
            """);

        var target = new AnalyzeAsyncTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<AsyncAnalysisData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, AsyncAnalysisData>([document.Document]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new AnalyzeAsyncRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Findings.Items.Should().ContainSingle();
        result.Data.Findings.Items[0].Kind.Should().Be("UnawaitedTask");
        result.Data.Findings.Items[0].Symbol!.DisplayName.Should().Be("SaveAsync");
    }
}
