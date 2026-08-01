namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Concurrency;

internal sealed record MultiWorkspaceMeasurement
{
    public required Guid PrimaryWorkspaceId { get; init; }

    public required Guid SecondaryWorkspaceId { get; init; }

    public required int ListedWorkspaceCount { get; init; }

    public required double ParallelQueryElapsedMilliseconds { get; init; }

    public required IReadOnlyList<ConcurrencyStepMeasurement> Steps { get; init; }
}
