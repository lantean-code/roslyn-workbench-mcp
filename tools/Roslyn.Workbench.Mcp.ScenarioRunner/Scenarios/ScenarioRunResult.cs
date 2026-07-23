using Roslyn.Workbench.Mcp.ScenarioRunner.Reporting;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

internal sealed record ScenarioRunResult
{
    public required string Repository { get; init; }

    public required string RepositorySize { get; init; }

    public required string Commit { get; init; }

    public required string Scenario { get; init; }

    public required string Tool { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required RunEnvironmentInfo Environment { get; init; }

    public required int WarmupCount { get; init; }

    public required IReadOnlyList<InvocationMeasurement> Measurements { get; init; }
}
