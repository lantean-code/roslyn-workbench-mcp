namespace Roslyn.Workbench.Mcp.ScenarioRunner.Diagnostics;

internal sealed record AtomicFileCommitRetryObservation
{
    public required int RetryNumber { get; init; }

    public required int DelayMilliseconds { get; init; }
}
