namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record DurableCommitPreparation
{
    public required double StagingMilliseconds { get; init; }

    public required double PreviewMilliseconds { get; init; }

    public required int PreviewDocumentCount { get; init; }
}
