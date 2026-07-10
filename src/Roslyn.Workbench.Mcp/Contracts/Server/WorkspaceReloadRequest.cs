using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Server.Contracts;

/// <summary>
/// Represents a request to reload the loaded workspace.
/// </summary>
public sealed record WorkspaceReloadRequest : WorkspaceBoundRequest
{ }
