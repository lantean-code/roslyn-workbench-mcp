namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class PluginExecutionContextFactory : IToolExecutionContextFactory
{
    private readonly IWorkspaceExecutionContextFactory _workspaceFactory;
    private readonly IToolExecutionServices _toolExecutionServices;

    public PluginExecutionContextFactory(
        IWorkspaceExecutionContextFactory workspaceFactory,
        IToolExecutionServices toolExecutionServices)
    {
        _workspaceFactory = workspaceFactory;
        _toolExecutionServices = toolExecutionServices;
    }

    public PluginMutationExecutionLease CreateMutationContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken)
    {
        var workspaceLease = _workspaceFactory.CreateMutationContext(request.Workspace, cancellationToken);
        var context = workspaceLease.Context is null
            ? null
            : new PluginMutationContext(workspaceLease.Context, _toolExecutionServices);
        return new PluginMutationExecutionLease(
            workspaceLease,
            context,
            PluginWorkspaceResultMapper.MapFailure(workspaceLease.Failure));
    }

    public ToolExecutionContextLease<IQueryContext> CreateQueryContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken)
    {
        var workspaceLease = _workspaceFactory.CreateQueryContext(request.Workspace, cancellationToken);
        var context = workspaceLease.Context is null
            ? null
            : new PluginQueryContext(workspaceLease.Context, _toolExecutionServices);

        if (workspaceLease.Failure is not null)
        {
            return ToolExecutionContextLease<IQueryContext>.Rejected(
                PluginWorkspaceResultMapper.MapFailure(workspaceLease.Failure)!,
                context,
                workspaceLease);
        }

        return ToolExecutionContextLease<IQueryContext>.Acquired(
            context ?? throw new InvalidOperationException("Workspace query acquisition completed without a context."),
            workspaceLease);
    }
}
