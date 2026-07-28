namespace Roslyn.Workbench.Mcp.Workspace.State;

internal readonly record struct WorkspaceSnapshotId
{
    public long Value { get; }

    public WorkspaceSnapshotId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }
}
