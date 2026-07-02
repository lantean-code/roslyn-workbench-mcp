using Roslyn.Workbench.Mcp.Contracts.Inspection;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal static class ProjectFileUtilities
{
    public static IReadOnlyList<string> GetTargetFrameworks(Microsoft.CodeAnalysis.Project project)
    {
        return GetTargetFrameworks(project.FilePath);
    }

    public static IReadOnlyList<string> GetTargetFrameworks(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
        {
            return [];
        }

        try
        {
            using var projectCollection = new ProjectCollection();
            var project = projectCollection.LoadProject(projectPath);
            var multipleTargetFrameworks = project.GetPropertyValue("TargetFrameworks");
            if (!string.IsNullOrWhiteSpace(multipleTargetFrameworks))
            {
                return multipleTargetFrameworks
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();
            }

            var singleTargetFramework = project.GetPropertyValue("TargetFramework");
            return string.IsNullOrWhiteSpace(singleTargetFramework)
                ? []
                : [singleTargetFramework.Trim()];
        }
        catch
        {
            return [];
        }
    }

    public static async Task<SolutionHierarchyInfo> GetSolutionHierarchyAsync(string? loadedPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loadedPath) || !File.Exists(loadedPath))
        {
            return SolutionHierarchyInfo.Empty;
        }

        var serializer = SolutionSerializers.GetSerializerByMoniker(loadedPath);
        if (serializer is null)
        {
            return SolutionHierarchyInfo.Empty;
        }

        try
        {
            var model = await serializer.OpenAsync(loadedPath, cancellationToken);
            var folders = model.SolutionFolders
                .Select(static folder => CreateSolutionFolderInfo(folder))
                .OrderBy(static folder => folder.Path, StringComparer.Ordinal)
                .ToArray();
            var projectFolderPaths = model.SolutionProjects.ToDictionary(
                static project => NormalizeRelativeProjectPath(project.FilePath),
                static project => project.Parent is not null ? NormalizeFolderPath(project.Parent.Path) : null,
                StringComparer.Ordinal);

            return new SolutionHierarchyInfo(folders, projectFolderPaths);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return SolutionHierarchyInfo.Empty;
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

    private static string NormalizeFolderPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').Trim('/').Trim();
    }

    private static string NormalizeRelativeProjectPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').Trim();
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

    internal sealed record SolutionHierarchyInfo(
        IReadOnlyList<SolutionFolderInfo> Folders,
        IReadOnlyDictionary<string, string?> ProjectFolderPaths)
    {
        public static SolutionHierarchyInfo Empty { get; } = new([], new Dictionary<string, string?>(StringComparer.Ordinal));
    }
}
