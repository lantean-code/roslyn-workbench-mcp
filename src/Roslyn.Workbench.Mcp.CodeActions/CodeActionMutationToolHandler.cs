using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.CodeActions;

internal abstract class CodeActionMutationToolHandler<TRequest> : IMutationToolHandler<TRequest>
    where TRequest : WorkspaceBoundRequest
{
    public ValueTask<PluginExecutionResult<MutationProposal>> ExecuteAsync(
        TRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        if (context is not ICodeActionMutationContext codeActionContext)
        {
            throw new InvalidOperationException("Mutation context does not support code-action execution.");
        }

        return ExecuteCoreAsync(request, codeActionContext, cancellationToken);
    }

    protected abstract ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(
        TRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);
}
