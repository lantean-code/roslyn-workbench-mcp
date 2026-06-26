namespace Roslyn.Workbench.Mcp.Contracts.Selectors;

/// <summary>
/// Represents the requested limit for collection-style results.
/// </summary>
public sealed record ResultLimit
{
    /// <summary>
    /// Gets the requested maximum result count.
    /// </summary>
    public int? MaxResults { get; init; }
}
