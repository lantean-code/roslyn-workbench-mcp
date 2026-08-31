namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Projects a neutral workspace execution context, plugin services and scoped cache onto the query-handler contract.
/// </summary>
internal sealed class PluginQueryContext : IQueryContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginQueryContext"/> class.
    /// </summary>
    /// <param name="workspaceContext">The acquired neutral workspace context.</param>
    /// <param name="toolExecutionServices">The services exposed to the plugin handler.</param>
    /// <param name="queryResultCache">The cache isolated to the current snapshot, plugin and tool.</param>
    public PluginQueryContext(
        IWorkspaceExecutionContext workspaceContext,
        IToolExecutionServices toolExecutionServices,
        IQueryResultCache queryResultCache)
    {
        CurrentSolution = workspaceContext.CurrentSolution;
        WorkspaceIdentity = workspaceContext.WorkspaceIdentity;
        Snapshot = workspaceContext.Snapshot;
        TransactionRevision = workspaceContext.TransactionRevision;
        DefaultMaxResults = workspaceContext.DefaultMaxResults;
        WorkspacePathService = workspaceContext.WorkspacePathService;
        WorkspaceResolver = workspaceContext.WorkspaceResolver;
        ToolExecutionServices = toolExecutionServices;
        QueryResultCache = queryResultCache;
    }

    /// <inheritdoc/>
    public Solution CurrentSolution { get; }

    /// <inheritdoc/>
    public WorkspaceIdentity WorkspaceIdentity { get; }

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

    /// <inheritdoc/>
    public IToolExecutionServices ToolExecutionServices { get; }

    /// <inheritdoc/>
    public IQueryResultCache QueryResultCache { get; }
}
