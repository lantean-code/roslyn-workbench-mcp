namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Replay;

/// <summary>
/// Reconstructs Code Actions from temporary references against the current workspace snapshot.
/// </summary>
internal interface ICodeActionResolver
{
    /// <summary>
    /// Rediscover the uniquely identified Code Action represented by a temporary reference.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="actionId">The action identifier.</param>
    /// <param name="expectedSnapshot">The snapshot precondition that the operation must satisfy.</param>
    /// <param name="context">The execution context that supplies the state and services required by the operation.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the rediscovered action and source context, or a rejection.</returns>
    ValueTask<CodeActionResolution<T>> ResolveActionAsync<T>(
        Guid actionId,
        SnapshotPrecondition? expectedSnapshot,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken);
}
