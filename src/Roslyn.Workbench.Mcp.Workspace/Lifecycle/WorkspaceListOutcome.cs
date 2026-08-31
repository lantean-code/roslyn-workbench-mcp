namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

/// <summary>
/// Describes the host's currently loaded workspaces and process-wide transaction owner.
/// </summary>
internal sealed record WorkspaceListOutcome
{
    /// <summary>
    /// Gets the loaded workspace identities in stable identifier order.
    /// </summary>
    public IReadOnlyList<WorkspaceIdentity> Workspaces { get; init; } = [];

    /// <summary>
    /// Gets the workspace that owns the process-wide transaction, when one is active.
    /// </summary>
    public Guid? TransactionOwnerWorkspaceId { get; init; }
}
