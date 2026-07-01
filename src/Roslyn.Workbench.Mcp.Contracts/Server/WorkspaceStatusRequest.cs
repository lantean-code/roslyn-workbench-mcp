using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents a request to retrieve workspace status.
/// </summary>
public sealed record WorkspaceStatusRequest : WorkspaceBoundRequest
{ }
