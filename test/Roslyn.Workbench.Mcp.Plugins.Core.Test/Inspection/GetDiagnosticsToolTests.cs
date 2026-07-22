using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetDiagnosticsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetDiagnosticsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<DiagnosticsData>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DiagnosticsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, DiagnosticsData>.Rejected(expected));

        var result = await target.ExecuteAsync(new GetDiagnosticsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ProjectCompilationIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyDiagnostics()
    {
        using var document = RoslynTestFactory.CreateUnsupportedDocument();

        var target = new GetDiagnosticsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DiagnosticsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, DiagnosticsData>.Resolved([document.Document]));

        var result = await target.ExecuteAsync(new GetDiagnosticsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Diagnostics.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_DocumentScopeContainsDiagnosticWithoutSourceDocument_WHEN_CallingExecuteAsync_THEN_ShouldExcludeDiagnostic()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                }
            }
            """);

        var descriptor = new DiagnosticDescriptor(
            "RWB002",
            "RWB002",
            "AnalyzerMessage",
            "Category",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        var analyzer = CreateFixedDiagnosticAnalyzer(Diagnostic.Create(descriptor, Location.None));
        var analyzerReference = new Mock<AnalyzerReference>();
        analyzerReference
            .Setup(item => item.GetAnalyzers(LanguageNames.CSharp))
            .Returns([analyzer.Object]);
        analyzerReference
            .Setup(item => item.GetGenerators(LanguageNames.CSharp))
            .Returns([]);

        var project = document.Solution.Projects.Single();
        document.Workspace.TryApplyChanges(
            document.Solution.AddAnalyzerReference(project.Id, analyzerReference.Object));

        var target = new GetDiagnosticsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Workspace.CurrentSolution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DiagnosticsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, DiagnosticsData>.Resolved([document.Workspace.CurrentSolution.Projects.Single().Documents.Single()]));

        var result = await target.ExecuteAsync(new GetDiagnosticsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Diagnostics.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_ProjectScopeContainsMixedDiagnosticsAndEmptyFilters_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedProjectedDiagnostics()
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

        var descriptor = new DiagnosticDescriptor(
            "RWB002",
            "RWB002",
            "AnalyzerMessage",
            "Category",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        var sharedDiagnostic = Diagnostic.Create(descriptor, Location.None);
        var analyzerOne = CreateFixedDiagnosticAnalyzer(sharedDiagnostic);
        var analyzerTwo = CreateFixedDiagnosticAnalyzer(sharedDiagnostic);
        var analyzerReferenceOne = new Mock<AnalyzerReference>();
        var analyzerReferenceTwo = new Mock<AnalyzerReference>();
        analyzerReferenceOne
            .Setup(item => item.GetAnalyzers(LanguageNames.CSharp))
            .Returns([analyzerOne.Object]);
        analyzerReferenceOne
            .Setup(item => item.GetGenerators(LanguageNames.CSharp))
            .Returns([]);
        analyzerReferenceTwo
            .Setup(item => item.GetAnalyzers(LanguageNames.CSharp))
            .Returns([analyzerTwo.Object]);
        analyzerReferenceTwo
            .Setup(item => item.GetGenerators(LanguageNames.CSharp))
            .Returns([]);

        var project = document.Solution.Projects.Single();
        document.Workspace.TryApplyChanges(
            document.Solution
                .AddAnalyzerReference(project.Id, analyzerReferenceOne.Object)
                .AddAnalyzerReference(project.Id, analyzerReferenceTwo.Object));

        var target = new GetDiagnosticsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var currentDocument = document.Workspace.CurrentSolution.Projects.Single().Documents.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Workspace.CurrentSolution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DiagnosticsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, DiagnosticsData>.Resolved([currentDocument]));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, currentDocument.Name));

        var result = await target.ExecuteAsync(new GetDiagnosticsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Project,
            },
            Ids = [],
            Severities = [],
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Diagnostics.Items.Should().HaveCount(2);
        result.Data.Diagnostics.Items[0].Id.Should().Be("RWB002");
        result.Data.Diagnostics.Items[0].Location.Should().BeNull();
        result.Data.Diagnostics.Items[1].Id.Should().Be("CS0219");
        result.Data.Diagnostics.Items[1].Location!.Document!.Path.Should().Be("Code.cs");
    }

    [Fact]
    public async Task GIVEN_DiagnosticIdFilterDoesNotMatch_WHEN_CallingExecuteAsync_THEN_ShouldReturnNoDiagnostics()
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

        var target = new GetDiagnosticsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DiagnosticsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, DiagnosticsData>.Resolved([document.Document]));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetDiagnosticsRequest
        {
            Ids = ["CS9999"],
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Diagnostics.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_MoreSourceDiagnosticsThanRequested_WHEN_CallingExecuteAsync_THEN_ShouldProjectOnlyReturnedDiagnostics()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    int first;
                    int second;
                }
            }
            """);

        var target = new GetDiagnosticsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DiagnosticsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, DiagnosticsData>.Resolved([document.Document]));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetDiagnosticsRequest
        {
            DiagnosticsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Diagnostics.Items.Should().ContainSingle();
        result.Data.Diagnostics.HasMore.Should().BeTrue();
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateResolvedLocation(It.IsAny<Location>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DiagnosticSeverityFilterDoesNotMatch_WHEN_CallingExecuteAsync_THEN_ShouldReturnNoDiagnostics()
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

        var target = new GetDiagnosticsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DiagnosticsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, DiagnosticsData>.Resolved([document.Document]));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetDiagnosticsRequest
        {
            Severities = ["Error"],
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Diagnostics.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_DocumentScopeAndDuplicateAnalyzerDiagnostics_WHEN_CallingExecuteAsync_THEN_ShouldReturnDistinctMatchingSelectedDocumentDiagnostics()
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
                            class First
                            {
                                string Format(string? value)
                                {
                                    return value.ToString();
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Second.cs",
                        Source = """
                            class Second
                            {
                                string Format(string? value)
                                {
                                    return value.ToString();
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var descriptor = new DiagnosticDescriptor(
            "RWB001",
            "RWB001",
            "AnalyzerMessage",
            "Category",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        var analyzerOne = CreateDuplicateWarningAnalyzer(descriptor);
        var analyzerTwo = CreateDuplicateWarningAnalyzer(descriptor);
        var analyzerReferenceOne = new Mock<AnalyzerReference>();
        var analyzerReferenceTwo = new Mock<AnalyzerReference>();
        analyzerReferenceOne
            .Setup(item => item.GetAnalyzers(LanguageNames.CSharp))
            .Returns([analyzerOne.Object]);
        analyzerReferenceOne
            .Setup(item => item.GetGenerators(LanguageNames.CSharp))
            .Returns([]);
        analyzerReferenceTwo
            .Setup(item => item.GetAnalyzers(LanguageNames.CSharp))
            .Returns([analyzerTwo.Object]);
        analyzerReferenceTwo
            .Setup(item => item.GetGenerators(LanguageNames.CSharp))
            .Returns([]);

        var project = solution.Solution.Projects.Single();
        solution.Workspace.TryApplyChanges(
            solution.Solution
                .AddAnalyzerReference(project.Id, analyzerReferenceOne.Object)
                .AddAnalyzerReference(project.Id, analyzerReferenceTwo.Object));

        var selectedDocument = solution.Workspace.CurrentSolution.Projects.Single().Documents.Single(item => item.Name == "First.cs");
        var target = new GetDiagnosticsTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Workspace.CurrentSolution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<DiagnosticsData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, DiagnosticsData>.Resolved([selectedDocument]));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, Path.GetFileName(item.SourceTree!.FilePath!)));

        var result = await target.ExecuteAsync(new GetDiagnosticsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
            },
            Ids = ["RWB001"],
            Severities = ["Warning"],
            DiagnosticsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Diagnostics.Items.Should().ContainSingle();
        result.Data.Diagnostics.Items[0].Id.Should().Be("RWB001");
        result.Data.Diagnostics.Items[0].Location!.Document!.Path.Should().Be("First.cs");
        result.Data.Diagnostics.HasMore.Should().BeFalse();
    }

    private static Mock<DiagnosticAnalyzer> CreateFixedDiagnosticAnalyzer(Diagnostic diagnostic)
    {
        var analyzer = new Mock<DiagnosticAnalyzer>();

        analyzer
            .SetupGet(item => item.SupportedDiagnostics)
            .Returns([diagnostic.Descriptor]);
        analyzer
            .Setup(item => item.Initialize(It.IsAny<AnalysisContext>()))
            .Callback<AnalysisContext>(analysisContext =>
            {
                analysisContext.EnableConcurrentExecution();
                analysisContext.RegisterCompilationAction(compilationContext => compilationContext.ReportDiagnostic(diagnostic));
            });

        return analyzer;
    }

    private static Mock<DiagnosticAnalyzer> CreateDuplicateWarningAnalyzer(DiagnosticDescriptor descriptor)
    {
        var analyzer = new Mock<DiagnosticAnalyzer>();

        analyzer
            .SetupGet(item => item.SupportedDiagnostics)
            .Returns([descriptor]);
        analyzer
            .Setup(item => item.Initialize(It.IsAny<AnalysisContext>()))
            .Callback<AnalysisContext>(analysisContext =>
            {
                analysisContext.EnableConcurrentExecution();
                analysisContext.RegisterSyntaxTreeAction(syntaxContext =>
                {
                    var root = syntaxContext.Tree.GetRoot(syntaxContext.CancellationToken);
                    var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                    if (classDeclaration is not null)
                    {
                        syntaxContext.ReportDiagnostic(Diagnostic.Create(descriptor, classDeclaration.Identifier.GetLocation()));
                    }
                });
            });

        return analyzer;
    }
}
