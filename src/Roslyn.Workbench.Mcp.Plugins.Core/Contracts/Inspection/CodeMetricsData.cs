using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents the structured payload returned by get-code-metrics.
/// </summary>
public sealed record CodeMetricsData
{
    /// <summary>
    /// Gets the returned metric rows.
    /// </summary>
    public BoundedCollection<MetricInfo> Metrics { get; init; } = BoundedCollection<MetricInfo>.Empty();
}
