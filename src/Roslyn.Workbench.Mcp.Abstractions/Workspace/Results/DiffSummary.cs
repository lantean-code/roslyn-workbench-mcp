namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents a compact summary of line changes in a document diff.
/// </summary>
public sealed record DiffSummary
{
    /// <summary>
    /// Gets the number of added lines.
    /// </summary>
    [Description("The number of added lines.")]
    public int AddedLines { get; init; }

    /// <summary>
    /// Gets the number of removed lines.
    /// </summary>
    [Description("The number of removed lines.")]
    public int RemovedLines { get; init; }

    /// <summary>
    /// Gets the number of changed lines.
    /// </summary>
    [Description("The number of changed lines.")]
    public int ChangedLines { get; init; }
}
