namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Executes one registered mutation tool.
/// </summary>
/// <typeparam name="TRequest">The request contract type.</typeparam>
/// <typeparam name="TResponse">The successful response payload type.</typeparam>
public interface IMutationToolHandler<TRequest, TResponse>
{
    /// <summary>
    /// Executes the tool for the provided request.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="context">The host-owned mutation execution context.</param>
    /// <param name="cancellationToken">The cancellation token for the invocation.</param>
    /// <returns>The normalized plugin execution outcome.</returns>
    ValueTask<PluginExecutionResult<TResponse>> ExecuteAsync(TRequest request, IMutationContext context, CancellationToken cancellationToken);
}
