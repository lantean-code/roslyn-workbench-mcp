namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-api-surface.
/// </summary>
[PublishedCollectionResponse(nameof(Symbols))]
public sealed record ApiSurfaceData
{
    /// <summary>
    /// Gets the returned exported API symbols.
    /// </summary>
    public IReadOnlyList<ApiSymbolInfo> Symbols { get; init; } = [];

    /// <summary>
    /// Gets the number of symbols returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more symbols were available.
    /// </summary>
    public bool HasMore { get; init; }
}
