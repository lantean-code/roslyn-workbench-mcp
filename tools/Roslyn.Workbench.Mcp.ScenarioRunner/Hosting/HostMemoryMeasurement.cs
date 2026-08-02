namespace Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;

internal sealed record HostMemoryMeasurement
{
    public required double SamplingIntervalMilliseconds { get; init; }

    public required int SampleCount { get; init; }

    public required long BaselineWorkingSetBytes { get; init; }

    public required long FinalWorkingSetBytes { get; init; }

    public required long PeakWorkingSetBytes { get; init; }

    public required long BaselinePrivateMemoryBytes { get; init; }

    public required long FinalPrivateMemoryBytes { get; init; }

    public required long PeakPrivateMemoryBytes { get; init; }

    public long PeakWorkingSetIncreaseBytes
    {
        get
        {
            return PeakWorkingSetBytes - BaselineWorkingSetBytes;
        }
    }

    public long PeakPrivateMemoryIncreaseBytes
    {
        get
        {
            return PeakPrivateMemoryBytes - BaselinePrivateMemoryBytes;
        }
    }
}
