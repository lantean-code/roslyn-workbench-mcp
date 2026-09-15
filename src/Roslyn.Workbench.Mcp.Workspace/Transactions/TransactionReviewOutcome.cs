namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Describes the canonical identity and bounded review projection for an active transaction.
/// </summary>
internal sealed record TransactionReviewOutcome
{
    /// <summary>
    /// Gets the exact reviewed transaction identity.
    /// </summary>
    public required TransactionReviewIdentity Identity { get; init; }

    /// <summary>
    /// Gets the current transaction state.
    /// </summary>
    public required TransactionInfo Transaction { get; init; }

    /// <summary>
    /// Gets the validated file operations in canonical path order.
    /// </summary>
    public IReadOnlyList<TransactionReviewDocument> Documents { get; init; } = [];

    /// <summary>
    /// Gets mutation revisions contributing to the reviewed transaction state.
    /// </summary>
    public IReadOnlyList<TransactionMutationProvenance> Provenance { get; init; } = [];

    /// <summary>
    /// Gets validations completed while constructing the receipt.
    /// </summary>
    public IReadOnlyList<TransactionReviewValidation> Validations { get; init; } = [];

    /// <summary>
    /// Gets the explicitly requested bounded source diff when present.
    /// </summary>
    public DocumentDiff? Diff { get; init; }
}
