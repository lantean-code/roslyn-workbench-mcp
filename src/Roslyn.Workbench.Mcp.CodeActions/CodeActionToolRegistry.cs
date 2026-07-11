namespace Roslyn.Workbench.Mcp.CodeActions;

internal sealed class CodeActionToolRegistry : ICodeActionToolRegistry
{
    private readonly List<IRegisteredCodeActionTool> _tools = [];
    private readonly HashSet<string> _toolNames = new(StringComparer.Ordinal);

    public IReadOnlyList<IRegisteredCodeActionTool> Tools => _tools;

    public void RegisterQueryTool<TRequest, TResponse>(
        CodeActionToolMetadata metadata,
        ICodeActionQueryToolHandler<TRequest, TResponse> handler)
        where TRequest : WorkspaceBoundRequest
    {
        Validate(metadata);
        _tools.Add(new CodeActionQueryRegistration<TRequest, TResponse>(metadata, handler));
    }

    public void RegisterMutationTool<TRequest>(
        CodeActionToolMetadata metadata,
        ICodeActionMutationToolHandler<TRequest> handler)
        where TRequest : WorkspaceBoundRequest
    {
        Validate(metadata);
        _tools.Add(new CodeActionMutationRegistration<TRequest>(metadata, handler));
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
