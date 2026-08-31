namespace Roslyn.Workbench.Mcp.Workspace.ExecutionContexts;

/// <summary>
/// Carries the immutable workspace view and snapshot-scoped services for one tool execution.
/// </summary>
internal sealed class WorkspaceExecutionContext : IWorkspaceExecutionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceExecutionContext"/> class.
    /// </summary>
    /// <param name="currentSolution">The solution visible to the execution.</param>
    /// <param name="workspaceIdentity">The selected workspace identity.</param>
    /// <param name="snapshotIdentity">The complete snapshot identity.</param>
    /// <param name="snapshot">The caller-facing snapshot precondition.</param>
    /// <param name="transactionRevision">The active transaction revision, when present.</param>
    /// <param name="defaultMaxResults">The configured default result limit.</param>
    /// <param name="workspacePathService">The snapshot-scoped path service.</param>
    /// <param name="workspaceResolver">The snapshot-scoped selector resolver.</param>
    public WorkspaceExecutionContext(
        Solution currentSolution,
        WorkspaceIdentity workspaceIdentity,
        WorkspaceSnapshotIdentity snapshotIdentity,
        SnapshotPrecondition snapshot,
        int? transactionRevision,
        int defaultMaxResults,
        IWorkspacePathService workspacePathService,
        IWorkspaceResolver workspaceResolver)
    {
        CurrentSolution = currentSolution;
        WorkspaceIdentity = workspaceIdentity;
        SnapshotIdentity = snapshotIdentity;
        Snapshot = snapshot;
        TransactionRevision = transactionRevision;
        DefaultMaxResults = defaultMaxResults;
        WorkspacePathService = workspacePathService;
        WorkspaceResolver = workspaceResolver;
    }

    /// <inheritdoc/>
    public Solution CurrentSolution { get; }

    /// <inheritdoc/>
    public WorkspaceIdentity WorkspaceIdentity { get; }

    /// <inheritdoc/>
    public WorkspaceSnapshotIdentity SnapshotIdentity { get; }

    /// <inheritdoc/>
    public SnapshotPrecondition Snapshot { get; }

    /// <inheritdoc/>
    public int? TransactionRevision { get; }

    /// <inheritdoc/>
    public int DefaultMaxResults { get; }

    /// <inheritdoc/>
    public IWorkspacePathService WorkspacePathService { get; }

    /// <inheritdoc/>
    public IWorkspaceResolver WorkspaceResolver { get; }
}
