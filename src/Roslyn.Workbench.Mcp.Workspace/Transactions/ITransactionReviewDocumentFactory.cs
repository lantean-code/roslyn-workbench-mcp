namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Projects validated commit-plan entries into canonical transaction review documents.
/// </summary>
internal interface ITransactionReviewDocumentFactory
{
    /// <summary>
    /// Creates canonical review documents from a validated commit plan.
    /// </summary>
    /// <param name="session">The immutable Workspace session being reviewed.</param>
    /// <param name="plan">The validated persistence plan.</param>
    /// <param name="changes">The optional bounded line summaries to include in the projection.</param>
    /// <param name="cancellationToken">The token used to cancel generated-source classification.</param>
    /// <returns>The review documents in canonical path order.</returns>
    ValueTask<IReadOnlyList<TransactionReviewDocument>> CreateAsync(
        WorkspaceSessionSnapshot session,
        WorkspaceCommitPlan plan,
        ChangeSummary? changes,
        CancellationToken cancellationToken);
}
