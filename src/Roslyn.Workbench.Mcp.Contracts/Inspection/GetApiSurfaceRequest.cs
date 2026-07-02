using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to return the exported API surface for a scope.
/// </summary>
public sealed record GetApiSurfaceRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the minimum accessibility threshold as Public, Protected, or Internal.
    /// </summary>
    public string MinimumAccessibility { get; init; } = "Public";

    /// <summary>
    /// Gets a value indicating whether obsolete symbols should be included.
    /// </summary>
    public bool IncludeObsolete { get; init; } = true;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public ResultLimit? Limit { get; init; }
}
