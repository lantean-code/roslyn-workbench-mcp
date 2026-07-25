namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

internal sealed class CodeActionToolRegistry : ICodeActionToolRegistry
{
    private readonly List<IRegisteredCodeActionTool> _tools = [];
    private readonly HashSet<string> _toolNames = new(StringComparer.Ordinal);

    public IReadOnlyList<IRegisteredCodeActionTool> Tools => _tools;

    public void RegisterQueryTool<THandler, TRequest, TResponse>(CodeActionToolMetadata metadata)
        where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
        where TRequest : WorkspaceBoundRequest
    {
        Validate(metadata);
        _tools.Add(new CodeActionQueryRegistration<THandler, TRequest, TResponse>(metadata));
    }

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
