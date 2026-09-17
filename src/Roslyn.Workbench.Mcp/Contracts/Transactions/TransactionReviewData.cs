using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents the exact persistence review bound to a short-lived receipt.
/// </summary>
internal sealed record TransactionReviewData
{
    /// <summary>
    /// Gets the opaque identifier required by receipt-aware commit.
    /// </summary>
    [Description("Opaque identifier required by transaction-commit in approval-required mode.")]
    public required string ReceiptId { get; init; }

    /// <summary>
    /// Gets the UTC receipt expiry.
    /// </summary>
    [Description("UTC instant after which this receipt cannot be approved.")]
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the canonicalisation algorithm identifier.
    /// </summary>
    [Description("Versioned canonicalisation algorithm used for the change-set digest.")]
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the canonical exact-change digest.
    /// </summary>
    [Description("Lowercase SHA-256 digest of the exact validated persistence set.")]
    public required string ChangeSetDigest { get; init; }

    /// <summary>
    /// Gets the bound Workspace identifier.
    /// </summary>
    [Description("Workspace identifier bound to this receipt.")]
    public required Guid WorkspaceId { get; init; }

    /// <summary>
    /// Gets the bound Workspace epoch.
    /// </summary>
    [Description("Workspace load epoch bound to this receipt.")]
    public required long WorkspaceEpoch { get; init; }

    /// <summary>
    /// Gets the bound snapshot identifier.
    /// </summary>
    [Description("Solution snapshot identifier bound to this receipt.")]
    public required Guid SnapshotId { get; init; }

    /// <summary>
    /// Gets the bound transaction revision.
    /// </summary>
    [Description("Transaction revision bound to this receipt.")]
    public required int TransactionRevision { get; init; }

    /// <summary>
    /// Gets the number of created files.
    /// </summary>
    [Description("Number of files created by the reviewed change.")]
    public required int AddedDocumentCount { get; init; }

    /// <summary>
    /// Gets the number of replaced files.
    /// </summary>
    [Description("Number of files replaced by the reviewed change.")]
    public required int ModifiedDocumentCount { get; init; }

    /// <summary>
    /// Gets the number of deleted files.
    /// </summary>
    [Description("Number of files deleted by the reviewed change.")]
    public required int DeletedDocumentCount { get; init; }

    /// <summary>
    /// Gets the active transaction information.
    /// </summary>
    [Description("The active transaction info.")]
    public required TransactionInfo Transaction { get; init; }

    /// <summary>
    /// Gets the canonical reviewed file operations.
    /// </summary>
    [Description("Validated file operations in canonical path order.")]
    public IReadOnlyList<TransactionReviewDocument> Documents { get; init; } = [];

    /// <summary>
    /// Gets the mutation provenance for the current revision.
    /// </summary>
    [Description("Mutation revisions contributing to the reviewed transaction.")]
    public IReadOnlyList<TransactionMutationProvenanceData> Provenance { get; init; } = [];

    /// <summary>
    /// Gets provider identities keyed by response-local identifiers used by provenance entries.
    /// </summary>
    [Description("Provider identities keyed by response-local references used by provenance entries.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, MutationProviderIdentity>? Providers { get; init; }

    /// <summary>
    /// Gets the completed review validations.
    /// </summary>
    [Description("Validations completed while producing this receipt.")]
    public IReadOnlyList<TransactionReviewValidation> Validations { get; init; } = [];

    /// <summary>
    /// Gets the optional explicitly requested document diff.
    /// </summary>
    [Description("Optional detailed diff for the explicitly selected document.")]
    public DocumentDiff? Diff { get; init; }

    /// <summary>
    /// Gets the structured continuation to the receipt-aware commit operation.
    /// </summary>
    [Description("Structured next step for committing this exact reviewed transaction state.")]
    public required ToolContinuation Continuation { get; init; }
}
