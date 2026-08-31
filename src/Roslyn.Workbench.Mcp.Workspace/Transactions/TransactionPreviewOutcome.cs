namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Describes transaction state and optional document details returned by a preview.
/// </summary>
internal sealed record TransactionPreviewOutcome
{
    /// <summary>
    /// Gets the current transaction state.
    /// </summary>
    public TransactionInfo Transaction { get; init; } = new();

    /// <summary>
    /// Gets the changed documents included in the preview.
    /// </summary>
    public IReadOnlyList<DocumentChange> Documents { get; init; } = [];

    /// <summary>
    /// Gets the detailed diff for the requested document, when requested.
    /// </summary>
    public DocumentDiff? Diff { get; init; }
}
