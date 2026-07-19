namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to return the exported API surface for a scope.
/// </summary>
public sealed record GetApiSurfaceRequest : WorkspaceBoundRequest
{
    internal const int _defaultSymbolsMaxResults = 100;

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
    [DefaultValue(_defaultSymbolsMaxResults)]
    public int? SymbolsLimit { get; init; } = _defaultSymbolsMaxResults;
}
