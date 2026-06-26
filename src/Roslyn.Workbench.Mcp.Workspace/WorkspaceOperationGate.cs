namespace Roslyn.Workbench.Mcp.Workspace;

/// <summary>
/// Provides shared and exclusive non-waiting operation leases for workspace activity.
/// </summary>
public sealed class WorkspaceOperationGate
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
    public IAsyncDisposable? TryAcquireShared()
    {
        lock (_syncRoot)
        {
            if (_exclusiveLeaseHeld || _sharedLeaseCount >= _maxConcurrentQueries)
            {
                return null;
            }

            _sharedLeaseCount++;
            return new Lease(this, isExclusive: false);
        }
    }

    /// <summary>
    /// Attempts to acquire an exclusive lease.
    /// </summary>
    /// <returns>The lease when acquired; otherwise, <see langword="null"/>.</returns>
    public IAsyncDisposable? TryAcquireExclusive()
    {
        lock (_syncRoot)
        {
            if (_exclusiveLeaseHeld || _sharedLeaseCount > 0)
            {
                return null;
            }

            _exclusiveLeaseHeld = true;
            return new Lease(this, isExclusive: true);
        }
    }

    private void Release(bool isExclusive)
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

    private sealed class Lease : IAsyncDisposable
    {
        private readonly WorkspaceOperationGate _owner;
        private readonly bool _isExclusive;
        private bool _disposed;

        public Lease(WorkspaceOperationGate owner, bool isExclusive)
        {
            _owner = owner;
            _isExclusive = isExclusive;
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _owner.Release(_isExclusive);
                _disposed = true;
            }

            return ValueTask.CompletedTask;
        }
    }
}
