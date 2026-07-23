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
    [DefaultValue(_defaultMembersMaxResults)]
    public int? MembersLimit { get; init; } = _defaultMembersMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveMembersLimit => ToolExecutionHelpers.GetMaxResults(MembersLimit, _defaultMembersMaxResults);
}
