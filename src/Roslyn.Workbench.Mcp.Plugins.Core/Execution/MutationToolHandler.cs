namespace Roslyn.Workbench.Mcp.Plugins.Core.Execution;

internal abstract class MutationToolHandler<TRequest> : IMutationToolHandler<TRequest>
    where TRequest : WorkspaceBoundRequest
{
    public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(
        TRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    protected abstract ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteCoreAsync(
        TRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);
}
