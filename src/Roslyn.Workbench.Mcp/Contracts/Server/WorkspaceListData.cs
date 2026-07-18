namespace Roslyn.Workbench.Mcp.Server.Contracts;

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
