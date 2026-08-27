namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to navigate to symbol definitions.
/// </summary>
internal sealed record GoToDefinitionRequest : WorkspaceBoundRequest
{
    private const int _defaultDefinitionsMaxResults = 32;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional definitions limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDefinitionsMaxResults)]
    public int? DefinitionsLimit { get; init; } = _defaultDefinitionsMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveDefinitionsLimit => ResultLimit.GetEffectiveValue(DefinitionsLimit, _defaultDefinitionsMaxResults);
}
