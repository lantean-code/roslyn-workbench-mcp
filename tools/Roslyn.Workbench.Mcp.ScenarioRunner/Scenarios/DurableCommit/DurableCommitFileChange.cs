using Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.DurableCommit;

internal sealed record DurableCommitFileChange
{
    public required string Path { get; init; }

    public required DurableCommitFileOperation Operation { get; init; }

    public long? OriginalBytes { get; init; }

    public long? CommittedBytes { get; init; }
}
