namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Identifies the workspace and commit that own a recovery directory.
/// </summary>
internal sealed record WorkspaceCommitOwner
{
    /// <summary>
    /// Gets the owner record format version.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Gets the workspace commit identifier.
    /// </summary>
    public required string CommitId { get; init; }

    /// <summary>
    /// Gets the loaded solution or project path associated with the commit.
    /// </summary>
    public required string LoadedPath { get; init; }

    /// <summary>
    /// Gets the workspace root associated with the commit.
    /// </summary>
    public required string WorkspaceRoot { get; init; }
}
