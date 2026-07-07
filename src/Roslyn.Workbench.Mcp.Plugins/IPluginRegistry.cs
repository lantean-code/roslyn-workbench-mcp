using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Records the tools exposed by one plugin entry point during startup.
/// </summary>
public interface IPluginRegistry
{
    /// <summary>
    /// Registers a query tool.
    /// </summary>
    /// <typeparam name="TRequest">The request contract type.</typeparam>
    /// <typeparam name="TResponse">The successful response payload type.</typeparam>
    /// <param name="metadata">The tool metadata.</param>
    /// <param name="handler">The typed query handler.</param>
    void RegisterQueryTool<TRequest, TResponse>(ToolRegistrationMetadata metadata, IQueryToolHandler<TRequest, TResponse> handler)
        where TRequest : WorkspaceBoundRequest;

    /// <summary>
    /// Registers a mutation tool.
    /// </summary>
    /// <typeparam name="TRequest">The request contract type.</typeparam>
    /// <param name="metadata">The tool metadata.</param>
    /// <param name="handler">The typed mutation handler.</param>
    void RegisterMutationTool<TRequest>(
        ToolRegistrationMetadata metadata,
        IMutationToolHandler<TRequest> handler)
        where TRequest : WorkspaceBoundRequest;
}
