using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

#pragma warning disable CA1812 // Instances are created by System.Text.Json deserialisation of the checked-in suite.
internal sealed record CodeActionSelectionDefinition
{
    public JsonElement Arguments { get; init; }

    public string? TitleContains { get; init; }

    public string? DiagnosticId { get; init; }

    public CodeActionSelectionLocation? Location { get; init; }

    public string? CaptureAs { get; init; }

    public string? UseCaptured { get; init; }
}
#pragma warning restore CA1812
