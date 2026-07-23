using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;

internal sealed record DurableCommitTarget
{
    public required string Path { get; init; }

    public required DurableCommitFileOperation Operation { get; init; }
}
