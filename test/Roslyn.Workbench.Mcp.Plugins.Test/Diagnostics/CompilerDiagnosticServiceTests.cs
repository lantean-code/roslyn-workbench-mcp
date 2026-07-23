using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Test.Diagnostics;

public sealed class CompilerDiagnosticServiceTests
{
    [Fact]
    public async Task GIVEN_ProjectWithoutDiagnostics_WHEN_GettingCompilerDiagnostics_THEN_ShouldReturnEmpty()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
                public string Format(string value)
                {
                    return value.Trim();
                }
            }
            """);
        var target = new CompilerDiagnosticService();
        var document = workspace.Solution.Projects.Single().Documents.Single();

        var result = await target.GetCompilerDiagnosticsAsync([document], TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_SelectedDocumentsAcrossProjects_WHEN_GettingCompilerDiagnostics_THEN_ShouldFilterToSelectedDocuments()
    {
        using var workspace = CreateMultiProjectWorkspace(
            ("ProjectOne", "First.cs", """
                namespace Sample;

                public sealed class First
                {
                    public void Run()
                    {
                        var unused = 42;
                    }
                }
                """),
            ("ProjectTwo", "Second.cs", """
                namespace Sample;

                public sealed class Second
                {
                    public void Run()
                    {
                        return;
                    }
                }
                """));
        var target = new CompilerDiagnosticService();
        var selectedDocument = workspace.CurrentSolution.Projects.Single(static project => project.Name == "ProjectOne").Documents.Single();

        var result = await target.GetCompilerDiagnosticsAsync([selectedDocument], TestContext.Current.CancellationToken);

        result.Should().ContainSingle(static diagnostic => diagnostic.Id == "CS0219");
    }

    [Fact]
    public async Task GIVEN_DuplicateLinkedDocumentDiagnostics_WHEN_GettingCompilerDiagnostics_THEN_ShouldReturnDistinctDiagnostics()
    {
        using var workspace = CreateLinkedDocumentWorkspace("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
                public void Run()
                {
                    var unused = 42;
                }
            }
            """);
        var target = new CompilerDiagnosticService();
        var selectedDocuments = workspace.CurrentSolution.Projects.SelectMany(static project => project.Documents).ToArray();

        var result = await target.GetCompilerDiagnosticsAsync(selectedDocuments, TestContext.Current.CancellationToken);

        result.Should().ContainSingle(static diagnostic => diagnostic.Id == "CS0219");
    }

    private static AdhocWorkspace CreateLinkedDocumentWorkspace(string source)
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        var projectIds = new[]
        {
            ProjectId.CreateNewId("ProjectOne"),
            ProjectId.CreateNewId("ProjectTwo"),
        };

        foreach (var projectId in projectIds)
        {
            solution = solution.AddProject(Microsoft.CodeAnalysis.ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                $"Project{Array.IndexOf(projectIds, projectId) + 1}",
                $"Project{Array.IndexOf(projectIds, projectId) + 1}",
                LanguageNames.CSharp,
                metadataReferences: GetMetadataReferences(),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: new CSharpParseOptions(LanguageVersion.Preview)));

            solution = solution.AddDocument(DocumentInfo.Create(
                DocumentId.CreateNewId(projectId, "Shared/SharedClass.cs"),
                "Shared/SharedClass.cs",
                filePath: "/workspace/Shared/SharedClass.cs",
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(source), VersionStamp.Create()))));
        }

        workspace.TryApplyChanges(solution);
        return workspace;
    }

    private static AdhocWorkspace CreateMultiProjectWorkspace(params (string ProjectName, string DocumentName, string Source)[] projects)
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;

        foreach (var project in projects)
        {
            var projectId = ProjectId.CreateNewId(project.ProjectName);
            solution = solution.AddProject(Microsoft.CodeAnalysis.ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                project.ProjectName,
                project.ProjectName,
                LanguageNames.CSharp,
                metadataReferences: GetMetadataReferences(),
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: new CSharpParseOptions(LanguageVersion.Preview)));
            solution = solution.AddDocument(DocumentInfo.Create(
                DocumentId.CreateNewId(projectId, project.DocumentName),
                project.DocumentName,
                filePath: $"/workspace/{project.ProjectName}/{project.DocumentName}",
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(project.Source), VersionStamp.Create()))));
        }

        workspace.TryApplyChanges(solution);
        return workspace;
    }

    private static PortableExecutableReference[] GetMetadataReferences()
    {
        var locations = new[]
        {
            typeof(object).Assembly.Location,
            typeof(Enumerable).Assembly.Location,
            typeof(Console).Assembly.Location,
            typeof(System.Runtime.GCSettings).Assembly.Location,
        };

        return locations
            .Where(static location => !string.IsNullOrWhiteSpace(location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static location => MetadataReference.CreateFromFile(location))
            .ToArray();
    }
}
