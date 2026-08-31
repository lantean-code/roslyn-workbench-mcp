namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents a document diff returned by a preview operation.
/// </summary>
public sealed record DocumentDiff
{
    /// <summary>
    /// Document for which the detailed diff was produced.
    /// </summary>
    [Description("Document for which the detailed diff was produced.")]
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Changed regions in unified-diff form.
    /// </summary>
    [Description("Changed regions in unified-diff form.")]
    public IReadOnlyList<DiffHunk> Hunks { get; init; } = [];

    /// <summary>
    /// Whether one or more diff hunks were omitted from the response.
    /// </summary>
    [Description("Whether one or more diff hunks were omitted from the response.")]
    public bool Truncated { get; init; }
}
