using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.ScenarioRunner;

[JsonConverter(typeof(JsonStringEnumConverter<ConflictMode>))]
internal enum ConflictMode
{
    PreWriteDrift,
    DuringApplication,
}
