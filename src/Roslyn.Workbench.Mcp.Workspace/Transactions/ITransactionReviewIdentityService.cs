namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Creates the canonical persistence identity for a reviewed transaction state.
/// </summary>
internal interface ITransactionReviewIdentityService
{
    /// <summary>
    /// Creates an identity from canonical review documents and the active transaction binding.
    /// </summary>
    /// <param name="session">The immutable Workspace session being reviewed.</param>
    /// <param name="documents">The canonical reviewed file operations.</param>
    /// <returns>The canonical change-set digest and transaction binding.</returns>
    TransactionReviewIdentity Create(
        WorkspaceSessionSnapshot session,
        IReadOnlyList<TransactionReviewDocument> documents);

    /// <summary>
    /// Determines whether an identity is bound to the supplied active transaction state.
    /// </summary>
    /// <param name="identity">The reviewed identity to compare.</param>
    /// <param name="session">The current immutable Workspace session.</param>
    /// <param name="transaction">The current active transaction.</param>
    /// <returns><see langword="true"/> when every transaction-binding component matches; otherwise, <see langword="false"/>.</returns>
    bool IsBoundTo(
        TransactionReviewIdentity identity,
        WorkspaceSessionSnapshot session,
        WorkspaceTransaction transaction);
}
