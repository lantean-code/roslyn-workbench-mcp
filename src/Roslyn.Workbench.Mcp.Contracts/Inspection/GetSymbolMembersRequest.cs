using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve members for a resolved symbol.
/// </summary>
public sealed record GetSymbolMembersRequest : WorkspaceBoundRequest
{
    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether inherited members should be included.
    /// </summary>
    public bool IncludeInherited { get; init; }

    /// <summary>
    /// Gets a value indicating whether explicit interface members should be included.
    /// </summary>
    public bool IncludeExplicitInterface { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    public CollectionLimit? MembersLimit { get; init; }

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
