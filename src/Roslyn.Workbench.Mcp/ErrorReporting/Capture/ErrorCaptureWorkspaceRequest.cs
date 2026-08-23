using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
internal sealed record ErrorCaptureWorkspaceRequest : WorkspaceBoundRequest
{
}
