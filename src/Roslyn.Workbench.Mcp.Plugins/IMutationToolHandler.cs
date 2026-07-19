namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Identifies a mutation tool handler for compile-time plugin configuration.
/// </summary>
/// <remarks>
/// Plugin handlers implement the generic <see cref="IMutationToolHandler{TRequest}"/> contract rather than
/// implementing this marker directly.
/// </remarks>
#pragma warning disable CA1040 // The non-generic interface is an intentional marker used for mutation-handler discovery and registration.
public interface IMutationToolHandler
{
}
#pragma warning restore CA1040

/// <summary>
/// Executes one registered mutation tool.
/// </summary>
/// <typeparam name="TRequest">The request contract type.</typeparam>
/// <remarks>
/// Implementations are retained for the lifetime of the plugin catalogue and must be stateless, thread-safe, and must
/// not own disposable resources. Invocation-scoped services are available through the supplied mutation context.
/// </remarks>
public interface IMutationToolHandler<TRequest> : IMutationToolHandler
    where TRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Executes the tool for the provided request.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="context">The host-owned mutation execution context.</param>
    /// <param name="cancellationToken">The cancellation token for the invocation.</param>
    /// <returns>The normalized plugin execution outcome.</returns>
    ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(
        TRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);
}
