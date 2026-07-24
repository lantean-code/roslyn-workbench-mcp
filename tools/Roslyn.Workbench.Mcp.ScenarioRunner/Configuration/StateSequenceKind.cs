using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter<StateSequenceKind>))]
internal enum StateSequenceKind
{
    ExternalReload,
    LiveBuild,
    MultiRevisionCommit,
    WatcherStress,
}
