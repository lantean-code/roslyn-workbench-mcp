namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

/// <summary>
/// Releases every resource owned by a removed workspace session.
/// </summary>
internal interface IWorkspaceSessionCleanup
{
    /// <summary>
    /// Removes the instance-status publication and disposes the session's input manifest and loaded workspace.
    /// </summary>
    /// <param name="session">The removed session whose resources must be released.</param>
    /// <returns>A task that represents the asynchronous cleanup operation.</returns>
    ValueTask CleanupAsync(WorkspaceSessionSnapshot session);
}
