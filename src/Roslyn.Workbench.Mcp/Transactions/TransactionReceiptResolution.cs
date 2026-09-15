using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Transactions;

/// <summary>
/// Represents the availability of one process-local transaction receipt.
/// </summary>
internal sealed class TransactionReceiptResolution
{
    /// <summary>
    /// Gets the receipt when it remains available.
    /// </summary>
    public TransactionReceipt? Receipt { get; }

    /// <summary>
    /// Gets the receipt availability classification.
    /// </summary>
    public TransactionReceiptResolutionStatus Status { get; }

    /// <summary>
    /// Gets whether a current receipt was resolved.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Receipt))]
    public bool IsAvailable => Receipt is not null;

    private TransactionReceiptResolution(
        TransactionReceipt? receipt,
        TransactionReceiptResolutionStatus status)
    {
        Receipt = receipt;
        Status = status;
    }

    /// <summary>
    /// Creates a resolution containing an available receipt.
    /// </summary>
    /// <param name="receipt">The current receipt.</param>
    /// <returns>An available resolution.</returns>
    public static TransactionReceiptResolution Available(TransactionReceipt receipt)
    {
        return new TransactionReceiptResolution(receipt, TransactionReceiptResolutionStatus.Available);
    }

    /// <summary>
    /// Creates a resolution for an unavailable receipt.
    /// </summary>
    /// <param name="status">The unavailable status.</param>
    /// <returns>An unavailable resolution.</returns>
    public static TransactionReceiptResolution Unavailable(TransactionReceiptResolutionStatus status)
    {
        if (status == TransactionReceiptResolutionStatus.Available)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new TransactionReceiptResolution(receipt: null, status);
    }
}
