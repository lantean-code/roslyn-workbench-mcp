namespace Roslyn.Workbench.Mcp.Transactions;

/// <summary>
/// Couples one process-local receipt handle with its immutable review and expiry.
/// </summary>
internal sealed record TransactionReceipt
{
    /// <summary>
    /// Gets the opaque receipt identifier returned to the MCP client.
    /// </summary>
    public required string ReceiptId { get; init; }

    /// <summary>
    /// Gets the instant after which the receipt cannot be approved.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the immutable Workspace review represented by the receipt.
    /// </summary>
    public required TransactionReviewOutcome Review { get; init; }
}
