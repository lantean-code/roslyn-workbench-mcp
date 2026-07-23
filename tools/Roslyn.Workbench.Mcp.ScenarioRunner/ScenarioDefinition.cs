using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ScenarioRunner;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record ScenarioDefinition
{
    public required string Id { get; init; }

    public required string Description { get; init; }

    public required string Tool { get; init; }

    public required JsonElement Arguments { get; init; }

    public IReadOnlyList<ToolCallDefinition> Setup { get; init; } = [];

    public IReadOnlyList<ToolCallDefinition> Cleanup { get; init; } = [];

    public bool CommitOnly { get; init; }

    public ConflictDefinition? Conflict { get; init; }

    public DurableCommitFileOperation? CrashAfterOperation { get; init; }

    public StateSequenceDefinition? StateSequence { get; init; }

    public ConcurrencyDefinition? Concurrency { get; init; }
}
#pragma warning restore CA1812
