namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Represents the immutable persisted shape of a version 1 recovery manifest.
/// </summary>
internal sealed record RecoveryManifestV1
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

    /// <summary>
    /// Gets the persisted recovery state.
    /// </summary>
    public RecoveryState State { get; init; }

    /// <summary>
    /// Gets the persisted file operations.
    /// </summary>
    public IReadOnlyList<RecoveryEntryV1?>? Entries { get; init; }

    /// <summary>
    /// Gets the persisted directories created during the commit.
    /// </summary>
    public IReadOnlyList<string?>? CreatedDirectories { get; init; }

    /// <summary>
    /// Gets the persisted supplementary recovery information.
    /// </summary>
    public string? Message { get; init; }
}
