using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter<DurableCommitFileOperation>))]
internal enum DurableCommitFileOperation
{
    Create,
    Replace,
    Delete,
}
