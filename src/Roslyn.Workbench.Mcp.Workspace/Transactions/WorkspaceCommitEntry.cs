namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Records one recoverable file operation and its expected original and intended state.
/// </summary>
internal sealed record WorkspaceCommitEntry
{
    /// <summary>
    /// Gets the workspace file to create, replace, or delete.
    /// </summary>
    public required string TargetPath { get; init; }

    /// <summary>
    /// Gets the file operation to perform.
    /// </summary>
    public required WorkspaceFileOperation Operation { get; init; }

    /// <summary>
    /// Gets a value indicating whether the target existed before the commit.
    /// </summary>
    public required bool OriginalExists { get; init; }

    /// <summary>
    /// Gets the expected hash of the original file, when it existed.
    /// </summary>
    public string? OriginalHash { get; init; }

    /// <summary>
    /// Gets the hash expected after the operation, when a file will remain.
    /// </summary>
    public string? IntendedHash { get; init; }

    /// <summary>
    /// Gets the original Unix file mode, when available.
    /// </summary>
    public UnixFileMode? OriginalUnixFileMode { get; init; }

    /// <summary>
    /// Gets the Unix file mode to apply to the committed file, when available.
    /// </summary>
    public UnixFileMode? IntendedUnixFileMode { get; init; }

    /// <summary>
    /// Gets the recovery-artifact path containing original file bytes.
    /// </summary>
    public string? BackupPath { get; init; }

    /// <summary>
    /// Gets the recovery-artifact path containing intended file bytes.
    /// </summary>
    public string? StagedPath { get; init; }

    /// <summary>
    /// Gets the recovery-artifact path marking an intended deletion.
    /// </summary>
    public string? DeleteMarkerPath { get; init; }

    /// <summary>
    /// Gets the required backup artifact path or throws when the entry has none.
    /// </summary>
    /// <returns>The required backup path.</returns>
    public string GetRequiredBackupPath()
    {
        return BackupPath ?? throw new InvalidOperationException("The commit entry does not contain a backup path.");
    }

    /// <summary>
    /// Gets the required staged-content artifact path or throws when the entry has none.
    /// </summary>
    /// <returns>The required staged path.</returns>
    public string GetRequiredStagedPath()
    {
        return StagedPath ?? throw new InvalidOperationException("The commit entry does not contain a staged path.");
    }

    /// <summary>
    /// Gets the required deletion-marker artifact path or throws when the entry has none.
    /// </summary>
    /// <returns>The required delete marker path.</returns>
    public string GetRequiredDeleteMarkerPath()
    {
        return DeleteMarkerPath ?? throw new InvalidOperationException("The commit entry does not contain a delete marker path.");
    }
}
