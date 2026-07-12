namespace Roslyn.Workbench.Mcp.CodeActions;

internal abstract class CodeActionMutationToolHandler<TRequest> : ICodeActionMutationToolHandler<TRequest>
    where TRequest : WorkspaceBoundRequest
{
    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteAsync(
        TRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    protected abstract ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(
        TRequest request,
        ICodeActionMutationContext context,
        CancellationToken cancellationToken);
}
