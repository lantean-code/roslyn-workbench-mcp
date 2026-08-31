namespace Roslyn.Workbench.Mcp.Plugins.Core.Execution;

/// <summary>
/// Applies common cancellation handling before dispatching bundled mutation tools.
/// </summary>
/// <typeparam name="TRequest">The workspace mutation request accepted by the tool.</typeparam>
internal abstract class MutationToolHandler<TRequest> : IMutationToolHandler<TRequest>
    where TRequest : WorkspaceMutationRequest
{
    /// <inheritdoc/>
    public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(
        TRequest request,
        IMutationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ExecuteCoreAsync(request, context, cancellationToken);
    }

    /// <summary>
    /// Executes the tool-specific mutation after common handler checks have completed.
    /// </summary>
    /// <param name="request">The validated mutation request.</param>
    /// <param name="context">The services and workspace snapshot available to the mutation.</param>
    /// <param name="cancellationToken">The token that cancels mutation execution.</param>
    /// <returns>The candidate solution change produced by the mutation.</returns>
    protected abstract ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteCoreAsync(
        TRequest request,
        IMutationContext context,
        CancellationToken cancellationToken);
}
