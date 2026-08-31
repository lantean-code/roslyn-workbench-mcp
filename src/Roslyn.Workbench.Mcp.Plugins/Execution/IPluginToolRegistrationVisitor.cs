namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Dispatches strongly typed plugin registrations without runtime generic inspection during tool execution.
/// </summary>
/// <typeparam name="TResult">The result produced for a registration.</typeparam>
internal interface IPluginToolRegistrationVisitor<out TResult>
{
    /// <summary>
    /// Visits a registered query handler with its request and response types preserved.
    /// </summary>
    /// <typeparam name="TRequest">The query request type.</typeparam>
    /// <typeparam name="TResponse">The query response type.</typeparam>
    /// <param name="registration">The typed query registration.</param>
    /// <returns>The visitor result.</returns>
    TResult VisitQuery<TRequest, TResponse>(PluginQueryRegistration<TRequest, TResponse> registration)
        where TRequest : WorkspaceBoundRequest
        where TResponse : IQueryResponse;

    /// <summary>
    /// Visits a registered mutation handler with its request type preserved.
    /// </summary>
    /// <typeparam name="TRequest">The mutation request type.</typeparam>
    /// <param name="registration">The typed mutation registration.</param>
    /// <returns>The visitor result.</returns>
    TResult VisitMutation<TRequest>(PluginMutationRegistration<TRequest> registration)
        where TRequest : WorkspaceMutationRequest;
}
