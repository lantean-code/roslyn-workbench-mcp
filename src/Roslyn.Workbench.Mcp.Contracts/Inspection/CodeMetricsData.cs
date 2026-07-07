namespace Roslyn.Workbench.Mcp.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-code-metrics.
/// </summary>
[PublishedCollectionResponse(nameof(Metrics))]
public sealed record CodeMetricsData
{
    /// <summary>
    /// Gets the returned metric rows.
    /// </summary>
    public IReadOnlyList<MetricInfo> Metrics { get; init; } = [];

    /// <summary>
    /// Gets the number of metric rows returned.
    /// </summary>
    public int ReturnedCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether more metric rows were available.
    /// </summary>
    public bool HasMore { get; init; }
}
