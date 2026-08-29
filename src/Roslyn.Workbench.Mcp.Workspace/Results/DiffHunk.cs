namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents one hunk within a document diff.
/// </summary>
public sealed record DiffHunk
{
    /// <summary>
    /// Gets the starting line of the original content.
    /// </summary>
    [Description("One-based starting line in the original document.")]
    public int OriginalStartLine { get; init; }

    /// <summary>
    /// Gets the number of lines from the original content.
    /// </summary>
    [Description("Number of original-document lines covered by the hunk.")]
    public int OriginalLineCount { get; init; }

    /// <summary>
    /// Gets the starting line of the updated content.
    /// </summary>
    [Description("One-based starting line in the updated document.")]
    public int UpdatedStartLine { get; init; }

    /// <summary>
    /// Gets the number of lines from the updated content.
    /// </summary>
    [Description("Number of updated-document lines covered by the hunk.")]
    public int UpdatedLineCount { get; init; }

    /// <summary>
    /// Gets the raw hunk lines.
    /// </summary>
    [Description("Unified-diff content lines prefixed with space, +, or -.")]
    public IReadOnlyList<string> Lines { get; init; } = [];
}
