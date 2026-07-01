using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve project details.
/// </summary>
public sealed record GetProjectDetailsRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the project selector.
    /// </summary>
    public ProjectSelector? Project { get; init; }

    /// <summary>
    /// Gets a value indicating whether documents should be included.
    /// </summary>
    public bool IncludeDocuments { get; init; } = true;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public ResultLimit? Limit { get; init; }
}
