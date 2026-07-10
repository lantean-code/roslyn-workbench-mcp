namespace Roslyn.Workbench.Mcp.CodeActions;

internal interface ICodeActionToolRegistrationVisitor<out TResult>
{
    TResult VisitQuery<TRequest, TResponse>(CodeActionQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest;

    TResult VisitMutation<TRequest>(CodeActionMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceBoundRequest;
}
