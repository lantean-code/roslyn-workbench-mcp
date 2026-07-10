using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Records the tools exposed by one plugin entry point during startup.
/// </summary>
/// <remarks>
/// The registry retains each supplied handler for the lifetime of the plugin catalogue. Handlers must be stateless,
/// thread-safe, and must not own disposable resources. Invocation-scoped services are available through the execution
/// context supplied to the handler.
/// </remarks>
public interface IPluginRegistry
{
    /// <summary>
    /// Registers a query tool.
    /// </summary>
    /// <typeparam name="TRequest">The request contract type.</typeparam>
    /// <typeparam name="TResponse">The successful response payload type.</typeparam>
    /// <param name="metadata">The tool metadata.</param>
    /// <param name="handler">The typed query handler retained for the lifetime of the plugin catalogue.</param>
    void RegisterQueryTool<TRequest, TResponse>(ToolRegistrationMetadata metadata, IQueryToolHandler<TRequest, TResponse> handler)
        where TRequest : WorkspaceBoundRequest;

    /// <summary>
    /// Registers a mutation tool.
    /// </summary>
    /// <typeparam name="TRequest">The request contract type.</typeparam>
    /// <param name="metadata">The tool metadata.</param>
    /// <param name="handler">The typed mutation handler retained for the lifetime of the plugin catalogue.</param>
    void RegisterMutationTool<TRequest>(
        ToolRegistrationMetadata metadata,
        IMutationToolHandler<TRequest> handler)
        where TRequest : WorkspaceBoundRequest;
}
