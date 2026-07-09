using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindDuplicateCodeToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        FindDuplicateCodeTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<FindDuplicateCodeRequest, DuplicateCodeData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "find-duplicate-code"
                && metadata.Title == "Find Duplicate Code"
                && metadata.Description == "Returns duplicate executable blocks that normalize to the same statement sequence."),
            It.IsAny<IQueryToolHandler<FindDuplicateCodeRequest, DuplicateCodeData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MinimumStatementsIsLessThanOne_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new FindDuplicateCodeTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        var result = await target.ExecuteAsync(new FindDuplicateCodeRequest
        {
            MinimumStatements = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
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
        var expected = PluginExecutionResult<DuplicateCodeData>.Rejected(new ToolError
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

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
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

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
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

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
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

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Groups.Items.Should().HaveCount(2);
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).Select(item => item.Symbol!.DisplayName).Should().Contain(".ctor");
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).Select(item => item.Symbol!.DisplayName).Should().Contain("set_Property");
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).Select(item => item.Symbol!.DisplayName).Should().Contain("MethodOne");
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).Select(item => item.Symbol!.DisplayName).Should().Contain("MethodTwo");
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).Select(item => item.Symbol!.DisplayName).Should().Contain("Local");
        result.Data.Groups.Items.Select(item => item.StatementCount).Should().Equal(3, 2);
        result.Data.Groups.Items.SelectMany(item => item.Occurrences).All(item => !string.IsNullOrWhiteSpace(item.Context)).Should().BeTrue();
    }
}
