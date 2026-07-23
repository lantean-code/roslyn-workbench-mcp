using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.ScenarioRunner;

[JsonConverter(typeof(JsonStringEnumConverter<StateSequenceKind>))]
internal enum StateSequenceKind
{
    ExternalReload,
    MultiRevisionCommit,
}
