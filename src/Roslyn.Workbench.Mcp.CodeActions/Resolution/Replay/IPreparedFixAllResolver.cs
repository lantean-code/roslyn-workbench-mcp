namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

/// <summary>
/// Reconstructs a previously prepared Fix All action from a temporary reference.
/// </summary>
internal interface IPreparedFixAllResolver
{
    /// <summary>
    /// Rediscover the source Code Fix and recreate its prepared Fix All action.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actionId">The action identifier.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the recreated Fix All action and source context, or a rejection.</returns>
    ValueTask<CodeActionResolution<T>> ResolveActionAsync<T>(
        Guid actionId,
        SnapshotPrecondition? expectedSnapshot,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
