namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Carries one consumed, protocol-neutral approval for an exact transaction review identity.
/// </summary>
internal sealed record TransactionReceiptAuthorisation
{
    /// <summary>
    /// Gets the approved transaction review identity.
    /// </summary>
    public required TransactionReviewIdentity Identity { get; init; }
}
