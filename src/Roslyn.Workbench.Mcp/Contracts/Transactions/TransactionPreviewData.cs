using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents the structured payload returned by transaction preview.
/// </summary>
internal sealed record TransactionPreviewData
{
    /// <summary>
    /// The active transaction info.
    /// </summary>
    [Description("The active transaction info.")]
    public TransactionInfo? Transaction { get; init; }

    /// <summary>
    /// The changed documents in the preview.
    /// </summary>
    [Description("The changed documents in the preview.")]
    public IReadOnlyList<DocumentChange> Documents { get; init; } = [];

    /// <summary>
    /// The mutation revisions contributing to the previewed transaction.
    /// </summary>
    [Description("Mutation revisions contributing to the previewed transaction.")]
    public IReadOnlyList<TransactionMutationProvenanceData> Provenance { get; init; } = [];

    /// <summary>
    /// Gets provider identities keyed by response-local identifiers used by provenance entries.
    /// </summary>
    [Description("Provider identities keyed by response-local references used by provenance entries.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, MutationProviderIdentity>? Providers { get; init; }

    /// <summary>
    /// The optional detailed diff.
    /// </summary>
    [Description("The optional detailed diff.")]
    public DocumentDiff? Diff { get; init; }
}
