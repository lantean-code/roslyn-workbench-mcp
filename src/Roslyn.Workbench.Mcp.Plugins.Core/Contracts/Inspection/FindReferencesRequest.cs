namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find references for a resolved symbol.
/// </summary>
internal sealed record FindReferencesRequest : WorkspaceBoundRequest
{
    private const int _defaultReferencesMaxResults = 100;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    [Description("The symbol selector.")]
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    [Description("The optional search scope.")]
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets a value indicating whether definitions should be included.
    /// </summary>
    [Description("Whether definitions should be included.")]
    public bool IncludeDefinitions { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether context snippets should be included.
    /// </summary>
    [Description("Whether context snippets should be included.")]
    public bool IncludeContext { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultReferencesMaxResults)]
    public int? ReferencesLimit { get; init; } = _defaultReferencesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    [Description("The expected snapshot for location-based symbol selectors.")]
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveReferencesLimit => ResultLimit.GetEffectiveValue(ReferencesLimit, _defaultReferencesMaxResults);
}
