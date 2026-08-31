namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Contexts;

/// <summary>
/// Projects a neutral workspace execution context onto the query-handler contract.
/// </summary>
internal sealed class CodeActionQueryContext : ICodeActionQueryContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionQueryContext"/> class.
    /// </summary>
    /// <param name="workspaceContext">The workspace context in which the operation executes.</param>
    public CodeActionQueryContext(IWorkspaceExecutionContext workspaceContext)
    {
        CurrentSolution = workspaceContext.CurrentSolution;
        WorkspaceIdentity = workspaceContext.WorkspaceIdentity;
        SnapshotIdentity = workspaceContext.SnapshotIdentity;
        Snapshot = workspaceContext.Snapshot;
        TransactionRevision = workspaceContext.TransactionRevision;
        DefaultMaxResults = workspaceContext.DefaultMaxResults;
        WorkspacePathService = workspaceContext.WorkspacePathService;
        WorkspaceResolver = workspaceContext.WorkspaceResolver;
    }

    /// <summary>
    /// Gets the immutable solution inspected by the query.
    /// </summary>
    public Solution CurrentSolution { get; }

    /// <summary>
    /// Gets the loaded workspace identity.
    /// </summary>
    public WorkspaceIdentity WorkspaceIdentity { get; }

    /// <summary>
    /// Gets the internal identity of the acquired solution snapshot.
    /// </summary>
    public WorkspaceSnapshotIdentity SnapshotIdentity { get; }

    /// <summary>
    /// Gets the snapshot precondition published to tool clients.
    /// </summary>
    public SnapshotPrecondition Snapshot { get; }

    /// <summary>
    /// Gets the transaction revision included in the acquired snapshot, when present.
    /// </summary>
    public int? TransactionRevision { get; }

    /// <summary>
    /// Gets the Host default used when a request omits a result limit.
    /// </summary>
    public int DefaultMaxResults { get; }

    /// <summary>
    /// Gets the service that enforces workspace path boundaries.
    /// </summary>
    public IWorkspacePathService WorkspacePathService { get; }

    /// <summary>
    /// Gets the resolver scoped to the acquired solution snapshot.
    /// </summary>
    public IWorkspaceResolver WorkspaceResolver { get; }
}
