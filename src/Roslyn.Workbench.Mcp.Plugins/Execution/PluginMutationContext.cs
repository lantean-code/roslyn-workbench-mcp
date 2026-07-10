namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class PluginMutationContext : IMutationContext
{
    public PluginMutationContext(
        IWorkspaceExecutionContext workspaceContext,
        IToolExecutionServices toolExecutionServices)
    {
        CurrentSolution = workspaceContext.CurrentSolution;
        WorkspaceIdentity = workspaceContext.WorkspaceIdentity;
        TransactionRevision = workspaceContext.TransactionRevision;
        DefaultMaxResults = workspaceContext.DefaultMaxResults;
        WorkspaceResolver = workspaceContext.WorkspaceResolver;
        ToolExecutionServices = toolExecutionServices;
    }

    public Solution CurrentSolution { get; }

    public WorkspaceIdentity WorkspaceIdentity { get; }

    public int? TransactionRevision { get; }

    public int DefaultMaxResults { get; }

    public IWorkspaceResolver WorkspaceResolver { get; }

    public IToolExecutionServices ToolExecutionServices { get; }
}
