namespace Roslyn.Workbench.Mcp.CodeActions;

internal interface ICodeActionToolRegistry
{
    void RegisterQueryTool<TRequest, TResponse>(
        CodeActionToolMetadata metadata,
        CodeActionQueryToolHandler<TRequest, TResponse> handler)
        where TRequest : WorkspaceBoundRequest;

    void RegisterMutationTool<TRequest>(
        CodeActionToolMetadata metadata,
        CodeActionMutationToolHandler<TRequest> handler)
        where TRequest : WorkspaceBoundRequest;
}
