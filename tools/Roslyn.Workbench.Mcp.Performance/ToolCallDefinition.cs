using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Performance;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record ToolCallDefinition
{
    public required string Tool { get; init; }

    public required JsonElement Arguments { get; init; }
}
#pragma warning restore CA1812
