namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed class WorkspaceProjectInputResolver : IWorkspaceProjectInputResolver
{
    public WorkspaceProjectInputResolution Resolve(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return WorkspaceProjectInputResolution.Succeeded();
        }

        if (!File.Exists(projectPath))
        {
            return WorkspaceProjectInputResolution.Failed(
                projectPath,
                "The project file does not exist.");
        }

        try
        {
            using var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection();
            var project = projectCollection.LoadProject(projectPath);

            var paths = project.Imports
                .Select(static import => import.ImportedProject?.FullPath)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .OfType<string>()
                .Select(static path => Path.GetFullPath(path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return WorkspaceProjectInputResolution.Succeeded(paths);
        }
        catch (Exception exception) when (exception is Microsoft.Build.Exceptions.InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            return WorkspaceProjectInputResolution.Failed(projectPath, exception.Message);
        }
    }
}
