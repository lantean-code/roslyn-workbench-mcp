namespace Roslyn.Workbench.Mcp.CodeActions;

internal interface ICodeActionExecutionContextFactory
{
    CodeActionQueryExecutionLease CreateQueryContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken);

    CodeActionMutationExecutionLease CreateMutationContext(
        WorkspaceBoundRequest request,
        CancellationToken cancellationToken);
}
