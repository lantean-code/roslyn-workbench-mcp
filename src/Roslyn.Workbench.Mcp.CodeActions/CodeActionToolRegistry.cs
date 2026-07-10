namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionToolRegistry : ICodeActionToolRegistry
{
    private readonly List<IRegisteredCodeActionTool> _tools = [];
    private readonly HashSet<string> _toolNames = new(StringComparer.Ordinal);

    public IReadOnlyList<IRegisteredCodeActionTool> Tools => _tools;

    public void RegisterQueryTool<TRequest, TResponse>(
        CodeActionToolMetadata metadata,
        CodeActionQueryToolHandler<TRequest, TResponse> handler)
        where TRequest : WorkspaceBoundRequest
    {
        ArgumentNullException.ThrowIfNull(handler);
        Validate(metadata);
        _tools.Add(new CodeActionQueryRegistration<TRequest, TResponse>(metadata, handler));
    }

    public void RegisterMutationTool<TRequest>(
        CodeActionToolMetadata metadata,
        CodeActionMutationToolHandler<TRequest> handler)
        where TRequest : WorkspaceBoundRequest
    {
        ArgumentNullException.ThrowIfNull(handler);
        Validate(metadata);
        _tools.Add(new CodeActionMutationRegistration<TRequest>(metadata, handler));
    }

    private void Validate(CodeActionToolMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

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
