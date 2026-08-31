namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve type-hierarchy information for a resolved symbol.
/// </summary>
internal sealed record GetTypeHierarchyRequest : WorkspaceBoundRequest
{
    private const int _defaultBaseTypesMaxResults = 16;
    private const int _defaultDerivedTypesMaxResults = 100;
    private const int _defaultInterfacesMaxResults = 64;
    private const int _defaultMaxDepth = 3;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    [Description("The symbol selector.")]
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether derived types should be included.
    /// </summary>
    [Description("Whether derived types should be included.")]
    public bool IncludeDerived { get; init; } = true;

    /// <summary>
    /// Gets the maximum base-type and derived-type traversal depth. Direct relationships are at depth one.
    /// </summary>
    [Description("The maximum base-type and derived-type traversal depth. Direct relationships are at depth one.")]
    [Range(1, int.MaxValue)]
    [DefaultValue(_defaultMaxDepth)]
    public int MaxDepth { get; init; } = _defaultMaxDepth;

    /// <summary>
    /// Gets the optional base types limit.
    /// </summary>
    [Description("Maximum number of base types to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultBaseTypesMaxResults)]
    public int? BaseTypesLimit { get; init; } = _defaultBaseTypesMaxResults;

    /// <summary>
    /// Gets the optional interfaces limit.
    /// </summary>
    [Description("Maximum number of interfaces to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultInterfacesMaxResults)]
    public int? InterfacesLimit { get; init; } = _defaultInterfacesMaxResults;

    /// <summary>
    /// Gets the optional derived types limit.
    /// </summary>
    [Description("Maximum number of derived types to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDerivedTypesMaxResults)]
    public int? DerivedTypesLimit { get; init; } = _defaultDerivedTypesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective base types limit.
    /// </summary>
    internal int EffectiveBaseTypesLimit => ResultLimit.GetEffectiveValue(BaseTypesLimit, _defaultBaseTypesMaxResults);

    /// <summary>
    /// Gets the effective interfaces limit.
    /// </summary>
    internal int EffectiveInterfacesLimit => ResultLimit.GetEffectiveValue(InterfacesLimit, _defaultInterfacesMaxResults);

    /// <summary>
    /// Gets the effective derived types limit.
    /// </summary>
    internal int EffectiveDerivedTypesLimit => ResultLimit.GetEffectiveValue(DerivedTypesLimit, _defaultDerivedTypesMaxResults);
}
