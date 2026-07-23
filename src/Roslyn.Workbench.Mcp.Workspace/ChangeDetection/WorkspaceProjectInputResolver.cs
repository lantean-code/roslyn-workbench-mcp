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

            var paths = new List<string>();
            var uniquePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var import in project.Imports)
            {
                var path = import.ImportedProject?.FullPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(path);
                if (uniquePaths.Add(fullPath))
                {
                    paths.Add(fullPath);
                }
            }

            return WorkspaceProjectInputResolution.Succeeded(paths.ToArray());
        }
        catch (Exception exception) when (exception is Microsoft.Build.Exceptions.InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            return WorkspaceProjectInputResolution.Failed(projectPath, exception.Message);
        }
    }
}
