namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Identifies an immutable committed or transactional solution snapshot.
/// </summary>
internal readonly record struct WorkspaceSnapshotId
{
    /// <summary>
    /// Gets the non-empty snapshot identifier.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceSnapshotId"/> structure.
    /// </summary>
    /// <param name="value">The non-empty identifier value.</param>
    public WorkspaceSnapshotId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }
}
