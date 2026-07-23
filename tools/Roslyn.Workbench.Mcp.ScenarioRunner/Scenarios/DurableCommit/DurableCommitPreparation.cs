namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;

internal sealed record DurableCommitPreparation
{
    public required double StagingMilliseconds { get; init; }

    public required double PreviewMilliseconds { get; init; }

    public required int PreviewDocumentCount { get; init; }

    public required IReadOnlyList<DurableCommitTarget> ChangedTargets { get; init; }
}
