using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class PluginExecutionContextFactory : IToolExecutionContextFactory
{
    private readonly IWorkspaceExecutionContextFactory _workspaceFactory;
    private readonly IToolExecutionServices _toolExecutionServices;
    private readonly IQueryResultCacheScopeFactory _queryCacheScopeFactory;

    public PluginExecutionContextFactory(
        IWorkspaceExecutionContextFactory workspaceFactory,
        IToolExecutionServices toolExecutionServices,
        IQueryResultCacheScopeFactory queryCacheScopeFactory)
    {
        _workspaceFactory = workspaceFactory;
        _toolExecutionServices = toolExecutionServices;
        _queryCacheScopeFactory = queryCacheScopeFactory;
    }

    public PluginMutationExecutionLease CreateMutationContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken)
    {
        var workspaceLease = _workspaceFactory.CreateMutationContext(request.Workspace, cancellationToken);
        if (workspaceLease.HasFailure)
        {
            var context = workspaceLease.Context is null
                ? null
                : new PluginMutationContext(workspaceLease.Context, _toolExecutionServices);

            var failure = PluginWorkspaceResultMapper.MapFailure(workspaceLease.Failure);
            var result = PluginMutationExecutionLease.Rejected(
                workspaceLease,
                failure,
                context);

            return result;
        }

        var mutationContext = new PluginMutationContext(
            workspaceLease.Context,
            _toolExecutionServices);

        var acquiredResult = PluginMutationExecutionLease.Acquired(
            workspaceLease,
            mutationContext);

        return acquiredResult;
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Ownership of each composite lease is transferred to the returned ToolExecutionContextLease.")]
    public ToolExecutionContextLease<IQueryContext> CreateQueryContext(
        WorkspaceBoundRequest request,
        string pluginId,
        string toolName,
        CancellationToken cancellationToken)
    {
        var workspaceLease = _workspaceFactory.CreateQueryContext(request.Workspace, cancellationToken);
        if (workspaceLease.HasFailure)
        {
            PluginQueryContext? context = null;
            IAsyncDisposable lease = workspaceLease;
            if (workspaceLease.Context is not null)
            {
                var cacheScope = _queryCacheScopeFactory.CreateScope(
                    workspaceLease.Context.SnapshotIdentity,
                    pluginId,
                    toolName);

                context = new PluginQueryContext(
                    workspaceLease.Context,
                    _toolExecutionServices,
                    cacheScope);

                lease = new PluginQueryExecutionLease(cacheScope, workspaceLease);
            }

            var failure = PluginWorkspaceResultMapper.MapFailure(workspaceLease.Failure);
            var rejectedResult = ToolExecutionContextLease.Rejected<IQueryContext>(
                failure,
                context,
                lease);

            return rejectedResult;
        }

        var acquiredCacheScope = _queryCacheScopeFactory.CreateScope(
            workspaceLease.Context.SnapshotIdentity,
            pluginId,
            toolName);

        var queryContext = new PluginQueryContext(
            workspaceLease.Context,
            _toolExecutionServices,
            acquiredCacheScope);

        var queryLease = new PluginQueryExecutionLease(
            acquiredCacheScope,
            workspaceLease);

        var acquiredResult = ToolExecutionContextLease.Acquired<IQueryContext>(
            queryContext,
            queryLease);

        return acquiredResult;
    }

    public ToolExecutionContextLease<IQueryContext> CreateQueryContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken)
    {
        return CreateQueryContext(
            request,
            "integration-test-plugin",
            "integration-test-tool",
            cancellationToken);
    }

    public ToolExecutionFailureResult? DetectUnexpectedWorkspaceChange(IToolExecutionContext context)
    {
        var failure = _workspaceFactory.DetectUnexpectedWorkspaceChange(context.WorkspaceIdentity.WorkspaceId);
        return failure is null
            ? null
            : PluginWorkspaceResultMapper.MapFailure(failure);
    }
}
