namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Represents the immutable process-wide registry of loaded Workspaces and transaction ownership.
/// </summary>
internal sealed record WorkspaceHostSnapshot
{
    /// <summary>
    /// Gets loaded sessions keyed by Workspace identifier.
    /// </summary>
    public IReadOnlyDictionary<Guid, WorkspaceSessionSnapshot> Workspaces { get; init; } = new Dictionary<Guid, WorkspaceSessionSnapshot>();

    /// <summary>
    /// Gets the Workspace that owns the single process-wide transaction slot.
    /// </summary>
    public Guid? TransactionOwnerWorkspaceId { get; init; }
}
