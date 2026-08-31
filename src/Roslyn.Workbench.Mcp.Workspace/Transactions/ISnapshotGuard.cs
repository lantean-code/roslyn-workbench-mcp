namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Enforces optimistic-concurrency preconditions for transaction operations.
/// </summary>
internal interface ISnapshotGuard
{
    /// <summary>
    /// Validates a caller's expected snapshot against the current session.
    /// </summary>
    /// <param name="session">The workspace session in which the operation runs.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <returns>A successful validation or a structured snapshot error.</returns>
    SnapshotValidationResult Validate(WorkspaceSessionSnapshot session, SnapshotPrecondition? expectedSnapshot);
}
