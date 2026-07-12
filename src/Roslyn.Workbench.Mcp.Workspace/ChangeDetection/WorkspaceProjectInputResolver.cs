namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceProjectInputResolver : IWorkspaceProjectInputResolver
{
    public IReadOnlyList<string> GetEvaluatedInputPaths(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
        {
            return [];
        }

        try
        {
            using var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection();
            var project = projectCollection.LoadProject(projectPath);

            return project.Imports
                .Select(static import => import.ImportedProject?.FullPath)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .OfType<string>()
                .Select(static path => Path.GetFullPath(path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is Microsoft.Build.Exceptions.InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
