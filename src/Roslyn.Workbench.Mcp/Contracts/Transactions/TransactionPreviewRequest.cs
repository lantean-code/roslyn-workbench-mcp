using System.ComponentModel;

namespace Roslyn.Workbench.Mcp.Transaction.Contracts;

/// <summary>
/// Represents a request to preview the current transaction.
/// </summary>
internal sealed record TransactionPreviewRequest : WorkspaceBoundRequest
{
    private const int _defaultContextLines = 3;

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
    [DefaultValue(_defaultContextLines)]
    public int ContextLines { get; init; } = _defaultContextLines;
}
