namespace Roslyn.Workbench.Mcp.Plugins.Core.Execution;

/// <summary>
/// Applies common cancellation handling before dispatching bundled query tools.
/// </summary>
/// <typeparam name="TRequest">The workspace-bound request accepted by the tool.</typeparam>
/// <typeparam name="TResponse">The query response returned by the tool.</typeparam>
internal abstract class QueryToolHandler<TRequest, TResponse> : IQueryToolHandler<TRequest, TResponse> where TRequest : WorkspaceBoundRequest where TResponse : IQueryResponse
{
    /// <inheritdoc/>
    public ValueTask<PluginExecutionResult<TResponse>> ExecuteAsync(TRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    /// <summary>
    /// Executes the tool-specific query after common handler checks have completed.
    /// </summary>
    /// <param name="request">The validated query request.</param>
    /// <param name="context">The services and workspace snapshot available to the query.</param>
    /// <param name="cancellationToken">The token that cancels query execution.</param>
    /// <returns>The plugin execution result produced by the query.</returns>
    protected abstract ValueTask<PluginExecutionResult<TResponse>> ExecuteCoreAsync(TRequest request, IQueryContext context, CancellationToken cancellationToken);
}
