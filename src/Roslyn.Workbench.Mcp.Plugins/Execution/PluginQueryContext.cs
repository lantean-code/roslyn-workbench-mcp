namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class PluginQueryContext : IQueryContext
{
    public PluginQueryContext(
        IWorkspaceExecutionContext workspaceContext,
        IToolExecutionServices toolExecutionServices,
        IQueryResultCache queryResultCache)
    {
        CurrentSolution = workspaceContext.CurrentSolution;
        WorkspaceIdentity = workspaceContext.WorkspaceIdentity;
        TransactionRevision = workspaceContext.TransactionRevision;
        DefaultMaxResults = workspaceContext.DefaultMaxResults;
        WorkspacePathService = workspaceContext.WorkspacePathService;
        WorkspaceResolver = workspaceContext.WorkspaceResolver;
        ToolExecutionServices = toolExecutionServices;
        QueryResultCache = queryResultCache;
    }

    public Solution CurrentSolution { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public int? TransactionRevision { get; }

    public int DefaultMaxResults { get; }

    public IWorkspacePathService WorkspacePathService { get; }

    public IWorkspaceResolver WorkspaceResolver { get; }

    public IToolExecutionServices ToolExecutionServices { get; }

    public IQueryResultCache QueryResultCache { get; }
}
