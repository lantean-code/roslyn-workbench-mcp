namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Produces a value from a strongly typed Code Action tool registration.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
internal interface ICodeActionToolRegistrationVisitor<out TResult>
{
    /// <summary>
    /// Visits a query-tool registration.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="registration">The query registration to visit.</param>
    /// <returns>The value produced for the query registration.</returns>
    TResult VisitQuery<THandler, TRequest, TResponse>(CodeActionQueryRegistration<THandler, TRequest, TResponse> registration)
        where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
        where TRequest : WorkspaceBoundRequest;

    /// <summary>
    /// Visits a mutation-tool registration.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="registration">The mutation registration to visit.</param>
    /// <returns>The value produced for the mutation registration.</returns>
    TResult VisitMutation<THandler, TRequest>(CodeActionMutationRegistration<THandler, TRequest> registration)
        where THandler : class, ICodeActionMutationToolHandler<TRequest>
        where TRequest : WorkspaceMutationRequest;
}
