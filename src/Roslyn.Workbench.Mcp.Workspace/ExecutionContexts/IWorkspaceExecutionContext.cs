namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

/// <summary>
/// Provides the immutable solution, snapshot, limits, and resolution services for one tool execution.
/// </summary>
internal interface IWorkspaceExecutionContext
{
    /// <summary>
    /// Gets the solution visible to the execution.
    /// </summary>
    Solution CurrentSolution { get; }

    /// <summary>
    /// Gets the selected workspace identity.
    /// </summary>
    WorkspaceIdentity WorkspaceIdentity { get; }

    /// <summary>
    /// Gets the complete snapshot identity backing the execution.
    /// </summary>
    WorkspaceSnapshotIdentity SnapshotIdentity { get; }

    /// <summary>
    /// Gets the snapshot precondition exposed to tool callers.
    /// </summary>
    SnapshotPrecondition Snapshot { get; }

    /// <summary>
    /// Gets the active transaction revision, when executing against a transaction.
    /// </summary>
    int? TransactionRevision { get; }

    /// <summary>
    /// Gets the configured default maximum number of results.
    /// </summary>
    int DefaultMaxResults { get; }

    /// <summary>
    /// Gets the service for projecting paths within the selected workspace.
    /// </summary>
    IWorkspacePathService WorkspacePathService { get; }

    /// <summary>
    /// Gets the service for resolving selectors against the execution snapshot.
    /// </summary>
    IWorkspaceResolver WorkspaceResolver { get; }
}
