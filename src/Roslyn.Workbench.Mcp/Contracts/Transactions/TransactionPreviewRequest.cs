namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents a request to preview the current transaction.
/// </summary>
internal sealed record TransactionPreviewRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the optional document selector for a detailed diff.
    /// </summary>
    public DocumentSelector? Document { get; init; }

    /// <summary>
    /// Gets a value indicating whether to include a detailed diff.
    /// </summary>
    public bool IncludeDiff { get; init; }

    /// <summary>
    /// Gets the requested diff context line count.
    /// </summary>
    public int ContextLines { get; init; } = 3;
}
