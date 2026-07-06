using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.TestSupport;

public static class MiniWorkspaceFactory
{
    public static MiniWorkspace CreateCSharp(string source)
    {
        return CreateCSharp(
            [
                ("Sample.cs", source),
            ]);
    }

    public static MiniWorkspace CreateCSharp(IReadOnlyList<(string Path, string Source)> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            throw new ArgumentException("At least one document is required.", nameof(documents));
        }

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId("Sample");
        var versionStamp = VersionStamp.Create();
        var projectPath = "/workspace/Sample.csproj";
        var loadedPath = "/workspace/Sample.sln";

        var metadataReferences = GetMetadataReferences();
        var solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId,
            versionStamp,
            "Sample",
            "Sample",
            LanguageNames.CSharp,
            filePath: projectPath,
            metadataReferences: metadataReferences,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview)));

        var documentIdsByPath = new Dictionary<string, DocumentId>(StringComparer.Ordinal);

        foreach (var document in documents)
        {
            var normalizedPath = document.Path.Replace('\\', '/');
            var documentId = DocumentId.CreateNewId(projectId, normalizedPath);
            solution = solution.AddDocument(DocumentInfo.Create(
                documentId,
                normalizedPath,
                filePath: $"/workspace/{normalizedPath}",
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(document.Source), versionStamp))));
            documentIdsByPath[normalizedPath] = documentId;
        }

        workspace.TryApplyChanges(solution);

        return new MiniWorkspace(workspace, workspace.CurrentSolution, loadedPath, "Sample.csproj", documentIdsByPath.ToImmutableDictionary(StringComparer.Ordinal));
    }

    private static IReadOnlyList<MetadataReference> GetMetadataReferences()
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
