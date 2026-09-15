namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Builds canonical receipt identities and bounded review projections from active transactions.
/// </summary>
internal interface ITransactionReviewBuilder
{
    /// <summary>
    /// Builds a review projection for one immutable transaction state.
    /// </summary>
    /// <param name="session">The immutable Workspace session being reviewed.</param>
    /// <param name="diffDocument">The optional document for which a bounded diff is requested.</param>
    /// <param name="contextLines">The number of unchanged lines around each diff hunk.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the review construction result.</returns>
    ValueTask<TransactionReviewBuildResult> CreateAsync(
        WorkspaceSessionSnapshot session,
        DocumentReference? diffDocument,
        int contextLines,
        CancellationToken cancellationToken);
}
