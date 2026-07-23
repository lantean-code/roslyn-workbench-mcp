namespace Roslyn.Workbench.Mcp.Performance;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record StateSequenceDefinition
{
    public required StateSequenceKind Kind { get; init; }

    public ExternalMemberInsertionDefinition? ExternalMutation { get; init; }

    public IReadOnlyList<ToolCallDefinition> Mutations { get; init; } = [];
}
#pragma warning restore CA1812
