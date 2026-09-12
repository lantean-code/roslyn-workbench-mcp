namespace Roslyn.Workbench.Mcp.Workspace.Authority;

/// <summary>
/// Enforces immutable Host-owned authority over Workspace admission and recovery.
/// </summary>
internal interface IWorkspaceAuthority
{
    /// <summary>
    /// Gets a value indicating whether Workspace admission is restricted to configured roots.
    /// </summary>
    bool IsRestricted { get; }

    /// <summary>
    /// Gets the number of effective configured authority roots.
    /// </summary>
    int AllowedRootCount { get; }

    /// <summary>
    /// Gets the policy applied to evaluated documents outside an effective Workspace root.
    /// </summary>
    ExternalDocumentPolicy ExternalDocumentPolicy { get; }

    /// <summary>
    /// Attempts to find the configured authority root containing a path.
    /// </summary>
    /// <param name="path">The absolute path to test.</param>
    /// <param name="allowedRoot">The matching configured root, when admission succeeds.</param>
    /// <returns><see langword="true"/> when the path is admitted; otherwise, <see langword="false"/>.</returns>
    bool TryGetAllowedRoot(string path, out string? allowedRoot);

    /// <summary>
    /// Determines whether a caller-selected Workspace root remains within Host authority.
    /// </summary>
    /// <param name="workspaceRoot">The proposed effective Workspace root.</param>
    /// <returns><see langword="true"/> when the root is admitted; otherwise, <see langword="false"/>.</returns>
    bool IsWorkspaceRootAllowed(string workspaceRoot);

    /// <summary>
    /// Determines whether an admitted Workspace's loaded path and effective root still remain within Host authority.
    /// </summary>
    /// <param name="loadedPath">The Workspace's absolute solution or project path.</param>
    /// <param name="workspaceRoot">The Workspace's effective root.</param>
    /// <returns><see langword="true"/> when both paths remain admitted; otherwise, <see langword="false"/>.</returns>
    bool IsWorkspaceAllowed(string loadedPath, string workspaceRoot);
}
