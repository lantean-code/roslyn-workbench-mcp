using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the structured payload returned when listing loaded workspaces.
/// </summary>
public sealed record WorkspaceListData
{
    /// <summary>
    /// Gets the loaded workspaces.
    /// </summary>
    public IReadOnlyList<WorkspaceIdentity> Workspaces { get; init; } = [];

    /// <summary>
    /// Gets the workspace identifier that currently owns the global transaction slot, when present.
    /// </summary>
    public string? TransactionOwnerWorkspaceId { get; init; }
}
