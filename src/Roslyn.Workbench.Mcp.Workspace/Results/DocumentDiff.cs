namespace Roslyn.Workbench.Mcp.Workspace.Results;

/// <summary>
/// Represents a document diff returned by a preview operation.
/// </summary>
public sealed record DocumentDiff
{
    /// <summary>
    /// Gets the document for which the diff was produced.
    /// </summary>
    [Description("Document for which the detailed diff was produced.")]
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Gets the diff hunks.
    /// </summary>
    [Description("Changed regions in unified-diff form.")]
    public IReadOnlyList<DiffHunk> Hunks { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the diff was truncated.
    /// </summary>
    [Description("Whether one or more diff hunks were omitted from the response.")]
    public bool Truncated { get; init; }
}
