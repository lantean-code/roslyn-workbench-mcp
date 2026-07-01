using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents a request to reload the loaded workspace.
/// </summary>
public sealed record WorkspaceReloadRequest : WorkspaceBoundRequest
{ }
