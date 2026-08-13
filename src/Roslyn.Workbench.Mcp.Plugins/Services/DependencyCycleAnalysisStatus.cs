namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Identifies the outcome of dependency-cycle analysis.
/// </summary>
public enum DependencyCycleAnalysisStatus
{
    /// <summary>
    /// The complete dependency graph was analysed.
    /// </summary>
    Completed,

    /// <summary>
    /// The graph exceeded the configured node limit.
    /// </summary>
    NodeLimitExceeded,

    /// <summary>
    /// The graph exceeded the configured edge limit.
    /// </summary>
    EdgeLimitExceeded,
}
