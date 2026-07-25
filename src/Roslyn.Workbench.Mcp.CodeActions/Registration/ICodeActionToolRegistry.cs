namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

internal interface ICodeActionToolRegistry
{
    void RegisterQueryTool<THandler, TRequest, TResponse>(CodeActionToolMetadata metadata)
        where THandler : class, ICodeActionQueryToolHandler<TRequest, TResponse>
        where TRequest : WorkspaceBoundRequest;

    void RegisterMutationTool<THandler, TRequest>(CodeActionToolMetadata metadata)
        where THandler : class, ICodeActionMutationToolHandler<TRequest>
        where TRequest : WorkspaceMutationRequest;
}
