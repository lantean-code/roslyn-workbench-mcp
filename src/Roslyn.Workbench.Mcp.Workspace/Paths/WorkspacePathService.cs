using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Paths;

internal sealed class WorkspacePathService : IWorkspacePathService
{
    private readonly string _workspaceRoot;
    private readonly IWorkspacePathNormalizer _pathNormalizer;

    public WorkspacePathService(string workspaceRoot, IWorkspacePathNormalizer pathNormalizer)
    {
        _workspaceRoot = workspaceRoot;
        _pathNormalizer = pathNormalizer;
    }

    public bool TryNormalizePath(string path, [NotNullWhen(true)] out string? normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !_pathNormalizer.TryGetWorkspaceRelativePath(_workspaceRoot, path, out var relativePath))
        {
            normalizedPath = null;
            return false;
        }

        normalizedPath = relativePath;
        return true;
    }
}
