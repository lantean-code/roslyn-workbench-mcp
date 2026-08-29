namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents the structured payload returned by transaction preview.
/// </summary>
internal sealed record TransactionPreviewData
{
    /// <summary>
    /// Gets the active transaction info.
    /// </summary>
    [Description("The active transaction info.")]
    public TransactionInfo? Transaction { get; init; }

    /// <summary>
    /// Gets the changed documents in the preview.
    /// </summary>
    [Description("The changed documents in the preview.")]
    public IReadOnlyList<DocumentChange> Documents { get; init; } = [];

    /// <summary>
    /// Gets the optional detailed diff.
    /// </summary>
    [Description("The optional detailed diff.")]
    public DocumentDiff? Diff { get; init; }
}
