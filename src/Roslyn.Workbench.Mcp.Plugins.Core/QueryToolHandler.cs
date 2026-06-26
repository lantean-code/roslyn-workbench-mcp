using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal abstract class QueryToolHandler<TRequest, TResponse> : IQueryToolHandler<TRequest, TResponse>
{
    public ValueTask<PluginExecutionResult<TResponse>> ExecuteAsync(TRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    protected abstract ValueTask<PluginExecutionResult<TResponse>> ExecuteCoreAsync(TRequest request, IQueryContext context, CancellationToken cancellationToken);
}
