namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal abstract class MutationToolHandler<TRequest, TResponse> : IMutationToolHandler<TRequest, TResponse>
{
    public ValueTask<PluginExecutionResult<TResponse>> ExecuteAsync(TRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    protected abstract ValueTask<PluginExecutionResult<TResponse>> ExecuteCoreAsync(TRequest request, IMutationContext context, CancellationToken cancellationToken);
}
