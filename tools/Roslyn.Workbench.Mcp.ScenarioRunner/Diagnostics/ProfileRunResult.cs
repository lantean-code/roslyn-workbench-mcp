using Roslyn.Workbench.Mcp.ScenarioRunner.Reporting;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Diagnostics;

internal sealed record ProfileRunResult
{
    public required string Repository { get; init; }

    public required string RepositorySize { get; init; }

    public required string Commit { get; init; }

    public required string Scenario { get; init; }

    public required string Tool { get; init; }

    public IReadOnlyList<string> ScenarioSequence { get; init; } = [];

    public IReadOnlyList<string> ToolSequence { get; init; } = [];

    public required ProfileKind Profile { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required RunEnvironmentInfo Environment { get; init; }

    public TimeSpan? RequestedDuration { get; init; }

    public required int InvocationCount { get; init; }

    public required string DiagnosticArtifact { get; init; }

    public string? PostCloseDiagnosticArtifact { get; init; }

    public ProfileInvocationTiming? InvocationTiming { get; init; }

    public IReadOnlyList<PhaseTraceSummary> PhaseSummary { get; init; } = [];
}
