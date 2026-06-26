using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Results;

/// <summary>
/// Represents a document diff returned by a preview operation.
/// </summary>
public sealed record DocumentDiff
{
    /// <summary>
    /// Gets the document for which the diff was produced.
    /// </summary>
    public DocumentReference? Document { get; init; }

    /// <summary>
    /// Gets the diff hunks.
    /// </summary>
    public IReadOnlyList<DiffHunk> Hunks { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the diff was truncated.
    /// </summary>
    public bool Truncated { get; init; }
}
