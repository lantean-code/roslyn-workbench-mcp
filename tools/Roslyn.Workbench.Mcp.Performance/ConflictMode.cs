using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Performance;

[JsonConverter(typeof(JsonStringEnumConverter<ConflictMode>))]
internal enum ConflictMode
{
    PreWriteDrift,
    DuringApplication,
}
