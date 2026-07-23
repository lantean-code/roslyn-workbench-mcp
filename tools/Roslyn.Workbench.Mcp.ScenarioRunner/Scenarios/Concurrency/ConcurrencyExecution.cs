namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Concurrency;

internal sealed record ConcurrencyExecution
{
    public required IReadOnlyList<ConcurrentBatchMeasurement> Batches { get; init; }

    public required MultiWorkspaceMeasurement MultiWorkspace { get; init; }
}
