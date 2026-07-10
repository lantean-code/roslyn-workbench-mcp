using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Executes one registered query tool.
/// </summary>
/// <typeparam name="TRequest">The request contract type.</typeparam>
/// <typeparam name="TResponse">The successful response payload type.</typeparam>
/// <remarks>
/// Implementations are retained for the lifetime of the plugin catalogue and must be stateless, thread-safe, and must
/// not own disposable resources. Invocation-scoped services are available through the supplied query context.
/// </remarks>
public interface IQueryToolHandler<TRequest, TResponse> where TRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Executes the tool for the provided request.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="context">The host-owned query execution context.</param>
    /// <param name="cancellationToken">The cancellation token for the invocation.</param>
    /// <returns>The normalized plugin execution outcome.</returns>
    ValueTask<PluginExecutionResult<TResponse>> ExecuteAsync(TRequest request, IQueryContext context, CancellationToken cancellationToken);
}
