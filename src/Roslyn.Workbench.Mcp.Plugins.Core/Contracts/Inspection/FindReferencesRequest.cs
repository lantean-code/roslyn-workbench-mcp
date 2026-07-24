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
    public required SymbolSelector Symbol { get; init; }

    /// <summary>
    /// Gets the optional search scope.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets a value indicating whether definitions should be included.
    /// </summary>
    public bool IncludeDefinitions { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether context snippets should be included.
    /// </summary>
    public bool IncludeContext { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [DefaultValue(_defaultReferencesMaxResults)]
    public int? ReferencesLimit { get; init; } = _defaultReferencesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    internal int EffectiveReferencesLimit => ToolExecutionHelpers.GetMaxResults(ReferencesLimit, _defaultReferencesMaxResults);
}
