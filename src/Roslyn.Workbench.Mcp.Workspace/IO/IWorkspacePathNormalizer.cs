namespace Roslyn.Workbench.Mcp.Workspace.IO;

/// <summary>
/// Canonicalises workspace paths without exposing expected path-resolution exceptions.
/// </summary>
internal interface IWorkspacePathNormalizer
{
    /// <summary>
    /// Attempts to canonicalise an absolute or process-relative path.
    /// </summary>
    /// <param name="path">The path to canonicalise.</param>
    /// <param name="fullPath">The canonical full path on success.</param>
    /// <returns><see langword="true"/> when canonicalisation succeeds; otherwise, <see langword="false"/>.</returns>
    bool TryGetFullPath(string path, out string fullPath);

    /// <summary>
    /// Attempts to canonicalise a path relative to a specified base.
    /// </summary>
    /// <param name="path">The path to canonicalise.</param>
    /// <param name="basePath">The directory against which a relative path is resolved.</param>
    /// <param name="fullPath">The canonical full path on success.</param>
    /// <returns><see langword="true"/> when canonicalisation succeeds; otherwise, <see langword="false"/>.</returns>
    bool TryGetFullPath(string path, string basePath, out string fullPath);

    /// <summary>
    /// Attempts to project a path relative to a workspace root using forward separators.
    /// </summary>
    /// <param name="workspaceRoot">The optional workspace root.</param>
    /// <param name="path">The full path to project relative to the workspace.</param>
    /// <param name="relativePath">The forward-separated relative path on success.</param>
    /// <returns><see langword="true"/> when projection succeeds; otherwise, <see langword="false"/>.</returns>
    bool TryGetWorkspaceRelativePath(string workspaceRoot, string path, out string relativePath);
}
