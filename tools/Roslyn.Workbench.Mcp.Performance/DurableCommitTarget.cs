namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record DurableCommitTarget
{
    public required string Path { get; init; }

    public required DurableCommitFileOperation Operation { get; init; }
}
