namespace Roslyn.Workbench.Mcp.Workspace.State;

internal readonly record struct WorkspaceSnapshotId
{
    public Guid Value { get; }

    public WorkspaceSnapshotId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }
}
