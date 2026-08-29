namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to retrieve members for a resolved symbol.
/// </summary>
internal sealed record GetSymbolMembersRequest : WorkspaceBoundRequest
{
    private const int _defaultMembersMaxResults = 100;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    [Description("The symbol selector.")]
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether inherited members should be included.
    /// </summary>
    [Description("Whether inherited members should be included.")]
    public bool IncludeInherited { get; init; }

    /// <summary>
    /// Gets a value indicating whether explicit interface members should be included.
    /// </summary>
    [Description("Whether explicit interface members should be included.")]
    public bool IncludeExplicitInterface { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultMembersMaxResults)]
    public int? MembersLimit { get; init; } = _defaultMembersMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    [Description("The expected snapshot for location-based symbol selectors.")]
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveMembersLimit => ResultLimit.GetEffectiveValue(MembersLimit, _defaultMembersMaxResults);
}
