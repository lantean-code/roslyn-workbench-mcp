using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.Protocol.Results;

[JsonConverter(typeof(JsonStringEnumConverter<ToolContinuationKind>))]
internal enum ToolContinuationKind
{
    CallTool,
    ChooseTool,
    RetryRequest,
    ReviseRequest,
    ResolveExternally,
}
