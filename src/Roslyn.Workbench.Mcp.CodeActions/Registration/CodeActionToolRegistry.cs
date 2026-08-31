namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

/// <summary>
/// Collects Code Action tool registrations and rejects invalid or duplicate metadata.
/// </summary>
internal sealed class CodeActionToolRegistry : ICodeActionToolRegistry
{
    private readonly List<IRegisteredCodeActionTool> _tools = [];
    private readonly HashSet<string> _toolNames = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the tools in registration order.
    /// </summary>
    public IReadOnlyList<IRegisteredCodeActionTool> Tools => _tools;

    /// <summary>
    /// Registers a query handler and its request and response contracts.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="metadata">The metadata published for the tool.</param>
    public void RegisterQueryTool<THandler, TRequest, TResponse>(CodeActionToolMetadata metadata)
        where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
        where TRequest : WorkspaceBoundRequest
    {
        Validate(metadata);
        _tools.Add(new CodeActionQueryRegistration<THandler, TRequest, TResponse>(metadata));
    }

    /// <summary>
    /// Registers a mutation handler and its request contract.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="metadata">The metadata published for the tool.</param>
    public void RegisterMutationTool<THandler, TRequest>(CodeActionToolMetadata metadata)
        where THandler : class, ICodeActionMutationToolHandler<TRequest>
        where TRequest : WorkspaceMutationRequest
    {
        Validate(metadata);
        _tools.Add(new CodeActionMutationRegistration<THandler, TRequest>(metadata));
    }

    private void Validate(CodeActionToolMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.Name)
            || string.IsNullOrWhiteSpace(metadata.Title)
            || string.IsNullOrWhiteSpace(metadata.Description))
        {
            throw new InvalidOperationException("Code Action tool metadata must provide Name, Title, and Description.");
        }

        if (!_toolNames.Add(metadata.Name))
        {
            throw new InvalidOperationException($"Code Action tool name '{metadata.Name}' is already registered.");
        }
    }
}
