namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed class WorkspaceOperationLease : IWorkspaceOperationLease
{
    private readonly WorkspaceOperationGate _owner;
    private readonly bool _isExclusive;
    private bool _disposed;

    public WorkspaceOperationLease(WorkspaceOperationGate owner, bool isExclusive)
    {
        _owner = owner;
        _isExclusive = isExclusive;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _owner.Release(_isExclusive);
        _disposed = true;
    }
}
