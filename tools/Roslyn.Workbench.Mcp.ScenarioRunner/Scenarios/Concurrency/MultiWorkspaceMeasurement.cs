namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Concurrency;

internal sealed record MultiWorkspaceMeasurement
{
    public required string PrimaryWorkspaceId { get; init; }

    public required string SecondaryWorkspaceId { get; init; }

    public required int ListedWorkspaceCount { get; init; }

    public required double ParallelQueryElapsedMilliseconds { get; init; }

    public required IReadOnlyList<ConcurrencyStepMeasurement> Steps { get; init; }
}
