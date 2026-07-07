namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by find-dependency-cycles.
/// </summary>
[PublishedCollectionResponse(nameof(Cycles))]
public sealed record DependencyCyclesData
{
    /// <summary>
    /// Gets the returned cycles.
    /// </summary>
    public IReadOnlyList<DependencyCycle> Cycles { get; init; } = [];

    /// <summary>
    /// Gets the number of cycles returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more cycles were available.
    /// </summary>
    public bool HasMore { get; init; }
}
