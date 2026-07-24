namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.StateSequences;

internal sealed record ExternalCommandMeasurement
{
    public required string FileName { get; init; }

    public required IReadOnlyList<string> Arguments { get; init; }

    public required double ElapsedMilliseconds { get; init; }

    public required int ExitCode { get; init; }

    public required double HostCpuMilliseconds { get; init; }

    public required long HostWorkingSetBeforeBytes { get; init; }

    public required long HostWorkingSetAfterBytes { get; init; }

    public required long HostWorkingSetDeltaBytes { get; init; }

    public required long HostPeakWorkingSetBytes { get; init; }

    public required int StandardOutputBytes { get; init; }

    public required int StandardErrorBytes { get; init; }
}
