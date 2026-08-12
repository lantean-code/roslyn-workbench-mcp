namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents a request to reload the loaded workspace.
/// </summary>
internal sealed record WorkspaceReloadRequest : WorkspaceBoundRequest
{ }
