namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to search for symbol declarations.
/// </summary>
[Description("Provide query, metadataName, or both.")]
[RequiresAtLeastOne(
    nameof(Query),
    nameof(MetadataName),
    ErrorMessage = "Search symbols requires query or metadataName.")]
internal sealed record SearchSymbolsRequest : WorkspaceBoundRequest
{
    private const int _defaultSymbolsMaxResults = 100;

    /// <summary>
    /// Gets the source-name query.
    /// </summary>
    [Description("Source-name query.")]
    public string? Query { get; init; }

    /// <summary>
    /// Gets the metadata-name query.
    /// </summary>
    public string? MetadataName { get; init; }

    /// <summary>
    /// Gets the optional scope selector.
    /// </summary>
    [Description("The optional scope selector.")]
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the included symbol kinds.
    /// </summary>
    [Description("The included symbol kinds.")]
    public IReadOnlyList<string>? Kinds { get; init; }

    /// <summary>
    /// Gets the included accessibilities.
    /// </summary>
    [Description("The included accessibilities.")]
    public IReadOnlyList<string>? Accessibilities { get; init; }

    /// <summary>
    /// Gets the optional namespace filter.
    /// </summary>
    [Description("The optional namespace filter.")]
    public string? Namespace { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [Description("Maximum number of results to return.")]
    [Range(0, int.MaxValue)]
    [DefaultValue(_defaultSymbolsMaxResults)]
    public int? SymbolsLimit { get; init; } = _defaultSymbolsMaxResults;

    internal int EffectiveSymbolsLimit => ResultLimit.GetEffectiveValue(SymbolsLimit, _defaultSymbolsMaxResults);
}
