namespace Roslyn.Workbench.Mcp.CodeActions;

internal interface ICodeActionToolRegistry
{
    void RegisterQueryTool<TRequest, TResponse>(
        CodeActionToolMetadata metadata,
        ICodeActionQueryToolHandler<TRequest, TResponse> handler)
        where TRequest : WorkspaceBoundRequest;

    void RegisterMutationTool<TRequest>(
        CodeActionToolMetadata metadata,
        ICodeActionMutationToolHandler<TRequest> handler)
        where TRequest : WorkspaceBoundRequest;
}
