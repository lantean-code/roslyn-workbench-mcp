namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Identifies a query tool handler for compile-time plugin configuration.
/// </summary>
/// <remarks>
/// Plugin handlers implement the generic <see cref="IQueryToolHandler{TRequest, TResponse}"/> contract rather than
/// implementing this marker directly.
/// </remarks>
#pragma warning disable CA1040 // The non-generic interface is an intentional marker used for query-handler discovery and registration.
public interface IQueryToolHandler
{
}
#pragma warning restore CA1040

/// <summary>
/// Executes one registered query tool.
/// </summary>
/// <typeparam name="TRequest">The request contract type.</typeparam>
/// <typeparam name="TResponse">The successful response payload type.</typeparam>
/// <remarks>
/// Implementations are retained for the lifetime of the plugin catalogue and must be stateless, thread-safe, and must
/// not own disposable resources. Invocation-scoped services are available through the supplied query context.
/// </remarks>
public interface IQueryToolHandler<TRequest, TResponse> : IQueryToolHandler where TRequest : WorkspaceBoundRequest
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
