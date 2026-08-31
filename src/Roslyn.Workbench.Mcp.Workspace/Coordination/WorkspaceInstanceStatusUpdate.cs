namespace Roslyn.Workbench.Mcp.Workspace.Coordination;

/// <summary>
/// Carries one queued update for a live workspace instance record.
/// </summary>
internal sealed record WorkspaceInstanceStatusUpdate
{
    /// <summary>
    /// Gets the workspace whose instance record should be updated.
    /// </summary>
    public required Guid WorkspaceId { get; init; }

    /// <summary>
    /// Gets the current workspace lifecycle state.
    /// </summary>
    public required WorkspaceLifecycleState State { get; init; }

    /// <summary>
    /// Gets the current transaction revision, when a transaction is active.
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

    /// <summary>
    /// Gets the optional completion source signalled after the record is written.
    /// </summary>
    public TaskCompletionSource? Completion { get; init; }
}
