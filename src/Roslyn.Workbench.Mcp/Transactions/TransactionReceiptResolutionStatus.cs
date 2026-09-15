namespace Roslyn.Workbench.Mcp.Transactions;

/// <summary>
/// Classifies the current availability of a process-local transaction receipt.
/// </summary>
internal enum TransactionReceiptResolutionStatus
{
    /// <summary>
    /// The receipt is current and available for approval.
    /// </summary>
    Available = 0,

    /// <summary>
    /// The receipt does not exist or has already been consumed.
    /// </summary>
    Missing = 1,

    /// <summary>
    /// The receipt existed but its review lifetime elapsed.
    /// </summary>
    Expired = 2,
}
