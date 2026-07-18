namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class DefaultProjectStructureService : IProjectStructureService
{
    public ProjectTargetFrameworksResult GetTargetFrameworks(Project project)
    {
        return GetTargetFrameworks(project.FilePath);
    }

    public ProjectTargetFrameworksResult GetTargetFrameworks(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return ProjectTargetFrameworksResult.Succeeded();
        }

        if (!File.Exists(projectPath))
        {
            return ProjectTargetFrameworksResult.Failed(
                $"Could not evaluate target frameworks because project file '{projectPath}' does not exist.");
        }

        try
        {
            using var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection();
            var project = projectCollection.LoadProject(projectPath);
            var multipleTargetFrameworks = project.GetPropertyValue("TargetFrameworks");
            if (!string.IsNullOrWhiteSpace(multipleTargetFrameworks))
            {
                var evaluatedMultipleTargetFrameworks = multipleTargetFrameworks
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();
                return ProjectTargetFrameworksResult.Succeeded(evaluatedMultipleTargetFrameworks);
            }

            var singleTargetFramework = project.GetPropertyValue("TargetFramework");
            var evaluatedSingleTargetFramework = string.IsNullOrWhiteSpace(singleTargetFramework)
                ? []
                : new[] { singleTargetFramework.Trim() };
            return ProjectTargetFrameworksResult.Succeeded(evaluatedSingleTargetFramework);
        }
        catch (Exception exception) when (exception is Microsoft.Build.Exceptions.InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            return ProjectTargetFrameworksResult.Failed(
                $"Could not evaluate target frameworks for '{projectPath}': {exception.Message}");
        }
    }

    public async Task<SolutionHierarchyResult> GetSolutionHierarchyAsync(
        string? loadedPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(loadedPath))
        {
            return SolutionHierarchyResult.Succeeded();
        }

        if (!File.Exists(loadedPath))
        {
            return SolutionHierarchyResult.Failed(
                $"Could not load solution hierarchy because workspace file '{loadedPath}' does not exist.");
        }

        var serializer = SolutionSerializers.GetSerializerByMoniker(loadedPath);
        if (serializer is null)
        {
            return SolutionHierarchyResult.Succeeded();
        }

        try
        {
            var model = await serializer.OpenAsync(loadedPath, cancellationToken).ConfigureAwait(false);
            var folders = model.SolutionFolders
                .Select(static folder => CreateSolutionFolderInfo(folder))
                .OrderBy(static folder => folder.Path, StringComparer.Ordinal)
                .ToArray();
            var projectFolderPaths = model.SolutionProjects.ToDictionary(
                static project => NormalizeRelativeProjectPath(project.FilePath),
                static project => project.Parent is not null ? NormalizeFolderPath(project.Parent.Path) : null,
                StringComparer.Ordinal);

            return SolutionHierarchyResult.Succeeded(folders, projectFolderPaths);
        }
        catch (Exception exception) when (exception is Microsoft.VisualStudio.SolutionPersistence.Model.SolutionException or System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            return SolutionHierarchyResult.Failed(
                $"Could not load solution hierarchy for '{loadedPath}': {exception.Message}");
        }
    }

    private static SolutionFolderInfo CreateSolutionFolderInfo(SolutionFolderModel folder)
    {
        var folderPath = NormalizeFolderPath(folder.Path);
        return new SolutionFolderInfo
        {
            Name = GetFolderName(folderPath),
            Path = folderPath,
            ParentPath = GetParentFolderPath(folderPath),
        };
    }

    private static string NormalizeFolderPath(string path)
    {
        return path.Replace('\\', '/').Trim('/').Trim();
    }

    private static string NormalizeRelativeProjectPath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private static string GetFolderName(string folderPath)
    {
        var lastSeparatorIndex = folderPath.LastIndexOf('/');
        return lastSeparatorIndex < 0 ? folderPath : folderPath[(lastSeparatorIndex + 1)..];
    }

    private static string? GetParentFolderPath(string folderPath)
    {
        var lastSeparatorIndex = folderPath.LastIndexOf('/');
        return lastSeparatorIndex < 0 ? null : folderPath[..lastSeparatorIndex];
    }
}
