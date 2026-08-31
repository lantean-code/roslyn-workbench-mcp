using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Paths;

/// <summary>
/// Projects canonical file-system paths as stable workspace-relative paths.
/// </summary>
internal sealed class WorkspacePathService : IWorkspacePathService
{
    private readonly string _workspaceRoot;
    private readonly IWorkspacePathNormalizer _pathNormalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspacePathService"/> class.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root path.</param>
    /// <param name="pathNormalizer">The service used to normalize workspace paths.</param>
    public WorkspacePathService(string workspaceRoot, IWorkspacePathNormalizer pathNormalizer)
    {
        _workspaceRoot = workspaceRoot;
        _pathNormalizer = pathNormalizer;
    }

    /// <summary>
    /// Attempts to canonicalise a path and express it relative to the workspace root.
    /// </summary>
    /// <param name="path">The absolute or process-relative path to project.</param>
    /// <param name="normalizedPath">The forward-separated workspace-relative path on success.</param>
    /// <returns><see langword="true"/> when the path is valid and belongs to the workspace; otherwise, <see langword="false"/>.</returns>
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
