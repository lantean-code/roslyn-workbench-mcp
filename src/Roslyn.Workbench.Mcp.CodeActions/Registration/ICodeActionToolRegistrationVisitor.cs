namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

internal interface ICodeActionToolRegistrationVisitor<out TResult>
{
    TResult VisitQuery<THandler, TRequest, TResponse>(CodeActionQueryRegistration<THandler, TRequest, TResponse> registration)
        where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
        where TRequest : WorkspaceBoundRequest;

    TResult VisitMutation<THandler, TRequest>(CodeActionMutationRegistration<THandler, TRequest> registration)
        where THandler : class, ICodeActionMutationToolHandler<TRequest>
        where TRequest : WorkspaceBoundRequest;
}
