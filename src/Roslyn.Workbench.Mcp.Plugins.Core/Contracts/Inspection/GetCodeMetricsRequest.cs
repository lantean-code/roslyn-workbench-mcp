using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to return code metrics for a scope or symbol.
/// </summary>
public sealed record GetCodeMetricsRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the scope to inspect.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether children should be included when a type symbol is selected.
    /// </summary>
    public bool IncludeChildren { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public CollectionLimit? MetricsLimit { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
