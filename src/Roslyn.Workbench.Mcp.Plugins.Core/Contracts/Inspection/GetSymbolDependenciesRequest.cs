namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to return the direct dependencies of a symbol.
/// </summary>
internal sealed record GetSymbolDependenciesRequest : WorkspaceBoundRequest
{
    private const int _defaultDependenciesMaxResults = 100;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    [Description("The symbol selector.")]
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets a value indicating whether containing assembly names should be included with each dependency.
    /// </summary>
    [Description("Whether containing assembly names should be included with each dependency.")]
    public bool IncludeAssemblies { get; init; } = true;

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultDependenciesMaxResults)]
    public int? DependenciesLimit { get; init; } = _defaultDependenciesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    [Description("The expected snapshot for location-based symbol selectors.")]
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveDependenciesLimit => ResultLimit.GetEffectiveValue(DependenciesLimit, _defaultDependenciesMaxResults);
}
