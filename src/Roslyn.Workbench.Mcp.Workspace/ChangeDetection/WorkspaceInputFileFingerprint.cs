namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Records the path and inexpensive filesystem metadata used to detect a changed Workspace input.
/// </summary>
internal sealed record WorkspaceInputFileFingerprint
{
    /// <summary>
    /// Gets the normalized absolute file path.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the file's last-write timestamp in UTC when the manifest was certified.
    /// </summary>
    public DateTime LastWriteTimeUtc { get; init; }

    /// <summary>
    /// Gets the file length in bytes when the manifest was certified.
    /// </summary>
    public long Length { get; init; }
}
