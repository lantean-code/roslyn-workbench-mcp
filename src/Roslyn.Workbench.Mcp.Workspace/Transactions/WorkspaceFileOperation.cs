namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Defines the durable file operations supported by a workspace commit. Numeric values are persisted and must remain stable.
/// </summary>
internal enum WorkspaceFileOperation
{
    /// <summary>
    /// Create a file that did not exist in the baseline.
    /// </summary>
    Create = 0,

    /// <summary>
    /// Replace the contents or permissions of an existing file.
    /// </summary>
    Replace = 1,

    /// <summary>
    /// Delete a file that existed in the baseline.
    /// </summary>
    Delete = 2,
}
