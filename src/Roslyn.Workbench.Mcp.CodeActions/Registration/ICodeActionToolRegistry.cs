namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Collects the strongly typed registrations for host-published Code Action tools.
/// </summary>
internal interface ICodeActionToolRegistry
{
    /// <summary>
    /// Registers a query handler and its request and response contracts.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="metadata">The metadata published for the tool.</param>
    void RegisterQueryTool<THandler, TRequest, TResponse>(CodeActionToolMetadata metadata)
        where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
        where TRequest : WorkspaceBoundRequest;

    /// <summary>
    /// Registers a mutation handler and its request contract.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="metadata">The metadata published for the tool.</param>
    void RegisterMutationTool<THandler, TRequest>(CodeActionToolMetadata metadata)
        where THandler : class, ICodeActionMutationToolHandler<TRequest>
        where TRequest : WorkspaceMutationRequest;
}
