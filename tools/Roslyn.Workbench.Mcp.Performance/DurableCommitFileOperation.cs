using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Performance;

[JsonConverter(typeof(JsonStringEnumConverter<DurableCommitFileOperation>))]
internal enum DurableCommitFileOperation
{
    Create,
    Replace,
    Delete,
}
