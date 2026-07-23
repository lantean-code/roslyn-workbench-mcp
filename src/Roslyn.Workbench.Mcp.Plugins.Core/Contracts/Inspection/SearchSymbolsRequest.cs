namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents a request to search for symbol declarations.
/// </summary>
internal sealed record SearchSymbolsRequest : WorkspaceBoundRequest
{
    private const int _defaultSymbolsMaxResults = 100;

    /// <summary>
    /// Gets the source-name query.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Gets the metadata-name query.
    /// </summary>
    public string? MetadataName { get; init; }

    /// <summary>
    /// Gets the optional scope selector.
    /// </summary>
    public ScopeSelector? Scope { get; init; }

    /// <summary>
    /// Gets the included symbol kinds.
    /// </summary>
    public IReadOnlyList<string>? Kinds { get; init; }

    /// <summary>
    /// Gets the included accessibilities.
    /// </summary>
    public IReadOnlyList<string>? Accessibilities { get; init; }

    /// <summary>
    /// Gets the optional namespace filter.
    /// </summary>
    public string? Namespace { get; init; }

    /// <summary>
    /// Gets the optional result limit.
    /// </summary>
    [DefaultValue(_defaultSymbolsMaxResults)]
    public int? SymbolsLimit { get; init; } = _defaultSymbolsMaxResults;

    internal int EffectiveSymbolsLimit => ToolExecutionHelpers.GetMaxResults(SymbolsLimit, _defaultSymbolsMaxResults);
}
