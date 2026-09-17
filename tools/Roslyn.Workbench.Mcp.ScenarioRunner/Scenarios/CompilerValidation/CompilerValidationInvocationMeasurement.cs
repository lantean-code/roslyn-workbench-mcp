namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CompilerValidation;

internal sealed record CompilerValidationInvocationMeasurement
{
    public required double ElapsedMilliseconds { get; init; }

    public required long ReportedDurationMilliseconds { get; init; }

    public required double HostCpuMilliseconds { get; init; }

    public required long WorkingSetBytes { get; init; }

    public required long WorkingSetDeltaBytes { get; init; }

    public required long PeakWorkingSetBytes { get; init; }

    public required int ResponseBytes { get; init; }

    public required int AffectedProjectCount { get; init; }

    public required int IntroducedErrorCount { get; init; }

    public required int IntroducedDiagnosticCount { get; init; }

    public required IReadOnlyDictionary<string, int> IntroducedDiagnosticIds { get; init; }

    public required IReadOnlyDictionary<string, int> ProjectsWithIntroducedErrors { get; init; }

    public required bool IsComplete { get; init; }

    public required bool Succeeded { get; init; }

    public required IReadOnlyList<string> Limitations { get; init; }
}
