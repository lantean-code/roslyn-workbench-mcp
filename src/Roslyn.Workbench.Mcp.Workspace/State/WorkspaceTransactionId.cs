namespace Roslyn.Workbench.Mcp.Workspace.State;

internal readonly record struct WorkspaceTransactionId
{
    public long Value { get; }

    public WorkspaceTransactionId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }
}
