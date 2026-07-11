namespace Roslyn.Workbench.Mcp.CodeActions;

internal abstract class CodeActionMutationToolHandler<TRequest> : ICodeActionMutationToolHandler<TRequest>
    where TRequest : WorkspaceBoundRequest
{
    public ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteAsync(
        TRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    protected abstract ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteCoreAsync(
        TRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);
}
