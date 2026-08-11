using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

public sealed class CodeActionDiagnosticServiceTests
{
    private readonly Mock<ICodeActionBuiltInAnalyzerIndex> _builtInAnalyzerIndex;
    private readonly CodeActionDiagnosticService _target;

    public CodeActionDiagnosticServiceTests()
    {
        _builtInAnalyzerIndex = new Mock<ICodeActionBuiltInAnalyzerIndex>();
        _builtInAnalyzerIndex
            .SetupGet(item => item.Warnings)
            .Returns([]);

        _builtInAnalyzerIndex
            .Setup(item => item.GetAnalyzers(It.IsAny<IReadOnlySet<string>>()))
            .Returns([]);

        _target = new CodeActionDiagnosticService(_builtInAnalyzerIndex.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GIVEN_CompilerDiagnostic_WHEN_GettingDocumentDiagnosticsWithoutEffectiveIdFilter_THEN_ShouldReturnDiagnostic(bool useNullFilter)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { MissingType Value; }");
        IReadOnlyList<string>? diagnosticIds = useNullFilter ? null : [];
        var syntaxTree = await roslyn.Document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);

        var result = await _target.GetDocumentDiagnosticsAsync(
            roslyn.Document,
            diagnosticIds,
            TestContext.Current.CancellationToken);

        result.Should().Contain(item => item.Id == "CS0246");
        result.Should().OnlyContain(item => item.Location.SourceTree == syntaxTree);
    }

    [Theory]
    [InlineData("CS0246", true)]
    [InlineData("CS0000", false)]
    public async Task GIVEN_DiagnosticIdFilter_WHEN_GettingDocumentDiagnostics_THEN_ShouldApplyFilter(string diagnosticId, bool expected)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { MissingType Value; }");

        var result = await _target.GetDocumentDiagnosticsAsync(
            roslyn.Document,
            [diagnosticId],
            TestContext.Current.CancellationToken);

