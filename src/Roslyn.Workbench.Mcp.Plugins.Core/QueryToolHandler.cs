using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal abstract class QueryToolHandler<TRequest, TResponse> : IQueryToolHandler<TRequest, TResponse> where TRequest : WorkspaceBoundRequest
{
    public ValueTask<PluginExecutionResult<TResponse>> ExecuteAsync(TRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    protected abstract ValueTask<PluginExecutionResult<TResponse>> ExecuteCoreAsync(TRequest request, IQueryContext context, CancellationToken cancellationToken);
}
