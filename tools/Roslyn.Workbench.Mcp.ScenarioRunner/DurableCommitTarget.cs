namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal sealed record DurableCommitTarget
{
    public required string Path { get; init; }

    public required DurableCommitFileOperation Operation { get; init; }
}
