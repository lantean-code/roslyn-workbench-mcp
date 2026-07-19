namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindDuplicateCodeToolTests
{
    [Fact]
    public async Task GIVEN_MinimumStatementsIsLessThanOne_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new FindDuplicateCodeTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        var result = await target.ExecuteAsync(new FindDuplicateCodeRequest
        {
            MinimumStatements = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "MinimumStatements must be at least 1.",
        });
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindDuplicateCodeTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<DuplicateCodeData>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DuplicateCodeData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DuplicateCodeData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindDuplicateCodeRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_DocumentsContainUnsupportedLanguageDocument_WHEN_CallingExecuteAsync_THEN_ShouldSkipDocumentAndReturnDuplicatesFromSupportedDocuments()
    {
        using var unsupportedDocument = RoslynTestFactory.CreateUnsupportedDocument();
        using var supportedDocument = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int First(int value)
                {
                    var next = value + 1;
                    return next;
                }

                int Second(int value)
                {
                    var next = value + 1;
                    return next;
                }
            }
            """);

        var target = new FindDuplicateCodeTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DuplicateCodeData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DuplicateCodeData>
            {
                Value = [unsupportedDocument.Document, supportedDocument.Document],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, Path.GetFileName(item.SourceTree!.FilePath!)));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindDuplicateCodeRequest
        {
            MinimumStatements = 2,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Groups.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GIVEN_ExecutableBlocksDoNotMeetMinimumStatements_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyGroups()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int First(int value)
                {
                    return value + 1;
                }

                int Second(int value)
                {
                    return value + 1;
                }
            }
            """);

        var target = new FindDuplicateCodeTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DuplicateCodeData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DuplicateCodeData>
            {
                Value = [document.Document],
            });

        var result = await target.ExecuteAsync(new FindDuplicateCodeRequest
        {
            MinimumStatements = 2,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Groups.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_WorkspaceResolverDoesNotCreateResolvedLocation_WHEN_CallingExecuteAsync_THEN_ShouldSkipOccurrence()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int First(int value)
                {
                    var next = value + 1;
                    return next;
                }

                int Second(int value)
                {
                    var next = value + 1;
                    return next;
                }
            }
            """);

        var target = new FindDuplicateCodeTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DuplicateCodeData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DuplicateCodeData>
            {
                Value = [document.Document],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns((ResolvedLocation?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindDuplicateCodeRequest
        {
            MinimumStatements = 2,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Groups.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_DuplicateExecutableBlocksExistAcrossSupportedBlockTypes_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedDuplicateGroups()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                public Formatter()
                {
                    var next = 1 + 1;
                    var final = next + 1;
                }

                int Property
                {
                    set
                    {
                        var next = 1 + 1;
                        var final = next + 1;
                    }
                }

                void MethodOne()
                {
                    var next = 1 + 1;
                    var final = next + 1;

                    void Local()
                    {
                        var next = 1 + 1;
                        var final = next + 1;
                    }
                }

                void MethodTwo()
                {
                    var next = 1 + 1;
                    var final = next + 1;

                    void Local()
                    {
                        var next = 1 + 1;
                        var final = next + 1;
                    }
                }
            }
            """);

        var target = new FindDuplicateCodeTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DuplicateCodeData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DuplicateCodeData>
            {
                Value = [document.Document],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, Path.GetFileName(item.SourceTree!.FilePath!)));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindDuplicateCodeRequest
        {
            MinimumStatements = 2,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Groups.Items.Should().HaveCount(2);
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).Select(item => item.Symbol!.DisplayName).Should().Contain(".ctor");
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).Select(item => item.Symbol!.DisplayName).Should().Contain("set_Property");
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).Select(item => item.Symbol!.DisplayName).Should().Contain("MethodOne");
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).Select(item => item.Symbol!.DisplayName).Should().Contain("MethodTwo");
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).Select(item => item.Symbol!.DisplayName).Should().Contain("Local");
        result.Data.Groups.Items.Select(item => item.StatementCount).Should().Equal(3, 2);
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).All(item => !string.IsNullOrWhiteSpace(item.Context)).Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_DuplicateGroupsExceedLimit_WHEN_CallingExecuteAsync_THEN_ShouldProjectOnlySelectedGroup()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void First()
                {
                    var value = 1;
                    value++;
                    value++;
                }

                void Second()
                {
                    var value = 1;
                    value++;
                    value++;
                }

                void Third()
                {
                    var value = 2;
                    value--;
                }

                void Fourth()
                {
                    var value = 2;
                    value--;
                }
            }
            """);

        var target = new FindDuplicateCodeTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DuplicateCodeData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, DuplicateCodeData>
            {
                Value = [document.Document],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindDuplicateCodeRequest
        {
            MinimumStatements = 2,
            GroupsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Groups.Items.Should().ContainSingle();
        result.Data.Groups.Items[0].StatementCount.Should().Be(3);
        result.Data.Groups.HasMore.Should().BeTrue();
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Exactly(2));
    }
}
