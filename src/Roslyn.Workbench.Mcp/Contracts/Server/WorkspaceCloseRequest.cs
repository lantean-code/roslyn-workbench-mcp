namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents a request to close the loaded workspace.
/// </summary>
internal sealed record WorkspaceCloseRequest : WorkspaceBoundRequest
{ }
