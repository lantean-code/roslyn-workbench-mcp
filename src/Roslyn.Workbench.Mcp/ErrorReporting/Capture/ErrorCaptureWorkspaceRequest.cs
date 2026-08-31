using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

/// <summary>
/// Binds only the workspace selector from a failed tool request while ignoring all other arguments.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
internal sealed record ErrorCaptureWorkspaceRequest : WorkspaceBoundRequest
{
}
