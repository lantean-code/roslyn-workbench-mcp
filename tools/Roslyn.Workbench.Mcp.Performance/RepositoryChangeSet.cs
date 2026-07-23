namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record RepositoryChangeSet
{
    public required IReadOnlyList<DurableCommitFileChange> Files { get; init; }
}
