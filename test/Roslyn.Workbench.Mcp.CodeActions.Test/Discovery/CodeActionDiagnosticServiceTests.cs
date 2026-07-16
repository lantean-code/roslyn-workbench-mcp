using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Discovery;

public sealed class CodeActionDiagnosticServiceTests
{
    private readonly Mock<ICodeActionAnalyzerActivator> _analyzerActivator;
    private readonly CodeActionDiagnosticService _target;

    public CodeActionDiagnosticServiceTests()
    {
        _analyzerActivator = new Mock<ICodeActionAnalyzerActivator>();
        _target = new CodeActionDiagnosticService(_analyzerActivator.Object);
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
    public async Task GIVEN_ExistingCompilerDiagnostic_WHEN_GettingScopedDiagnostics_THEN_ShouldNotActivateAdditionalAnalyzer()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { MissingType Value; }");

        var result = await _target.GetScopedCodeFixDiagnosticsAsync(
            roslyn.Document,
            ["CS0246"],
            "AnalyzerTypeName",
            "SYNTHETIC001",
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle(item => item.Id == "CS0246");
        _analyzerActivator.Verify(item => item.Activate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ExistingCompilerDiagnosticAtLocation_WHEN_GettingLocationDiagnostics_THEN_ShouldNotActivateAdditionalAnalyzer()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { MissingType Value; }");
        var diagnostics = await _target.GetDocumentDiagnosticsAsync(
            roslyn.Document,
            ["CS0246"],
            TestContext.Current.CancellationToken);

        var result = await _target.GetLocationScopedCodeFixDiagnosticsAsync(
            roslyn.Document,
            diagnostics[0].Location.SourceSpan,
            ["CS0246"],
            "AnalyzerTypeName",
            "SYNTHETIC001",
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle(item => item.Id == "CS0246");
        _analyzerActivator.Verify(item => item.Activate(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_AdditionalAnalyzerDiagnostics_WHEN_GettingScopedDiagnostics_THEN_ShouldApplyIdAndLocationFilters()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { int First; int Second; }");
        var analyzer = CreateSourceAnalyzer(
        [
            ("ANALYZER001", new TextSpan(0, 1)),
            ("ANALYZER002", new TextSpan(20, 1)),
        ]);
        _analyzerActivator
            .Setup(item => item.Activate("AnalyzerTypeName"))
            .Returns(new CodeActionAnalyzerActivationResult
            {
                Status = CodeActionAnalyzerActivationStatus.Available,
                Analyzer = analyzer.Object,
            });

        var locationDiagnostics = await _target.GetLocationScopedCodeFixDiagnosticsAsync(
            roslyn.Document,
            new TextSpan(0, 2),
            ["ANALYZER001", "ANALYZER002"],
            "AnalyzerTypeName",
            "SYNTHETIC001",
            TestContext.Current.CancellationToken);
        var documentDiagnostics = await _target.GetScopedCodeFixDiagnosticsAsync(
            roslyn.Document,
            ["ANALYZER002"],
            "AnalyzerTypeName",
            "SYNTHETIC001",
            TestContext.Current.CancellationToken);

        locationDiagnostics.Should().ContainSingle(item => item.Id == "ANALYZER001");
        documentDiagnostics.Should().ContainSingle(item => item.Id == "ANALYZER002");
        _analyzerActivator.Verify(item => item.Activate("AnalyzerTypeName"), Times.Exactly(2));
    }

    [Fact]
    public async Task GIVEN_AdditionalAnalyzerReturnsOtherLocations_WHEN_GettingScopedDiagnostics_THEN_ShouldReturnOnlyCurrentDocumentSourceDiagnostics()
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
        _analyzerActivator
            .Setup(item => item.Activate("AnalyzerTypeName"))
            .Returns(new CodeActionAnalyzerActivationResult
            {
                Status = CodeActionAnalyzerActivationStatus.Available,
                Analyzer = analyzer.Object,
            });
        var document = roslyn.GetDocument("First.cs");
        var syntaxTree = await document.GetSyntaxTreeAsync(TestContext.Current.CancellationToken);

        var result = await _target.GetScopedCodeFixDiagnosticsAsync(
            document,
            [],
            "AnalyzerTypeName",
            syntheticDiagnosticId: null,
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle(item => item.Id == "SOURCE001");
        result[0].Location.SourceTree.Should().Be(syntaxTree);
    }

    [Fact]
    public async Task GIVEN_NoDiagnostics_WHEN_GettingScopedDiagnosticsWithSyntheticId_THEN_ShouldCreateFullDocumentDiagnostic()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        var sourceText = await roslyn.Document.GetTextAsync(TestContext.Current.CancellationToken);

        var result = await _target.GetScopedCodeFixDiagnosticsAsync(
            roslyn.Document,
            [],
            analyzerTypeName: null,
            "SYNTHETIC001",
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].Id.Should().Be("SYNTHETIC001");
        result[0].Location.SourceSpan.Should().Be(new TextSpan(0, sourceText.Length));
    }

    [Fact]
    public async Task GIVEN_NoDiagnostics_WHEN_GettingLocationDiagnosticsWithSyntheticId_THEN_ShouldCreateSelectedSpanDiagnostic()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        var span = new TextSpan(2, 4);

        var result = await _target.GetLocationScopedCodeFixDiagnosticsAsync(
            roslyn.Document,
            span,
            [],
            analyzerTypeName: null,
            "SYNTHETIC001",
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].Id.Should().Be("SYNTHETIC001");
        result[0].Location.SourceSpan.Should().Be(span);
    }

    [Fact]
    public async Task GIVEN_AdditionalAnalyzerIsUnavailableAndSyntheticIdIsBlank_WHEN_GettingScopedDiagnostics_THEN_ShouldReturnEmpty()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class Sample { }");
        _analyzerActivator
            .Setup(item => item.Activate("AnalyzerTypeName"))
            .Returns(new CodeActionAnalyzerActivationResult
            {
                Status = CodeActionAnalyzerActivationStatus.TypeNotFound,
            });

        var result = await _target.GetScopedCodeFixDiagnosticsAsync(
            roslyn.Document,
            [],
            "AnalyzerTypeName",
            " ",
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _analyzerActivator.Verify(item => item.Activate("AnalyzerTypeName"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_UnsupportedDocument_WHEN_GettingSyntheticDiagnostics_THEN_ShouldReturnEmpty()
    {
        using var roslyn = RoslynTestFactory.CreateUnsupportedDocument();

        var result = await _target.GetScopedCodeFixDiagnosticsAsync(
            roslyn.Document,
            [],
            analyzerTypeName: null,
            "SYNTHETIC001",
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_UnsupportedDocument_WHEN_GettingLocationSyntheticDiagnostics_THEN_ShouldReturnEmpty()
    {
        using var roslyn = RoslynTestFactory.CreateUnsupportedDocument();

        var result = await _target.GetLocationScopedCodeFixDiagnosticsAsync(
            roslyn.Document,
            new TextSpan(0, 1),
            [],
            analyzerTypeName: null,
            "SYNTHETIC001",
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_UnsupportedDocumentAndAvailableAnalyzer_WHEN_GettingScopedDiagnostics_THEN_ShouldReturnEmpty()
    {
        using var roslyn = RoslynTestFactory.CreateUnsupportedDocument();
        var analyzer = new Mock<DiagnosticAnalyzer>();
        _analyzerActivator
            .Setup(item => item.Activate("AnalyzerTypeName"))
            .Returns(new CodeActionAnalyzerActivationResult
            {
                Status = CodeActionAnalyzerActivationStatus.Available,
                Analyzer = analyzer.Object,
            });

        var result = await _target.GetScopedCodeFixDiagnosticsAsync(
            roslyn.Document,
            [],
            "AnalyzerTypeName",
            syntheticDiagnosticId: null,
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
        _analyzerActivator.Verify(item => item.Activate("AnalyzerTypeName"), Times.Once);
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

    private static Mock<AnalyzerReference> CreateAnalyzerReference(DiagnosticAnalyzer analyzer)
    {
        var analyzerReference = new Mock<AnalyzerReference>();
        analyzerReference
            .Setup(item => item.GetAnalyzers(LanguageNames.CSharp))
            .Returns([analyzer]);
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
}
