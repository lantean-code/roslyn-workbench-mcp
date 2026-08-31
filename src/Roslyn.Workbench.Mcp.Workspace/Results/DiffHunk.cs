namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents one hunk within a document diff.
/// </summary>
public sealed record DiffHunk
{
    /// <summary>
    /// One-based starting line in the original document.
    /// </summary>
    [Description("One-based starting line in the original document.")]
    public int OriginalStartLine { get; init; }

    /// <summary>
    /// Number of original-document lines covered by the hunk.
    /// </summary>
    [Description("Number of original-document lines covered by the hunk.")]
    public int OriginalLineCount { get; init; }

    /// <summary>
    /// One-based starting line in the updated document.
    /// </summary>
    [Description("One-based starting line in the updated document.")]
    public int UpdatedStartLine { get; init; }

    /// <summary>
    /// Number of updated-document lines covered by the hunk.
    /// </summary>
    [Description("Number of updated-document lines covered by the hunk.")]
    public int UpdatedLineCount { get; init; }

    /// <summary>
    /// Unified-diff content lines prefixed with space, +, or -.
    /// </summary>
    [Description("Unified-diff content lines prefixed with space, +, or -.")]
    public IReadOnlyList<string> Lines { get; init; } = [];
}
