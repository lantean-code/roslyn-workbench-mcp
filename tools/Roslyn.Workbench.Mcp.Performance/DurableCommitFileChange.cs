namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record DurableCommitFileChange
{
    public required string Path { get; init; }

    public required DurableCommitFileOperation Operation { get; init; }

    public long? OriginalBytes { get; init; }

    public long? CommittedBytes { get; init; }
}
