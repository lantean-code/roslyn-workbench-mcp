namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

/// <summary>
/// Records the workspace ownership and active operation advertised by one server instance.
/// </summary>
internal sealed record WorkspaceInstanceStatus
{
    /// <summary>
    /// Gets the instance-record format version.
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Gets the identifier of the server instance that owns the record.
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Gets the solution or project path loaded by the instance.
    /// </summary>
    public required string LoadedPath { get; init; }

    /// <summary>
    /// Gets the trusted workspace root owned by the instance.
    /// </summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>
    /// Gets the instance's current workspace lifecycle state.
    /// </summary>
    public required WorkspaceLifecycleState WorkspaceState { get; init; }

    /// <summary>
    /// Gets the active transaction revision, when one exists.
    /// </summary>
    public long? TransactionRevision { get; init; }

    /// <summary>
    /// Gets the identifier of the active commit, when one exists.
    /// </summary>
    public string? CommitId { get; init; }

    /// <summary>
    /// Gets the current phase of the active commit, when one exists.
    /// </summary>
    public string? CommitPhase { get; init; }
}
