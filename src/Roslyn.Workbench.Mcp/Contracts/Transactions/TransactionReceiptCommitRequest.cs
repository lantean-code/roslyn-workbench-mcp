using System.ComponentModel.DataAnnotations;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents a request to approve and commit one exact transaction review receipt.
/// </summary>
internal sealed record TransactionReceiptCommitRequest : WorkspaceMutationRequest
{
    /// <summary>
    /// The opaque receipt identifier returned by transaction-review.
    /// </summary>
    [Description("The opaque receipt identifier returned by transaction-review.")]
    [MinLength(1)]
    public required string ReceiptId { get; init; }
}
