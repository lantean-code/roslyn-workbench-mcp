namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Contexts;

/// <summary>
/// Acquires workspace-scoped execution leases for Code Action tools.
/// </summary>
internal interface ICodeActionExecutionContextFactory
{
    /// <summary>
    /// Acquires a read-only context for a workspace-bound request.
    /// </summary>
    /// <param name="request">The request identifying the target workspace.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An acquired query lease or a normalized workspace failure.</returns>
    CodeActionQueryExecutionLease CreateQueryContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Acquires a transaction-scoped context for a mutation request.
    /// </summary>
    /// <param name="request">The request identifying the workspace and expected snapshot.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>An acquired mutation lease or a normalized workspace failure.</returns>
    CodeActionMutationExecutionLease CreateMutationContext(
        WorkspaceMutationRequest request,
        CancellationToken cancellationToken);
}
