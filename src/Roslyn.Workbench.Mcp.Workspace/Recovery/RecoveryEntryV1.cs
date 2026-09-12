namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Represents the persisted shape of one version 1 recovery operation.
/// </summary>
internal sealed record RecoveryEntryV1
{
    /// <summary>
    /// Gets the persisted target path.
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// Gets the persisted file operation.
    /// </summary>
    public WorkspaceFileOperation Operation { get; init; }

    /// <summary>
    /// Gets whether the target originally existed.
    /// </summary>
    public bool OriginalExists { get; init; }

    /// <summary>
    /// Gets the persisted original-content hash.
    /// </summary>
    public string? OriginalHash { get; init; }

    /// <summary>
    /// Gets the persisted intended-content hash.
    /// </summary>
    public string? IntendedHash { get; init; }

    /// <summary>
    /// Gets the persisted original Unix file mode.
    /// </summary>
    public UnixFileMode? OriginalUnixFileMode { get; init; }

    /// <summary>
    /// Gets the persisted intended Unix file mode.
    /// </summary>
    public UnixFileMode? IntendedUnixFileMode { get; init; }

    /// <summary>
    /// Gets the persisted backup artifact path.
    /// </summary>
    public string? BackupPath { get; init; }

    /// <summary>
    /// Gets the persisted staged artifact path.
    /// </summary>
    public string? StagedPath { get; init; }

    /// <summary>
    /// Gets the persisted deletion marker path.
    /// </summary>
    public string? DeleteMarkerPath { get; init; }
}
