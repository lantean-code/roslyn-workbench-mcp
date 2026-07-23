using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Performance;

[JsonConverter(typeof(JsonStringEnumConverter<StateSequenceKind>))]
internal enum StateSequenceKind
{
    ExternalReload,
    MultiRevisionCommit,
}
