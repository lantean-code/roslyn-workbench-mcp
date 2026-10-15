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
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    [Description("Limits caller discovery to the selected documents or projects; omitted searches the loaded solution.")]
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets a value indicating whether context snippets should be included.
    /// </summary>
    [Description("Include the trimmed source line for each returned call site when available; defaults to false.")]
    public bool IncludeContext { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultCallersMaxResults)]
    [Description("Caps returned callers independently of the call-site limit. Omitted or null uses 100; zero returns no callers.")]
    public int? CallersLimit { get; init; } = _defaultCallersMaxResults;

    /// <summary>
    /// Gets the optional call-site limit applied independently to each returned caller.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultCallSitesPerCallerMaxResults)]
    [Description("Caps call sites independently for each returned caller. Omitted or null uses 100; zero retains callers without call sites.")]
    public int? CallSitesPerCallerLimit { get; init; } = _defaultCallSitesPerCallerMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective callers limit.
    /// </summary>
    internal int EffectiveCallersLimit => ResultLimit.GetEffectiveValue(CallersLimit, _defaultCallersMaxResults);

    /// <summary>
    /// Gets the effective call sites per caller limit.
    /// </summary>
    internal int EffectiveCallSitesPerCallerLimit => ResultLimit.GetEffectiveValue(CallSitesPerCallerLimit, _defaultCallSitesPerCallerMaxResults);
}
