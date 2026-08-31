namespace Roslyn.Workbench.Mcp.Contracts.Server;

/// <summary>
/// Represents the structured payload returned when listing loaded workspaces.
/// </summary>
internal sealed record WorkspaceListData
{
    /// <summary>
    /// The loaded workspaces.
    /// </summary>
    [Description("The loaded workspaces.")]
    public IReadOnlyList<WorkspaceIdentity> Workspaces { get; init; } = [];

    /// <summary>
    /// The workspace identifier that currently owns the global transaction slot, when present.
    /// </summary>
    [Description("The workspace identifier that currently owns the global transaction slot, when present.")]
    public Guid? TransactionOwnerWorkspaceId { get; init; }
}
