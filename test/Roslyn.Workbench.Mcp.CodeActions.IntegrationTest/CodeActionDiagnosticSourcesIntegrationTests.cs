using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Options;
using Moq;
using Roslyn.Workbench.Mcp.CodeActions.Composition;
using Roslyn.Workbench.Mcp.CodeActions.Discovery;
using Roslyn.Workbench.Mcp.CodeActions.Policy;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public sealed class CodeActionDiagnosticSourcesIntegrationTests
{
    [Fact]
    public async Task GIVEN_CompilerDiagnostic_WHEN_CollectingWithPinnedComposition_THEN_ShouldReachBuiltInCodeFixProvider()
    {
        var composition = CreateComposition();
        using var workspace = CreateWorkspace(
            composition,
            "class Sample { void M() { int unused = 0; } }",
            editorConfig: null,
            analyzer: null);

        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var diagnosticService = CreateDiagnosticService();
        var diagnostics = await diagnosticService.GetDocumentDiagnosticsAsync(
            document,
            ["CS0219"],
            TestContext.Current.CancellationToken);

        var actions = await RegisterFirstAvailableFixAsync(
            composition,
            document,
            diagnostics,
            "CS0219",
            TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle(item => item.Id == "CS0219");
        actions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GIVEN_ProjectAnalyzerDiagnostic_WHEN_CollectingWithPinnedComposition_THEN_ShouldReachComposedCodeFixProvider()
    {
        var composition = CreateComposition();
        using var workspace = CreateWorkspace(
            composition,
            "class Sample { }",
            editorConfig: null,
            new ProjectDiagnosticAnalyzer());

        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var diagnosticService = CreateDiagnosticService();
        var diagnostics = await diagnosticService.GetDocumentDiagnosticsAsync(
            document,
            ["PROJECT9000"],
            TestContext.Current.CancellationToken);

        var actions = await RegisterFirstAvailableFixAsync(
            composition,
            document,
            diagnostics,
            "PROJECT9000",
            TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle(item => item.Id == "PROJECT9000");
        actions.Should().ContainSingle(item => item.Title == "Apply project analyzer fix");
    }

    [Fact]
    public async Task GIVEN_BuiltInIdeDiagnostic_WHEN_CollectingWithPinnedComposition_THEN_ShouldReachBuiltInCodeFixProvider()
    {
        var composition = CreateComposition();
        var editorConfig = """
            root = true

            [*.cs]
            dotnet_style_qualification_for_field = false:warning
            dotnet_diagnostic.IDE0003.severity = warning
            """;

        using var workspace = CreateWorkspace(
            composition,
            "class Sample { private int _value; int GetValue() { return this._value; } }",
            editorConfig,
            analyzer: null);

        var document = workspace.CurrentSolution.Projects.Single().Documents.Single();
        var diagnosticService = CreateDiagnosticService();
        var diagnostics = await diagnosticService.GetDocumentDiagnosticsAsync(
            document,
            ["IDE0003"],
            TestContext.Current.CancellationToken);

        var actions = await RegisterFirstAvailableFixAsync(
            composition,
            document,
            diagnostics,
            "IDE0003",
            TestContext.Current.CancellationToken);

        diagnostics.Should().ContainSingle(item => item.Id == "IDE0003");
        actions.Should().NotBeEmpty();
    }

    [Fact]
    public void GIVEN_MultipleBuiltInIndexes_WHEN_SelectingAnalyzers_THEN_ShouldReuseProcessCachedInstances()
    {
        var options = Options.Create(new CodeActionCompositionOptions());
        var first = new CodeActionBuiltInAnalyzerIndex(options, new CodeActionAnalyzerActivator());
        var second = new CodeActionBuiltInAnalyzerIndex(options, new CodeActionAnalyzerActivator());
        var diagnosticIds = new HashSet<string>(["IDE0003"], StringComparer.Ordinal);

        var firstAnalyzers = first.GetAnalyzers(diagnosticIds);
        var secondAnalyzers = second.GetAnalyzers(diagnosticIds);

        firstAnalyzers.Should().NotBeEmpty();
        secondAnalyzers.Should().HaveSameCount(firstAnalyzers);
        for (var index = 0; index < firstAnalyzers.Length; index++)
        {
            secondAnalyzers[index].Should().BeSameAs(firstAnalyzers[index]);
        }
    }

    private static ICodeActionComposition CreateComposition()
    {
        var options = new CodeActionCompositionOptions
        {
            AdditionalAssemblies =
            [
                typeof(CodeActionDiagnosticSourcesIntegrationTests).Assembly,
            ],
        };

        return CodeActionCompositionFactory.Create(options);
    }

    private static CodeActionDiagnosticService CreateDiagnosticService()
    {
        var activator = new CodeActionAnalyzerActivator();
        var index = new CodeActionBuiltInAnalyzerIndex(
            Options.Create(new CodeActionCompositionOptions()),
            activator);

        return new CodeActionDiagnosticService(activator, index);
    }

    private static AdhocWorkspace CreateWorkspace(
        ICodeActionComposition composition,
        string source,
        string? editorConfig,
        DiagnosticAnalyzer? analyzer)
    {
        var hostServices = composition.WorkspaceHostServices
            ?? throw new InvalidOperationException("Code Action composition did not provide Workspace host services.");

        var workspace = new AdhocWorkspace(hostServices);
        var projectId = ProjectId.CreateNewId("Project");
        var documentId = DocumentId.CreateNewId(projectId, "Code.cs");
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "Project",
                "Project",
                LanguageNames.CSharp,
                filePath: "/workspace/Project/Project.csproj",
                metadataReferences: CreateMetadataReferences(),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: new CSharpParseOptions(LanguageVersion.Preview)))
            .AddDocument(
                documentId,
                "Code.cs",
                SourceText.From(source),
                filePath: "/workspace/Project/Code.cs");

        if (editorConfig is not null)
        {
            solution = solution.AddAnalyzerConfigDocument(
                DocumentId.CreateNewId(projectId, ".editorconfig"),
                ".editorconfig",
                SourceText.From(editorConfig),
                filePath: "/workspace/Project/.editorconfig");
        }

        if (analyzer is not null)
        {
            var analyzerReference = new Mock<AnalyzerReference>();
            analyzerReference
                .Setup(item => item.GetAnalyzers(LanguageNames.CSharp))
                .Returns([analyzer]);

            analyzerReference
                .Setup(item => item.GetGenerators(LanguageNames.CSharp))
                .Returns([]);

            solution = solution.AddAnalyzerReference(projectId, analyzerReference.Object);
        }

        if (!workspace.TryApplyChanges(solution))
        {
            workspace.Dispose();
            throw new InvalidOperationException("The diagnostic integration Workspace could not be created.");
        }

        return workspace;
    }

    private static async Task<IReadOnlyList<CodeAction>> RegisterFirstAvailableFixAsync(
        ICodeActionComposition composition,
        Document document,
        IReadOnlyList<Diagnostic> diagnostics,
        string diagnosticId,
        CancellationToken cancellationToken)
    {
        var policy = new CodeActionPolicy();
        var providerSelection = new CodeActionProviderSelection(composition, policy);
        foreach (var provider in providerSelection.CodeFixProviders.Values)
        {
            if (!provider.FixableDiagnosticIds.Contains(diagnosticId, StringComparer.Ordinal))
            {
                continue;
            }

            var matchingDiagnostics = diagnostics
                .Where(item => string.Equals(item.Id, diagnosticId, StringComparison.Ordinal))
                .ToImmutableArray();

            if (matchingDiagnostics.IsDefaultOrEmpty)
            {
                return [];
            }

            var actions = new List<CodeAction>();
            var span = matchingDiagnostics[0].Location.SourceSpan;
            var context = new CodeFixContext(
                document,
                span,
                matchingDiagnostics,
                (action, _) => actions.Add(action),
                cancellationToken);

            await provider.RegisterCodeFixesAsync(context);
            if (actions.Count > 0)
            {
                return actions;
            }
        }

        return [];
    }

    private static PortableExecutableReference[] CreateMetadataReferences()
    {
        return
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.GCSettings).Assembly.Location),
        ];
    }
}
