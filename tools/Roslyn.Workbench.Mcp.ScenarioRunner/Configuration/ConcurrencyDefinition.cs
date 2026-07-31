using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record ConcurrencyDefinition
{
    public required string SecondaryWorkspacePath { get; init; }

    public required JsonElement SecondaryArguments { get; init; }

    public bool ValidateSingleFlight { get; init; }
}
#pragma warning restore CA1812
