namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal sealed record RepositoryChangeSet
{
    public required IReadOnlyList<DurableCommitFileChange> Files { get; init; }
}
