namespace Roslyn.Workbench.Mcp.Transactions;

/// <summary>
/// Retains at most one short-lived receipt for each loaded Workspace.
/// </summary>
internal sealed class TransactionReceiptStore : ITransactionReceiptStore
{
    private static readonly TimeSpan _receiptLifetime = TimeSpan.FromMinutes(15);

    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<Guid, TransactionReceipt> _receiptsByWorkspace = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReceiptStore"/> class.
    /// </summary>
    /// <param name="timeProvider">The provider used to measure receipt lifetime.</param>
    public TransactionReceiptStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public TransactionReceipt CreateOrGet(TransactionReviewOutcome review)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var workspaceId = review.Identity.WorkspaceId;
            if (_receiptsByWorkspace.TryGetValue(workspaceId, out var existing)
                && existing.ExpiresAt > now
                && existing.Review.Identity == review.Identity)
            {
                return existing;
            }

            var receipt = new TransactionReceipt
            {
                ReceiptId = Guid.NewGuid().ToString("n"),
                ExpiresAt = now.Add(_receiptLifetime),
                Review = review,
            };

            _receiptsByWorkspace[workspaceId] = receipt;
            return receipt;
        }
    }

    /// <inheritdoc/>
    public TransactionReceiptResolution Resolve(string receiptId)
    {
        lock (_sync)
        {
            return ResolveUnderLock(receiptId);
        }
    }

    /// <inheritdoc/>
    public TransactionReceiptResolution Consume(
        string receiptId,
        TransactionReviewIdentity expectedIdentity)
    {
        lock (_sync)
        {
            var resolution = ResolveUnderLock(receiptId);
            if (!resolution.IsAvailable)
            {
                return resolution;
            }

            if (resolution.Receipt.Review.Identity != expectedIdentity)
            {
                return TransactionReceiptResolution.Unavailable(TransactionReceiptResolutionStatus.Missing);
            }

            _receiptsByWorkspace.Remove(resolution.Receipt.Review.Identity.WorkspaceId);
            return resolution;
        }
    }

    private TransactionReceiptResolution ResolveUnderLock(string receiptId)
    {
        var receipt = _receiptsByWorkspace.Values.FirstOrDefault(item =>
            string.Equals(item.ReceiptId, receiptId, StringComparison.Ordinal));

        if (receipt is null)
        {
            return TransactionReceiptResolution.Unavailable(TransactionReceiptResolutionStatus.Missing);
        }

        if (receipt.ExpiresAt > _timeProvider.GetUtcNow())
        {
            return TransactionReceiptResolution.Available(receipt);
        }

        _receiptsByWorkspace.Remove(receipt.Review.Identity.WorkspaceId);
        return TransactionReceiptResolution.Unavailable(TransactionReceiptResolutionStatus.Expired);
    }
}
