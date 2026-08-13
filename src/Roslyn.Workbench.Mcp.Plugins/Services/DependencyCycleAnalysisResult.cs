using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Represents the complete outcome of dependency-cycle analysis.
/// </summary>
public sealed class DependencyCycleAnalysisResult
{
    /// <summary>
    /// Gets the analysis status.
    /// </summary>
    public DependencyCycleAnalysisStatus Status { get; }

    /// <summary>
    /// Gets the selected cycles when analysis completed.
    /// </summary>
    public IReadOnlyList<DependencyCycle>? Cycles { get; }

    /// <summary>
    /// Gets the complete cycle count when analysis completed.
    /// </summary>
    public int? TotalCount { get; }

    /// <summary>
    /// Gets a value indicating whether the complete graph was analysed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Cycles), nameof(TotalCount))]
    public bool IsCompleted => Status == DependencyCycleAnalysisStatus.Completed;

    private DependencyCycleAnalysisResult(DependencyCycleAnalysisStatus status, IReadOnlyList<DependencyCycle>? cycles, int? totalCount)
    {
        Status = status;
        Cycles = cycles;
        TotalCount = totalCount;
    }

    /// <summary>
    /// Creates a completed analysis result.
    /// </summary>
    /// <param name="cycles">The selected cycles.</param>
    /// <param name="totalCount">The complete cycle count.</param>
    /// <returns>The completed result.</returns>
    public static DependencyCycleAnalysisResult Completed(IReadOnlyList<DependencyCycle> cycles, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(cycles);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        if (cycles.Count > totalCount)
        {
            throw new ArgumentException("The selected cycle count cannot exceed the total cycle count.", nameof(cycles));
        }

        return new DependencyCycleAnalysisResult(DependencyCycleAnalysisStatus.Completed, cycles, totalCount);
    }

    /// <summary>
    /// Creates a result indicating that the node limit was exceeded.
    /// </summary>
    /// <returns>The limit result.</returns>
    public static DependencyCycleAnalysisResult NodeLimitExceeded()
    {
        return new DependencyCycleAnalysisResult(DependencyCycleAnalysisStatus.NodeLimitExceeded, null, null);
    }

    /// <summary>
    /// Creates a result indicating that the edge limit was exceeded.
    /// </summary>
    /// <returns>The limit result.</returns>
    public static DependencyCycleAnalysisResult EdgeLimitExceeded()
    {
        return new DependencyCycleAnalysisResult(DependencyCycleAnalysisStatus.EdgeLimitExceeded, null, null);
    }
}
