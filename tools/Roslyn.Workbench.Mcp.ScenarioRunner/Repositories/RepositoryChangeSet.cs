using Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Repositories;

internal sealed record RepositoryChangeSet
{
    public required IReadOnlyList<DurableCommitFileChange> Files { get; init; }
}
