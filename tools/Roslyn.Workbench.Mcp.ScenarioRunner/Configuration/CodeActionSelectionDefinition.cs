using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record CodeActionSelectionDefinition
{
    public required JsonElement Arguments { get; init; }

    public required string TitleContains { get; init; }

    public string? DiagnosticId { get; init; }
}
#pragma warning restore CA1812
