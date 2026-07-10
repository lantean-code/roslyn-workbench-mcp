using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents the structured payload returned by transaction preview.
/// </summary>
public sealed record TransactionPreviewData
{
    /// <summary>
    /// Gets the active transaction info.
    /// </summary>
    public TransactionInfo? Transaction { get; init; }

    /// <summary>
    /// Gets the changed documents in the preview.
    /// </summary>
    public IReadOnlyList<DocumentChange> Documents { get; init; } = [];

    /// <summary>
    /// Gets the optional detailed diff.
    /// </summary>
    public DocumentDiff? Diff { get; init; }
}
