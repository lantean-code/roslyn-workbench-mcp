using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class UnavailableProjectStructureService : IProjectStructureService
{
    private const string _message = "Tool execution services are unavailable.";

    public IReadOnlyList<string> GetTargetFrameworks(Project project)
    {
        _ = project;

        throw new InvalidOperationException(_message);
    }

    public IReadOnlyList<string> GetTargetFrameworks(string? projectPath)
    {
        _ = projectPath;

        throw new InvalidOperationException(_message);
    }

    public Task<(IReadOnlyList<SolutionFolderInfo> Folders, IReadOnlyDictionary<string, string?> ProjectFolderPaths)> GetSolutionHierarchyAsync(
        string? loadedPath,
        CancellationToken cancellationToken)
    {
        _ = loadedPath;
        _ = cancellationToken;

        return Task.FromException<(IReadOnlyList<SolutionFolderInfo> Folders, IReadOnlyDictionary<string, string?> ProjectFolderPaths)>(new InvalidOperationException(_message));
    }
}
