using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Roslyn.Workbench.Mcp.Workspace.Validation;

namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents a request to preview the current transaction.
/// </summary>
internal sealed record TransactionPreviewRequest : WorkspaceBoundRequest
{
    private const int _defaultContextLines = 3;

    /// <summary>
    /// Gets the optional document selector for a detailed diff.
    /// </summary>
    [Description("Document whose detailed diff should be returned; provide it when includeDiff is true.")]
    [RequiredWhen(nameof(IncludeDiff), true)]
    public DocumentSelector? Document { get; init; }

    /// <summary>
    /// Gets a value indicating whether to include a detailed diff.
    /// </summary>
    [Description("Whether to include a detailed diff.")]
    public bool IncludeDiff { get; init; }

    /// <summary>
    /// Gets the requested diff context line count.
    /// </summary>
    [Description("Number of unchanged context lines around each diff hunk when includeDiff is true.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultContextLines)]
    public int ContextLines { get; init; } = _defaultContextLines;
}
