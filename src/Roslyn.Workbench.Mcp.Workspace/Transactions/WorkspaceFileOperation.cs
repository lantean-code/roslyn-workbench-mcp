namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Defines the durable file operations supported by a workspace commit.
/// </summary>
internal enum WorkspaceFileOperation
{
    /// <summary>
    /// Create a file that did not exist in the baseline.
    /// </summary>
    Create,
    /// <summary>
    /// Replace the contents or permissions of an existing file.
    /// </summary>
    Replace,
    /// <summary>
    /// Delete a file that existed in the baseline.
    /// </summary>
    Delete,
}
