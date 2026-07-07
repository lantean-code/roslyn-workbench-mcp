namespace Roslyn.Workbench.Mcp.Contracts.Selectors;

/// <summary>
/// Represents the requested limit for a published collection.
/// </summary>
public sealed record CollectionLimit
{
    /// <summary>
    /// Gets the requested maximum result count.
    /// </summary>
    public int? MaxResults { get; init; }
}
