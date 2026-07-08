using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal abstract class MutationToolHandler<TRequest> : IMutationToolHandler<TRequest>
    where TRequest : WorkspaceBoundRequest
{
    public ValueTask<PluginExecutionResult<MutationProposal>> ExecuteAsync(
        TRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    protected abstract ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(
        TRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);
}
