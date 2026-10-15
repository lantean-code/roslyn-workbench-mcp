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
    [Description("Limits references and declarations to the selected documents or projects; omitted searches the loaded solution.")]
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets a value indicating whether definitions should be included.
    /// </summary>
    [Description("Include source declarations alongside references within the same result limit; defaults to true.")]
    public bool IncludeDefinitions { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether context snippets should be included.
    /// </summary>
    [Description("Include the trimmed source line for each returned reference when available; declaration entries have no context. Defaults to false.")]
    public bool IncludeContext { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultReferencesMaxResults)]
    [Description("Caps returned occurrences, including declarations when requested. Omitted or null uses 100; zero returns no occurrences.")]
    public int? ReferencesLimit { get; init; } = _defaultReferencesMaxResults;

    /// <summary>
    /// Gets the expected snapshot for location-based symbol selectors.
    /// </summary>
    public SnapshotPrecondition? ExpectedSnapshot { get; init; }

    /// <summary>
    /// Gets the effective references limit.
    /// </summary>
    internal int EffectiveReferencesLimit => ResultLimit.GetEffectiveValue(ReferencesLimit, _defaultReferencesMaxResults);
}
