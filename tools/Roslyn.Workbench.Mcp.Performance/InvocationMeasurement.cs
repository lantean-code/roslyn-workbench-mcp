namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record InvocationMeasurement
{
    public required int Iteration { get; init; }

    public required double ElapsedMilliseconds { get; init; }

    public required double HostCpuMilliseconds { get; init; }

    public required long WorkingSetBytes { get; init; }

    public required long WorkingSetDeltaBytes { get; init; }

    public required long PeakWorkingSetBytes { get; init; }

    public required int ResponseBytes { get; init; }

    public required string ResponseSha256 { get; init; }

    public IReadOnlyList<BoundedCollectionObservation> BoundedCollections { get; init; } = [];

    public bool? MutationStaged { get; init; }

    public required bool IsError { get; init; }
}
