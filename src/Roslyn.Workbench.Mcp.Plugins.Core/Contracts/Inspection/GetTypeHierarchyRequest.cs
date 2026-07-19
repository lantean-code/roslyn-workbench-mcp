namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve type-hierarchy information for a resolved symbol.
/// </summary>
public sealed record GetTypeHierarchyRequest : WorkspaceBoundRequest
{
    internal const int _defaultBaseTypesMaxResults = 16;
    internal const int _defaultDerivedTypesMaxResults = 100;
    internal const int _defaultInterfacesMaxResults = 64;
    internal const int _defaultMaxDepth = 3;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public SymbolSelector? Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether derived types should be included.
    /// </summary>
    public bool IncludeDerived { get; init; } = true;

    /// <summary>
    /// Gets the maximum base-type and derived-type traversal depth. Direct relationships are at depth one.
    /// </summary>
    [DefaultValue(_defaultMaxDepth)]
    public int MaxDepth { get; init; } = _defaultMaxDepth;

    /// <summary>
    /// Gets the optional base types limit.
    /// </summary>
    [DefaultValue(_defaultBaseTypesMaxResults)]
    public int? BaseTypesLimit { get; init; } = _defaultBaseTypesMaxResults;

    /// <summary>
    /// Gets the optional interfaces limit.
    /// </summary>
    [DefaultValue(_defaultInterfacesMaxResults)]
    public int? InterfacesLimit { get; init; } = _defaultInterfacesMaxResults;

    /// <summary>
    /// Gets the optional derived types limit.
    /// </summary>
    [DefaultValue(_defaultDerivedTypesMaxResults)]
    public int? DerivedTypesLimit { get; init; } = _defaultDerivedTypesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }
}
