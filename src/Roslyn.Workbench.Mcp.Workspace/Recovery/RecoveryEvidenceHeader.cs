namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Carries the version and safe identity fields read before format-specific recovery deserialisation.
/// </summary>
internal sealed record RecoveryEvidenceHeader
{
    /// <summary>
    /// Gets the persisted recovery format version.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Gets the persisted loaded path when represented as a string.
    /// </summary>
    public string? LoadedPath { get; }

    /// <summary>
    /// Gets the persisted Workspace root when represented as a string.
    /// </summary>
    public string? WorkspaceRoot { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RecoveryEvidenceHeader"/> class.
    /// </summary>
    /// <param name="version">The persisted recovery format version.</param>
    /// <param name="loadedPath">The persisted loaded path when represented as a string.</param>
    /// <param name="workspaceRoot">The persisted Workspace root when represented as a string.</param>
    public RecoveryEvidenceHeader(int version, string? loadedPath, string? workspaceRoot)
    {
        Version = version;
        LoadedPath = loadedPath;
        WorkspaceRoot = workspaceRoot;
    }
}
