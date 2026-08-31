namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Contexts;

/// <summary>
/// Exposes the immutable workspace snapshot and services available during Code Action execution.
/// </summary>
internal interface ICodeActionExecutionContext
{
    /// <summary>
    /// Gets the immutable solution acquired for the invocation.
    /// </summary>
    Solution CurrentSolution { get; }

    /// <summary>
    /// Gets the loaded workspace identity.
    /// </summary>
    WorkspaceIdentity WorkspaceIdentity { get; }

    /// <summary>
    /// Gets the internal identity of the acquired solution snapshot.
    /// </summary>
    WorkspaceSnapshotIdentity SnapshotIdentity { get; }

    /// <summary>
    /// Gets the snapshot precondition published to tool clients.
    /// </summary>
    SnapshotPrecondition Snapshot { get; }

    /// <summary>
    /// Gets the transaction revision included in the snapshot, when present.
    /// </summary>
    int? TransactionRevision { get; }

    /// <summary>
    /// Gets the Host default used when a request omits a result limit.
    /// </summary>
    int DefaultMaxResults { get; }

    /// <summary>
    /// Gets the service that enforces workspace path boundaries.
    /// </summary>
    IWorkspacePathService WorkspacePathService { get; }

    /// <summary>
    /// Gets the resolver scoped to the acquired solution snapshot.
    /// </summary>
    IWorkspaceResolver WorkspaceResolver { get; }
}
