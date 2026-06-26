using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace;

internal static class WorkspaceInputManifestBuilder
{
    public static WorkspaceInputManifest Build(Solution solution, string loadedPath)
    {
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentException.ThrowIfNullOrWhiteSpace(loadedPath);

        var filePaths = new HashSet<string>(StringComparer.Ordinal);

        AddFilePath(filePaths, loadedPath);

        foreach (var project in solution.Projects)
        {
            AddFilePath(filePaths, project.FilePath);

            foreach (var document in project.Documents)
            {
                AddFilePath(filePaths, document.FilePath);
            }

            foreach (var document in project.AdditionalDocuments)
            {
                AddFilePath(filePaths, document.FilePath);
            }

            foreach (var document in project.AnalyzerConfigDocuments)
            {
                AddFilePath(filePaths, document.FilePath);
            }

            foreach (var analyzerReference in project.AnalyzerReferences)
            {
                AddFilePath(filePaths, analyzerReference.Display);
            }

            foreach (var metadataReference in project.MetadataReferences.OfType<PortableExecutableReference>())
            {
                AddFilePath(filePaths, metadataReference.FilePath);
            }

            foreach (var importPath in MsBuildProjectUtilities.GetEvaluatedInputPaths(project.FilePath))
            {
                AddFilePath(filePaths, importPath);
            }
        }

        return new WorkspaceInputManifest
        {
            Files = filePaths
                .Select(WorkspaceInputFileFingerprint.Create)
                .ToArray(),
        };
    }

    private static void AddFilePath(ISet<string> filePaths, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(path);
        if (File.Exists(normalizedPath))
        {
            filePaths.Add(normalizedPath);
        }
    }
}
