namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Concurrency;

internal sealed record ConcurrentBatchMeasurement
{
    public required int Iteration { get; init; }

    public required double ElapsedMilliseconds { get; init; }

    public required IReadOnlyList<ConcurrentInvocationMeasurement> Invocations { get; init; }
}
