namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal sealed record StateSequenceRunResult
{
    public required string Repository { get; init; }

    public required string RepositorySize { get; init; }

    public required string Commit { get; init; }

    public required string Scenario { get; init; }

    public required StateSequenceKind Kind { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required RunEnvironmentInfo Environment { get; init; }

    public required int WarmupCount { get; init; }

    public required IReadOnlyList<StateSequenceMeasurement> Measurements { get; init; }
}
