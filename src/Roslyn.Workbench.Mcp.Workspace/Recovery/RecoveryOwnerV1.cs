namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Represents the immutable persisted shape of a version 1 recovery owner record.
/// </summary>
internal sealed record RecoveryOwnerV1
{
    /// <summary>
    /// Gets the persisted format version.
    /// </summary>
    public int Version { get; init; } = RecoveryFormatVersions.V1;

    /// <summary>
    /// Gets the persisted commit identifier.
    /// </summary>
    public string? CommitId { get; init; }

    /// <summary>
    /// Gets the persisted loaded solution or project path.
    /// </summary>
    public string? LoadedPath { get; init; }

    /// <summary>
    /// Gets the persisted Workspace root.
    /// </summary>
    public string? WorkspaceRoot { get; init; }
}
