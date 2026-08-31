namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Projects a neutral workspace execution context and plugin services onto the mutation-handler contract.
/// </summary>
internal sealed class PluginMutationContext : IMutationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginMutationContext"/> class.
    /// </summary>
    /// <param name="workspaceContext">The acquired neutral workspace context.</param>
    /// <param name="toolExecutionServices">The services exposed to the plugin handler.</param>
    public PluginMutationContext(
        IWorkspaceExecutionContext workspaceContext,
        IToolExecutionServices toolExecutionServices)
    {
        CurrentSolution = workspaceContext.CurrentSolution;
        WorkspaceIdentity = workspaceContext.WorkspaceIdentity;
        Snapshot = workspaceContext.Snapshot;
        TransactionRevision = workspaceContext.TransactionRevision;
        DefaultMaxResults = workspaceContext.DefaultMaxResults;
        WorkspacePathService = workspaceContext.WorkspacePathService;
        WorkspaceResolver = workspaceContext.WorkspaceResolver;
        ToolExecutionServices = toolExecutionServices;
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
}