        if (expected)
        {
            result.Should().ContainSingle(item => item.Id == diagnosticId);
        }
        else
        {
            result.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task GIVEN_DiagnosticsInMultipleDocuments_WHEN_GettingDocumentDiagnostics_THEN_ShouldReturnOnlyRequestedDocument()
    {
        using var roslyn = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "First.cs",
                        Source = "class First { MissingFirst Value; }",
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Second.cs",
                        Source = "class Second { MissingSecond Value; }",
                    },
                ],
            },
        ]);

        var document = roslyn.GetDocument("First.cs");
        var syntaxTree = await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);

        var result = await _target.GetDocumentDiagnosticsAsync(
            document,
            ["CS0246"],
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result.Should().OnlyContain(item => item.Location.SourceTree == syntaxTree);
    }

    [Fact]
    public async Task GIVEN_DiagnosticsInsideAndOutsideSpan_WHEN_GettingLocationDiagnostics_THEN_ShouldReturnIntersectingDiagnostic()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { MissingType First; MissingType Second; }");
        var diagnostics = await _target.GetDocumentDiagnosticsAsync(
            roslyn.Document,
            ["CS0246"],
            TestContext.Current.CancellationToken);

        var selectedSpan = diagnostics[0].Location.SourceSpan;

        var result = await _target.GetDocumentDiagnosticsAsync(
            roslyn.Document,
            selectedSpan,
            ["CS0246"],
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].Location.SourceSpan.Should().Be(selectedSpan);
    }

    [Fact]
    public async Task GIVEN_ConfiguredProjectAnalyzer_WHEN_GettingDocumentAndProjectDiagnostics_THEN_ShouldSeparateSourceAndProjectDiagnostics()
    {
        using var roslyn = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "First.cs",
                        Source = "class First { }",
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Second.cs",
                        Source = "class Second { }",
                    },
                ],
            },
        ]);

        var analyzer = CreateCompilationAnalyzer("SOURCE001", "PROJECT001");
        var analyzerReference = CreateAnalyzerReference(analyzer.Object);
        var project = roslyn.GetProject("Project");
        var updatedSolution = roslyn.Solution.AddAnalyzerReference(project.Id, analyzerReference.Object);
        roslyn.Workspace.TryApplyChanges(updatedSolution).Should().BeTrue();
        var document = roslyn.Workspace.CurrentSolution.GetDocument(roslyn.GetDocument("First.cs").Id)
            ?? throw new InvalidOperationException("The updated test document could not be resolved.");

        var updatedProject = document.Project;

        var documentDiagnostics = await _target.GetDocumentDiagnosticsAsync(
            document,
            ["SOURCE001"],
            TestContext.Current.CancellationToken);

        var projectDiagnostics = await _target.GetProjectDiagnosticsAsync(
            updatedProject,
            ["PROJECT001"],
            TestContext.Current.CancellationToken);

        var unfilteredProjectDiagnostics = await _target.GetProjectDiagnosticsAsync(
            updatedProject,
            diagnosticIds: null,
            TestContext.Current.CancellationToken);

        var emptyFilterProjectDiagnostics = await _target.GetProjectDiagnosticsAsync(
            updatedProject,
            [],
            TestContext.Current.CancellationToken);

        var excludedProjectDiagnostics = await _target.GetProjectDiagnosticsAsync(
            updatedProject,
            ["PROJECT002"],
            TestContext.Current.CancellationToken);

        documentDiagnostics.Should().ContainSingle(item => item.Id == "SOURCE001");
        projectDiagnostics.Should().ContainSingle(item => item.Id == "PROJECT001");
        unfilteredProjectDiagnostics.Should().Contain(item => item.Id == "PROJECT001");
        emptyFilterProjectDiagnostics.Should().Contain(item => item.Id == "PROJECT001");
        excludedProjectDiagnostics.Should().NotContain(item => item.Id == "PROJECT001");
        analyzerReference.Verify(item => item.GetAnalyzers(LanguageNames.CSharp), Times.Exactly(5));
    }

    [Fact]
    public async Task GIVEN_CompilerAndAnalyzerDiagnostics_WHEN_CollectingProjectDiagnostics_THEN_ShouldReturnPartitionedAggregate()
    {
        using var roslyn = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "First.cs",
                        Source = "class First { MissingType Value; }",
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Second.cs",
                        Source = "class Second { }",
                    },
                ],
            },
        ]);

        var analyzer = CreateCompilationAnalyzer("SOURCE001", "PROJECT001");
        var analyzerReference = CreateAnalyzerReference(analyzer.Object);
        var project = roslyn.GetProject("Project");
        var updatedSolution = roslyn.Solution.AddAnalyzerReference(project.Id, analyzerReference.Object);
        roslyn.Workspace.TryApplyChanges(updatedSolution).Should().BeTrue();
        var updatedProject = roslyn.Workspace.CurrentSolution.GetProject(project.Id)
            ?? throw new InvalidOperationException("The updated test project could not be resolved.");

        var firstDocument = updatedProject.Documents.Single(document => document.Name == "First.cs");
        var firstSyntaxTree = await firstDocument.GetSyntaxTreeAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The test document did not provide a syntax tree.");

        var result = await _target.CollectProjectDiagnosticsAsync(
            updatedProject,
            ["CS0246", "SOURCE001", "PROJECT001"],
            TestContext.Current.CancellationToken);

        result.Diagnostics.Should().HaveCount(4);
        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "CS0246");
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "SOURCE001");
        result.ProjectDiagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "PROJECT001");
        result.GetDocumentDiagnostics(firstSyntaxTree, span: null).Should().HaveCount(2);
        analyzerReference.Verify(item => item.GetAnalyzers(LanguageNames.CSharp), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AnalyzerReportsExternalLocation_WHEN_CollectingProjectDiagnostics_THEN_ShouldTreatItAsProjectDiagnostic()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        var descriptor = CreateDescriptor("EXTERNAL001");
        var externalLocation = Location.Create(
            "/external/Generated.cs",
            new TextSpan(0, 1),
            new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 1)));

        var analyzer = new Mock<DiagnosticAnalyzer>();
        analyzer
            .SetupGet(item => item.SupportedDiagnostics)
            .Returns([descriptor]);

        analyzer
            .Setup(item => item.Initialize(It.IsAny<AnalysisContext>()))
            .Callback<AnalysisContext>(context =>
            {
                context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
                context.RegisterCompilationAction(compilationContext =>
                    compilationContext.ReportDiagnostic(Diagnostic.Create(descriptor, externalLocation)));
            });

        var analyzerReference = CreateAnalyzerReference(analyzer.Object);
        var updatedSolution = roslyn.Solution.AddAnalyzerReference(
            roslyn.Document.Project.Id,
            analyzerReference.Object);

        roslyn.Workspace.TryApplyChanges(updatedSolution).Should().BeTrue();
        var updatedProject = roslyn.Workspace.CurrentSolution.GetProject(roslyn.Document.Project.Id)
            ?? throw new InvalidOperationException("The updated test project could not be resolved.");

        var result = await _target.CollectProjectDiagnosticsAsync(
            updatedProject,
            ["EXTERNAL001"],
            TestContext.Current.CancellationToken);

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "EXTERNAL001");
        result.ProjectDiagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "EXTERNAL001");
    }

    [Fact]
    public async Task GIVEN_ProjectAnalyzers_WHEN_GettingFilteredDiagnostics_THEN_ShouldExecuteOnlySupportingAnalyzers()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        var matchingAnalyzer = CreateSourceAnalyzer(
        [
            ("MATCH001", new TextSpan(0, 1)),
            ("OTHER001", new TextSpan(1, 1)),
        ]);

        var unrelatedAnalyzer = CreateSourceAnalyzer(
        [
            ("UNRELATED001", new TextSpan(0, 1)),
        ]);

        var analyzerReference = CreateAnalyzerReference(matchingAnalyzer.Object, unrelatedAnalyzer.Object);
        var updatedSolution = roslyn.Solution.AddAnalyzerReference(roslyn.Document.Project.Id, analyzerReference.Object);
        roslyn.Workspace.TryApplyChanges(updatedSolution).Should().BeTrue();
        var document = roslyn.Workspace.CurrentSolution.GetDocument(roslyn.Document.Id)
            ?? throw new InvalidOperationException("The updated test document could not be resolved.");

        var result = await _target.GetDocumentDiagnosticsAsync(
            document,
            ["MATCH001"],
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle(item => item.Id == "MATCH001");
        matchingAnalyzer.Verify(item => item.Initialize(It.IsAny<AnalysisContext>()), Times.Once);
        unrelatedAnalyzer.Verify(item => item.Initialize(It.IsAny<AnalysisContext>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_BuiltInAnalyzerSupportsRequestedDiagnostic_WHEN_CollectingDiagnostics_THEN_ShouldJoinBuiltInSource()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        var analyzer = CreateSourceAnalyzer(
        [
            ("IDE9000", new TextSpan(0, 1)),
        ]);

        _builtInAnalyzerIndex
            .Setup(item => item.GetAnalyzers(
                It.Is<IReadOnlySet<string>>(ids => ids.Count == 1 && ids.Contains("IDE9000"))))
            .Returns([analyzer.Object]);

        var result = await _target.CollectDocumentDiagnosticsAsync(
            roslyn.Document,
            span: null,
            ["IDE9000"],
            TestContext.Current.CancellationToken);

        result.Diagnostics.Should().ContainSingle(item => item.Id == "IDE9000");
        result.Warnings.Should().BeEmpty();
        _builtInAnalyzerIndex.Verify(
            item => item.GetAnalyzers(It.IsAny<IReadOnlySet<string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GIVEN_DuplicateDiagnosticsFromAnalyzer_WHEN_CollectingDiagnostics_THEN_ShouldDeduplicateByStableIdentity()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        var descriptor = CreateDescriptor("DUPLICATE001");
        var analyzer = new Mock<DiagnosticAnalyzer>();
        analyzer
            .SetupGet(item => item.SupportedDiagnostics)
            .Returns([descriptor]);

        analyzer
            .Setup(item => item.Initialize(It.IsAny<AnalysisContext>()))
            .Callback<AnalysisContext>(context =>
            {
                context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
                context.RegisterSyntaxTreeAction(syntaxTreeContext =>
                {
                    var diagnostic = Diagnostic.Create(
                        descriptor,
                        syntaxTreeContext.Tree.GetLocation(new TextSpan(0, 1)),
                        properties: ImmutableDictionary<string, string?>.Empty.Add("Key", "Value"));

                    syntaxTreeContext.ReportDiagnostic(diagnostic);
                    syntaxTreeContext.ReportDiagnostic(diagnostic);
                    syntaxTreeContext.ReportDiagnostic(Diagnostic.Create(
                        descriptor,
                        syntaxTreeContext.Tree.GetLocation(new TextSpan(0, 1)),
                        properties: ImmutableDictionary<string, string?>.Empty.Add("Key", "OtherValue")));
                });
            });

        _builtInAnalyzerIndex
            .Setup(item => item.GetAnalyzers(It.IsAny<IReadOnlySet<string>>()))
            .Returns([analyzer.Object]);

        var result = await _target.CollectDocumentDiagnosticsAsync(
            roslyn.Document,
            span: null,
            ["DUPLICATE001"],
            TestContext.Current.CancellationToken);

        result.Diagnostics.Should().HaveCount(2);
        result.Diagnostics.Should().OnlyContain(item => item.Id == "DUPLICATE001");
    }

    [Fact]
    public async Task GIVEN_BuiltInAnalyzerFails_WHEN_CollectingCompilerDiagnostics_THEN_ShouldRetainCompilerDiagnosticAndReportWarning()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { MissingType Value; }");
        var descriptor = CreateDescriptor("IDE9001");
        var analyzer = new Mock<DiagnosticAnalyzer>();
        analyzer
            .SetupGet(item => item.SupportedDiagnostics)
            .Returns([descriptor]);

        analyzer
            .Setup(item => item.Initialize(It.IsAny<AnalysisContext>()))
            .Throws(new InvalidOperationException("Analyzer failure."));

        _builtInAnalyzerIndex
            .Setup(item => item.GetAnalyzers(It.IsAny<IReadOnlySet<string>>()))
            .Returns([analyzer.Object]);

        var result = await _target.CollectDocumentDiagnosticsAsync(
            roslyn.Document,
            span: null,
            ["CS0246", "IDE9001"],
            TestContext.Current.CancellationToken);

        result.Diagnostics.Should().ContainSingle(item => item.Id == "CS0246");
        result.Warnings.Should().ContainSingle(item =>
            item.Contains(
                "failed during diagnostic collection (InvalidOperationException)",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_ProjectAnalyzerReferenceFails_WHEN_CollectingCompilerDiagnostics_THEN_ShouldRetainCompilerDiagnosticAndReportWarning()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { MissingType Value; }");
        var analyzerReference = CreateAnalyzerReference();
        analyzerReference
            .Setup(item => item.GetAnalyzers(LanguageNames.CSharp))
            .Throws(new InvalidOperationException("Analyzer reference failure."));

        var updatedSolution = roslyn.Solution.AddAnalyzerReference(
            roslyn.Document.Project.Id,
            analyzerReference.Object);

        roslyn.Workspace.TryApplyChanges(updatedSolution).Should().BeTrue();
        var document = roslyn.Workspace.CurrentSolution.GetDocument(roslyn.Document.Id)
            ?? throw new InvalidOperationException("The updated test document could not be resolved.");

        var result = await _target.CollectDocumentDiagnosticsAsync(
            document,
            span: null,
            ["CS0246"],
            TestContext.Current.CancellationToken);

        result.Diagnostics.Should().ContainSingle(item => item.Id == "CS0246");
        result.Warnings.Should().ContainSingle(item =>
            item.Contains("failed while loading project analyzers", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_ProjectAnalyzerMetadataFails_WHEN_CollectingCompilerDiagnostics_THEN_ShouldRetainCompilerDiagnosticAndReportWarning()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { MissingType Value; }");
        var analyzer = new Mock<DiagnosticAnalyzer>();
        analyzer
            .SetupGet(item => item.SupportedDiagnostics)
            .Throws(new InvalidOperationException("Analyzer metadata failure."));

        var analyzerReference = CreateAnalyzerReference(analyzer.Object);
        var updatedSolution = roslyn.Solution.AddAnalyzerReference(
            roslyn.Document.Project.Id,
            analyzerReference.Object);

        roslyn.Workspace.TryApplyChanges(updatedSolution).Should().BeTrue();
        var document = roslyn.Workspace.CurrentSolution.GetDocument(roslyn.Document.Id)
            ?? throw new InvalidOperationException("The updated test document could not be resolved.");

        var result = await _target.CollectDocumentDiagnosticsAsync(
            document,
            span: null,
            ["CS0246", "ANALYZER001"],
            TestContext.Current.CancellationToken);

        result.Diagnostics.Should().ContainSingle(item => item.Id == "CS0246");
        result.Warnings.Should().ContainSingle(item =>
            item.Contains("failed while reading supported diagnostics", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("true", "warning", true)]
    [InlineData("false", "warning", false)]
    [InlineData("true", "none", false)]
    public async Task GIVEN_EditorConfigOptions_WHEN_CollectingProjectAnalyzerDiagnostics_THEN_ShouldRespectOptionAndSeverity(
        string enabled,
        string severity,
        bool expected)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        var analyzer = new ConfigurableAnalyzer();
        var analyzerReference = CreateAnalyzerReference(analyzer);
        var project = roslyn.Document.Project;
        var analyzerConfigId = DocumentId.CreateNewId(project.Id, ".editorconfig");
        var editorConfig = $"""
            root = true

            [*.cs]
            workbench_test_enabled = {enabled}
            dotnet_diagnostic.CONFIG001.severity = {severity}
            """;

        var updatedSolution = roslyn.Solution
            .AddAnalyzerReference(project.Id, analyzerReference.Object)
            .AddAnalyzerConfigDocument(
                analyzerConfigId,
                ".editorconfig",
                SourceText.From(editorConfig),
                filePath: "/workspace/Project/.editorconfig");

        roslyn.Workspace.TryApplyChanges(updatedSolution).Should().BeTrue();
        var document = roslyn.Workspace.CurrentSolution.GetDocument(roslyn.Document.Id)
            ?? throw new InvalidOperationException("The updated test document could not be resolved.");

        var result = await _target.GetDocumentDiagnosticsAsync(
            document,
            ["CONFIG001"],
            TestContext.Current.CancellationToken);

        if (expected)
        {
            result.Should().ContainSingle(item => item.Id == "CONFIG001");
        }
        else
        {
            result.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task GIVEN_UnsupportedProject_WHEN_GettingProjectDiagnostics_THEN_ShouldReturnEmpty()
    {
        using var roslyn = RoslynTestFactory.CreateUnsupportedDocument();

        var result = await _target.GetProjectDiagnosticsAsync(
            roslyn.Document.Project,
            diagnosticIds: null,
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_CancelledOperation_WHEN_GettingDocumentDiagnostics_THEN_ShouldPropagateCancellation()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        var cancellationToken = new CancellationToken(canceled: true);

        Func<Task> action = () => _target.GetDocumentDiagnosticsAsync(
            roslyn.Document,
            diagnosticIds: null,
            cancellationToken);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static Mock<AnalyzerReference> CreateAnalyzerReference(params DiagnosticAnalyzer[] analyzers)
    {
        var analyzerReference = new Mock<AnalyzerReference>();
        analyzerReference
            .Setup(item => item.GetAnalyzers(LanguageNames.CSharp))
            .Returns(analyzers.ToImmutableArray());

        analyzerReference
            .Setup(item => item.GetGenerators(LanguageNames.CSharp))
            .Returns([]);

        return analyzerReference;
    }

    private static Mock<DiagnosticAnalyzer> CreateCompilationAnalyzer(string sourceDiagnosticId, string projectDiagnosticId)
    {
        var sourceDescriptor = CreateDescriptor(sourceDiagnosticId);
        var projectDescriptor = CreateDescriptor(projectDiagnosticId);
        var analyzer = new Mock<DiagnosticAnalyzer>();
        analyzer
            .SetupGet(item => item.SupportedDiagnostics)
            .Returns([sourceDescriptor, projectDescriptor]);

        analyzer
            .Setup(item => item.Initialize(It.IsAny<AnalysisContext>()))
            .Callback<AnalysisContext>(context =>
            {
                context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
                context.EnableConcurrentExecution();
                context.RegisterCompilationAction(compilationContext =>
                {
                    foreach (var syntaxTree in compilationContext.Compilation.SyntaxTrees)
                    {
                        compilationContext.ReportDiagnostic(Diagnostic.Create(
                            sourceDescriptor,
                            syntaxTree.GetLocation(new TextSpan(0, 1))));
                    }

                    compilationContext.ReportDiagnostic(Diagnostic.Create(projectDescriptor, Location.None));
                });
            });

        return analyzer;
    }

    private static Mock<DiagnosticAnalyzer> CreateSourceAnalyzer(IReadOnlyList<(string Id, TextSpan Span)> definitions)
    {
        var descriptors = definitions
            .Select(static definition => CreateDescriptor(definition.Id))
            .ToImmutableArray();

        var analyzer = new Mock<DiagnosticAnalyzer>();
        analyzer
            .SetupGet(item => item.SupportedDiagnostics)
            .Returns(descriptors);

        analyzer
            .Setup(item => item.Initialize(It.IsAny<AnalysisContext>()))
            .Callback<AnalysisContext>(context =>
            {
                context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
                context.EnableConcurrentExecution();
                context.RegisterSyntaxTreeAction(syntaxTreeContext =>
                {
                    for (var index = 0; index < definitions.Count; index++)
                    {
                        syntaxTreeContext.ReportDiagnostic(Diagnostic.Create(
                            descriptors[index],
                            syntaxTreeContext.Tree.GetLocation(definitions[index].Span)));
                    }
                });
            });

        return analyzer;
    }

    private static DiagnosticDescriptor CreateDescriptor(string diagnosticId)
    {
        return new DiagnosticDescriptor(
            diagnosticId,
            diagnosticId,
            diagnosticId,
            "Category",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }

#pragma warning disable RS1001 // The configurable analyser is supplied directly as unit-test data rather than exported.

    private sealed class ConfigurableAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor _descriptor = CreateDescriptor("CONFIG001");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [_descriptor];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxTreeAction(static syntaxTreeContext =>
            {
                var options = syntaxTreeContext.Options.AnalyzerConfigOptionsProvider.GetOptions(syntaxTreeContext.Tree);
                if (!options.TryGetValue("workbench_test_enabled", out var enabled)
                    || !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                syntaxTreeContext.ReportDiagnostic(Diagnostic.Create(
                    _descriptor,
                    syntaxTreeContext.Tree.GetLocation(new TextSpan(0, 1))));
            });
        }
    }

#pragma warning restore RS1001
}
