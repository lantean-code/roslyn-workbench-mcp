namespace Roslyn.Workbench.Mcp.Plugins.Core.Execution;

internal abstract class QueryToolHandler<TRequest, TResponse> : IQueryToolHandler<TRequest, TResponse> where TRequest : WorkspaceBoundRequest where TResponse : IQueryResponse
{
    public ValueTask<PluginExecutionResult<TResponse>> ExecuteAsync(TRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    protected abstract ValueTask<PluginExecutionResult<TResponse>> ExecuteCoreAsync(TRequest request, IQueryContext context, CancellationToken cancellationToken);
}
