namespace Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

/// <summary>
/// Represents one hunk within a document diff.
/// </summary>
public sealed record DiffHunk
{
    /// <summary>
    /// Gets the starting line of the original content.
    /// </summary>
    public int OriginalStartLine { get; init; }

    /// <summary>
    /// Gets the number of lines from the original content.
    /// </summary>
    public int OriginalLineCount { get; init; }

    /// <summary>
    /// Gets the starting line of the updated content.
    /// </summary>
    public int UpdatedStartLine { get; init; }

    /// <summary>
    /// Gets the number of lines from the updated content.
    /// </summary>
    public int UpdatedLineCount { get; init; }

    /// <summary>
    /// Gets the raw hunk lines.
    /// </summary>
    public IReadOnlyList<string> Lines { get; init; } = [];
}
