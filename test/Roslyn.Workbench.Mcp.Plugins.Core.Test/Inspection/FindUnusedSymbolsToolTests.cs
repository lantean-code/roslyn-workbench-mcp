using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindUnusedSymbolsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        FindUnusedSymbolsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<FindUnusedSymbolsRequest, UnusedSymbolsData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "find-unused-symbols"
                && metadata.Title == "Find Unused Symbols"
                && metadata.Description == "Returns candidate unused locals and members from compiler diagnostics."),
            It.IsAny<IQueryToolHandler<FindUnusedSymbolsRequest, UnusedSymbolsData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindUnusedSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<UnusedSymbolsData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<UnusedSymbolsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, UnusedSymbolsData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindUnusedSymbolsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ExcludeGeneratedIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldExcludeGeneratedDocumentsFromCompilerDiagnosticsRequest()
    {
        using var generatedDocument = RoslynTestFactory.CreateDocument("class Generated {}", "Generated.g.cs");
        using var regularDocument = RoslynTestFactory.CreateDocument("class Regular {}", "Regular.cs");

        var target = new FindUnusedSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<UnusedSymbolsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, UnusedSymbolsData>
            {
                Value = [generatedDocument.Document, regularDocument.Document],
            });
        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await target.ExecuteAsync(new FindUnusedSymbolsRequest
        {
            ExcludeGenerated = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        compilerDiagnosticService.Verify(item => item.GetCompilerDiagnosticsAsync(
            It.Is<IReadOnlyList<Document>>(documents => documents.Count == 1 && documents[0].Name == "Regular.cs"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DiagnosticLocationHasNoSourceTree_WHEN_CallingExecuteAsync_THEN_ShouldSkipDiagnostic()
    {
        var target = new FindUnusedSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();

        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<UnusedSymbolsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, UnusedSymbolsData>
            {
                Value = [],
            });
        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([Diagnostic.Create(new DiagnosticDescriptor("CS0219", "CS0219", "Message", "Category", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, isEnabledByDefault: true), Location.None)]);

        var result = await target.ExecuteAsync(new FindUnusedSymbolsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Candidates.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_DiagnosticDocumentIsNotInCurrentSolution_WHEN_CallingExecuteAsync_THEN_ShouldSkipDiagnostic()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    int unused = 0;
                }
            }
            """);
        using var foreignDocument = RoslynTestFactory.CreateDocument("""
            class Other
            {
                void Run()
                {
                    int unused = 0;
                }
            }
            """);

        var target = new FindUnusedSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var foreignSyntaxTree = await foreignDocument.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<UnusedSymbolsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, UnusedSymbolsData>
            {
                Value = [document.Document],
            });
        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([RoslynTestFactory.CreateDiagnostic("CS0219", foreignSyntaxTree!, 50, 6)]);

        var result = await target.ExecuteAsync(new FindUnusedSymbolsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Candidates.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_DiagnosticDoesNotResolveCandidateSymbol_WHEN_CallingExecuteAsync_THEN_ShouldSkipDiagnostic()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    int value = 0;
                }
            }
            """);

        var target = new FindUnusedSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var syntaxTree = await document.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var start = (await document.Document.GetTextAsync(TestContext.Current.CancellationToken)).ToString().IndexOf("value", StringComparison.Ordinal);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<UnusedSymbolsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, UnusedSymbolsData>
            {
                Value = [document.Document],
            });
        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([RoslynTestFactory.CreateDiagnostic("CS0219", syntaxTree!, start, "value".Length)]);

        var result = await target.ExecuteAsync(new FindUnusedSymbolsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Candidates.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_InternalFieldAndIncludeInternalIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldExcludeCandidate()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                internal int unusedField;
            }
            """);

        var target = new FindUnusedSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var syntaxTree = await document.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<UnusedSymbolsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, UnusedSymbolsData>
            {
                Value = [document.Document],
            });
        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([RoslynTestFactory.CreateDiagnostic("CS0169", syntaxTree!, 33, 11)]);

        var result = await target.ExecuteAsync(new FindUnusedSymbolsRequest
        {
            IncludeInternal = false,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Candidates.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_PrivateFieldDiagnostic_WHEN_CallingExecuteAsync_THEN_ShouldIncludeCandidate()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                private int unusedField;
            }
            """);

        var target = new FindUnusedSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var syntaxTree = await document.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var start = (await document.Document.GetTextAsync(TestContext.Current.CancellationToken)).ToString().IndexOf("unusedField", StringComparison.Ordinal);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<UnusedSymbolsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, UnusedSymbolsData>
            {
                Value = [document.Document],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, Path.GetFileName(item.SourceTree!.FilePath!)));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));
        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([RoslynTestFactory.CreateDiagnostic("CS0169", syntaxTree!, start, "unusedField".Length)]);

        var result = await target.ExecuteAsync(new FindUnusedSymbolsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Candidates.Items.Should().ContainSingle(item => item.Symbol!.DisplayName == "unusedField");
    }

    [Fact]
    public async Task GIVEN_ProtectedFieldAndIncludeInternalIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldExcludeCandidate()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                protected int unusedField;
            }
            """);

        var target = new FindUnusedSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var syntaxTree = await document.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var start = (await document.Document.GetTextAsync(TestContext.Current.CancellationToken)).ToString().IndexOf("unusedField", StringComparison.Ordinal);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<UnusedSymbolsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, UnusedSymbolsData>
            {
                Value = [document.Document],
            });
        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([RoslynTestFactory.CreateDiagnostic("CS0169", syntaxTree!, start, "unusedField".Length)]);

        var result = await target.ExecuteAsync(new FindUnusedSymbolsRequest
        {
            IncludeInternal = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Candidates.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_UnusedInternalFieldAndUnusedCatchVariable_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedCandidates()
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
                        Name = "First.cs",
                        Source = """
                            class Formatter
                            {
                                internal int unusedField;
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Second.cs",
                        Source = """
                            class Catcher
                            {
                                void Run()
                                {
                                    try
                                    {
                                    }
                                    catch (System.Exception ex)
                                    {
                                    }
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new FindUnusedSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var compilerDiagnosticService = new Mock<ICompilerDiagnosticService>();
        var firstTree = await solution.GetDocument("First.cs").GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var secondTree = await solution.GetDocument("Second.cs").GetSyntaxTreeAsync(TestContext.Current.CancellationToken);
        var firstText = await solution.GetDocument("First.cs").GetTextAsync(TestContext.Current.CancellationToken);
        var secondText = await solution.GetDocument("Second.cs").GetTextAsync(TestContext.Current.CancellationToken);
        var fieldStart = firstText.ToString().IndexOf("unusedField", StringComparison.Ordinal);
        var catchStart = secondText.ToString().IndexOf("ex", StringComparison.Ordinal);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.CompilerDiagnosticService)
            .Returns(compilerDiagnosticService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<UnusedSymbolsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, UnusedSymbolsData>
            {
                Value = [solution.GetDocument("First.cs"), solution.GetDocument("Second.cs")],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, Path.GetFileName(item.SourceTree!.FilePath!)));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));
        compilerDiagnosticService
            .Setup(item => item.GetCompilerDiagnosticsAsync(
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                RoslynTestFactory.CreateDiagnostic("CS0169", firstTree!, fieldStart, "unusedField".Length),
                RoslynTestFactory.CreateDiagnostic("CS0168", secondTree!, catchStart, "ex".Length),
            ]);

        var result = await target.ExecuteAsync(new FindUnusedSymbolsRequest
        {
            IncludeInternal = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Candidates.Items.Select(item => item.Symbol!.DisplayName).Should().Equal("unusedField", "ex");
        result.Data.Candidates.Items.SelectMany(item => item.Reasons).Should().Contain("CS0169");
        result.Data.Candidates.Items.SelectMany(item => item.Reasons).Should().Contain("CS0168");
    }
}
