using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Persistently records a recoverable workspace commit and its current application state.
/// </summary>
internal sealed record WorkspaceCommitManifest
{
    /// <summary>
    /// Gets a value indicating whether the workspace identity could not be parsed.
    /// </summary>
    [JsonIgnore]
    public bool HasMalformedWorkspaceIdentity { get; init; }

    /// <summary>
    /// Gets the manifest format version.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Gets the durable commit identifier.
    /// </summary>
    public required string CommitId { get; init; }

    /// <summary>
    /// Gets the solution or project path loaded by the workspace.
    /// </summary>
    public required string LoadedPath { get; init; }

    /// <summary>
    /// Gets the workspace root that bounds every file operation.
    /// </summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>
    /// Gets the current recovery state of the commit.
    /// </summary>
    public required RecoveryState State { get; init; }

    /// <summary>
    /// Gets the ordered file operations in the commit.
    /// </summary>
    public required IReadOnlyList<WorkspaceCommitEntry> Entries { get; init; }

    /// <summary>
    /// Gets directories created while applying the commit and eligible for rollback cleanup.
    /// </summary>
    public required IReadOnlyList<string> CreatedDirectories { get; init; }

    /// <summary>
    /// Gets supplementary recovery status or failure information.
    /// </summary>
    public string? Message { get; init; }
}
