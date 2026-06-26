using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents a request to search for symbol declarations.
/// </summary>
public sealed record SearchSymbolsRequest
{
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
    public ResultLimit? Limit { get; init; }
}
