namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.StateSequences;

internal sealed record WatcherStressMeasurement
{
    public required string ArtifactPath { get; init; }

    public required int FileCount { get; init; }

    public required int WritePasses { get; init; }

    public int ExternalRootCount { get; init; }

    public int EvaluatedExternalGlobCount { get; init; }

    public int LoadedExternalFileCount { get; init; }

    public required double BaselineReloadMilliseconds { get; init; }

    public required double StressedReloadMilliseconds { get; init; }

    public required double ReloadDeltaMilliseconds { get; init; }

    public required double ElapsedMilliseconds { get; init; }

    public required double HostCpuMilliseconds { get; init; }

    public required long HostWorkingSetBeforeBytes { get; init; }

    public required long HostWorkingSetAfterBytes { get; init; }

    public required long HostWorkingSetDeltaBytes { get; init; }

    public required long HostPeakWorkingSetBytes { get; init; }
}
