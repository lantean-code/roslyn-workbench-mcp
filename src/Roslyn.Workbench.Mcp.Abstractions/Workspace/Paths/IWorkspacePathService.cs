using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Paths;

/// <summary>
/// Provides path normalization relative to the workspace for the current invocation.
/// </summary>
public interface IWorkspacePathService
{
    /// <summary>
    /// Attempts to normalize a path to its workspace-relative form.
    /// </summary>
    /// <param name="path">The absolute or workspace-relative path to normalize.</param>
    /// <param name="normalizedPath">The normalized workspace-relative path when normalization succeeds; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the path was normalized; otherwise, <see langword="false"/>.</returns>
    bool TryNormalizePath(string path, [NotNullWhen(true)] out string? normalizedPath);
}
