namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find callers for a resolved symbol.
/// </summary>
internal sealed record FindCallersRequest : WorkspaceBoundRequest
{
    private const int _defaultCallersMaxResults = 100;
    private const int _defaultCallSitesPerCallerMaxResults = 100;

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
    /// Gets a value indicating whether context snippets should be included.
    /// </summary>
    [Description("Whether context snippets should be included.")]
    public bool IncludeContext { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultCallersMaxResults)]
    public int? CallersLimit { get; init; } = _defaultCallersMaxResults;

    /// <summary>
    /// Gets the optional call-site limit applied independently to each returned caller.
    /// </summary>
    [Description("Maximum number of call sites to return for each caller.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultCallSitesPerCallerMaxResults)]
    public int? CallSitesPerCallerLimit { get; init; } = _defaultCallSitesPerCallerMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    [Description("The expected snapshot for location-based symbol selectors.")]
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveCallersLimit => ResultLimit.GetEffectiveValue(CallersLimit, _defaultCallersMaxResults);

    internal int EffectiveCallSitesPerCallerLimit => ResultLimit.GetEffectiveValue(CallSitesPerCallerLimit, _defaultCallSitesPerCallerMaxResults);
}
