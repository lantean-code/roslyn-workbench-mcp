using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to find overrides of a virtual or abstract member.
/// </summary>
public sealed record FindOverridesRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public CollectionLimit? OverridesLimit { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
