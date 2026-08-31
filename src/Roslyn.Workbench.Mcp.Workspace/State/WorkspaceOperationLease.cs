namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Releases shared or exclusive Workspace operation ownership exactly once.
/// </summary>
internal sealed class WorkspaceOperationLease : IWorkspaceOperationLease
{
    private readonly WorkspaceOperationGate _owner;
    private readonly bool _isExclusive;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceOperationLease"/> class.
    /// </summary>
    /// <param name="owner">The gate to notify on disposal.</param>
    /// <param name="isExclusive">Whether the workspace lease holds exclusive ownership.</param>
    public WorkspaceOperationLease(WorkspaceOperationGate owner, bool isExclusive)
    {
        _owner = owner;
        _isExclusive = isExclusive;
    }

    /// <inheritdoc/>
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
