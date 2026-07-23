namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;

internal sealed record DurableCommitExecution
{
    public required double StagingMilliseconds { get; init; }

    public required double PreviewMilliseconds { get; init; }

    public required double CommitMilliseconds { get; init; }

    public required double CommitHostCpuMilliseconds { get; init; }

    public required long WorkingSetBytes { get; init; }

    public required long PeakWorkingSetBytes { get; init; }

    public required int CommitResponseBytes { get; init; }

    public required string CommitResponseSha256 { get; init; }

    public required int PreviewDocumentCount { get; init; }
}
