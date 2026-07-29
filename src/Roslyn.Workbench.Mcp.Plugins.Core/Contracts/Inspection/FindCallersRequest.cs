namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to find callers for a resolved symbol.
/// </summary>
internal sealed record FindCallersRequest : WorkspaceBoundRequest
{
    private const int _defaultCallersMaxResults = 100;

    /// <summary>
    /// Gets the symbol selector.
    /// </summary>
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets a value indicating whether context snippets should be included.
    /// </summary>
    public bool IncludeContext { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultCallersMaxResults)]
    public int? CallersLimit { get; init; } = _defaultCallersMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveCallersLimit => ResultLimit.GetEffectiveValue(CallersLimit, _defaultCallersMaxResults);
}
