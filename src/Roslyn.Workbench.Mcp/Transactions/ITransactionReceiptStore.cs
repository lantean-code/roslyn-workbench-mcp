namespace Roslyn.Workbench.Mcp.Transactions;

/// <summary>
/// Owns bounded process-local transaction review receipts and one-use consumption.
/// </summary>
internal interface ITransactionReceiptStore
{
    /// <summary>
    /// Creates or reuses the current receipt for an immutable transaction review.
    /// </summary>
    /// <param name="review">The completed Workspace review.</param>
    /// <returns>The current process-local receipt.</returns>
    TransactionReceipt CreateOrGet(TransactionReviewOutcome review);

    /// <summary>
    /// Resolves an unexpired receipt without consuming it.
    /// </summary>
    /// <param name="receiptId">The opaque receipt identifier.</param>
    /// <returns>The current receipt resolution.</returns>
    TransactionReceiptResolution Resolve(string receiptId);

    /// <summary>
    /// Atomically consumes a current receipt when it still has the expected identity.
    /// </summary>
    /// <param name="receiptId">The opaque receipt identifier.</param>
    /// <param name="expectedIdentity">The identity observed before approval was requested.</param>
    /// <returns>The consumed receipt resolution.</returns>
    TransactionReceiptResolution Consume(
        string receiptId,
        TransactionReviewIdentity expectedIdentity);
}
