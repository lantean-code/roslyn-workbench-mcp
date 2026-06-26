namespace Roslyn.Workbench.Mcp.Contracts.Results;

/// <summary>
/// Represents a compact summary of line changes in a document diff.
/// </summary>
public sealed record DiffSummary
{
    /// <summary>
    /// Gets the number of added lines.
    /// </summary>
    public int AddedLines { get; init; }

    /// <summary>
    /// Gets the number of removed lines.
    /// </summary>
    public int RemovedLines { get; init; }

    /// <summary>
    /// Gets the number of changed lines.
    /// </summary>
    public int ChangedLines { get; init; }
}
