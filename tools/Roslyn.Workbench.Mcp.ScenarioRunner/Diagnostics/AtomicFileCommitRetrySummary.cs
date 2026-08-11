namespace Roslyn.Workbench.Mcp.ScenarioRunner.Diagnostics;

internal sealed record AtomicFileCommitRetrySummary
{
    public required int TotalRetryAttempts { get; init; }

    public required int RetriedOperationCount { get; init; }

    public required int MaximumRetriesForOneOperation { get; init; }

    public required int TotalDelayMilliseconds { get; init; }
}
