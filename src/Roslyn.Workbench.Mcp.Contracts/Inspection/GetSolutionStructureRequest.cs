using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve solution structure.
/// </summary>
public sealed record GetSolutionStructureRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets a value indicating whether documents should be included in project projections.
    /// </summary>
    public bool IncludeDocuments { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public ResultLimit? Limit { get; init; }
}
