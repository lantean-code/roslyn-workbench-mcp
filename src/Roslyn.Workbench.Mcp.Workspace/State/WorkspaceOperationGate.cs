namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Provides shared and exclusive non-waiting operation leases for workspace activity.
/// </summary>
internal sealed class WorkspaceOperationGate : IWorkspaceOperationGate
{
    private readonly Lock _syncRoot;
    private readonly int _maxConcurrentQueries;
    private int _sharedLeaseCount;
    private bool _exclusiveLeaseHeld;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceOperationGate"/> class.
    /// </summary>
    /// <param name="maxConcurrentQueries">The maximum number of concurrent shared query leases.</param>
    public WorkspaceOperationGate(int maxConcurrentQueries)
    {
        _maxConcurrentQueries = maxConcurrentQueries > 0 ? maxConcurrentQueries : throw new ArgumentOutOfRangeException(nameof(maxConcurrentQueries));
        _syncRoot = new Lock();
    }

    /// <summary>
    /// Attempts to acquire a shared lease.
    /// </summary>
    /// <returns>The lease when acquired; otherwise, <see langword="null"/>.</returns>
    public IWorkspaceOperationLease? TryAcquireShared()
    {
        lock (_syncRoot)
        {
            if (_exclusiveLeaseHeld || _sharedLeaseCount >= _maxConcurrentQueries)
            {
                return null;
            }

            _sharedLeaseCount++;
            return new WorkspaceOperationLease(this, isExclusive: false);
        }
    }

    /// <summary>
    /// Attempts to acquire an exclusive lease.
    /// </summary>
    /// <returns>The lease when acquired; otherwise, <see langword="null"/>.</returns>
    public IWorkspaceOperationLease? TryAcquireExclusive()
    {
        lock (_syncRoot)
        {
            if (_exclusiveLeaseHeld || _sharedLeaseCount > 0)
            {
                return null;
            }

            _exclusiveLeaseHeld = true;
            return new WorkspaceOperationLease(this, isExclusive: true);
        }
    }

    internal void Release(bool isExclusive)
    {
        lock (_syncRoot)
        {
            if (isExclusive)
            {
                _exclusiveLeaseHeld = false;
            }
            else
            {
                _sharedLeaseCount--;
            }
        }
    }

}
