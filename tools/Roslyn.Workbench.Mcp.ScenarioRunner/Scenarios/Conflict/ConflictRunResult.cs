using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;
using Roslyn.Workbench.Mcp.ScenarioRunner.Reporting;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.Conflict;

internal sealed record ConflictRunResult
{
    public required string Repository { get; init; }

    public required string RepositorySize { get; init; }

    public required string Commit { get; init; }

    public required string Scenario { get; init; }

    public required ConflictMode Mode { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required RunEnvironmentInfo Environment { get; init; }

    public required int WarmupCount { get; init; }

    public required IReadOnlyList<ConflictMeasurement> Measurements { get; init; }
}
