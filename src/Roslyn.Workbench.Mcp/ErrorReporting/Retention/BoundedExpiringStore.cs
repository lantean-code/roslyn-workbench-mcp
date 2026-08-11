using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Retention;

internal sealed class BoundedExpiringStore<TKey, TValue> :
    IBoundedExpiringStore<TKey, TValue>,
    IDisposable,
    IAsyncDisposable
    where TKey : notnull
    where TValue : class
{
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<TKey, TValue> _entries = [];
    private readonly IBoundedExpiringStorePolicy<TKey, TValue> _policy;
    private readonly TimeProvider _timeProvider;
    private readonly ITimer _expirationTimer;
    private readonly int _capacity;
    private bool _disposed;

    public BoundedExpiringStore(
        IBoundedExpiringStorePolicy<TKey, TValue> policy,
        TimeProvider timeProvider)
    {
        _policy = policy;
        _timeProvider = timeProvider;
        _capacity = policy.Capacity;
        _expirationTimer = timeProvider.CreateTimer(
            RemoveExpired,
            state: null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    public void AddOrReplace(TKey key, TValue value)
    {
        Execute(() =>
        {
            if (!TryMakeCapacity())
            {
                throw new InvalidOperationException(
                    "The bounded expiring store capacity policy rejected an add-or-replace operation.");
            }

            _entries[key] = value;
            return true;
        });
    }

    public bool TryAdd(TKey key, TValue value)
    {
        return Execute(() =>
        {
            if (!TryMakeCapacity())
            {
                return false;
            }

            return _entries.TryAdd(key, value);
        });
    }

    public bool TryGet(
        TKey key,
        [NotNullWhen(true)] out TValue? value)
    {
        TValue? storedValue = default;
        var wasFound = Execute(() => _entries.TryGetValue(key, out storedValue));
        value = storedValue;
        return wasFound;
    }

    public BoundedExpiringStoreUpdateResult<TValue> Update(
        TKey key,
        Func<TValue, TValue> update)
    {
        return Execute(() =>
        {
            if (!_entries.TryGetValue(key, out var originalValue))
            {
                return BoundedExpiringStoreUpdateResult.NotFound<TValue>();
            }

            var updatedValue = update(originalValue);
            _entries[key] = updatedValue;
            return BoundedExpiringStoreUpdateResult.Updated(
                originalValue,
                updatedValue);
        });
    }

    public bool Remove(TKey key)
    {
        return Execute(() => _entries.Remove(key));
    }

    public void Dispose()
    {
        if (MarkDisposed())
        {
            _expirationTimer.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (MarkDisposed())
        {
            await _expirationTimer.DisposeAsync();
        }
    }

    private TResult Execute<TResult>(Func<TResult> operation)
    {
        lock (_syncRoot)
        {
            var now = _timeProvider.GetUtcNow();
            RemoveExpiredLocked(now);
            try
            {
                return operation();
            }
            finally
            {
                ScheduleNextExpirationLocked(now);
            }
        }
    }

    private bool TryMakeCapacity()
    {
        if (_entries.Count < _capacity)
        {
            return true;
        }

        if (!_policy.TrySelectEvictionKey(_entries, out var evictionKey))
        {
            return false;
        }

        return _entries.Remove(evictionKey);
    }

    private void RemoveExpired(object? state)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();
            RemoveExpiredLocked(now);
            ScheduleNextExpirationLocked(now);
        }
    }

    private void RemoveExpiredLocked(DateTimeOffset now)
    {
        foreach (var pair in _entries.ToArray())
        {
            if (_policy.GetExpiration(pair.Value) <= now)
            {
                _entries.Remove(pair.Key);
            }
        }
    }

    private void ScheduleNextExpirationLocked(DateTimeOffset now)
    {
        DateTimeOffset? nextExpiration = null;
        foreach (var entry in _entries.Values)
        {
            var expiration = _policy.GetExpiration(entry);
            if (nextExpiration is null)
            {
                nextExpiration = expiration;
                continue;
            }

            if (expiration < nextExpiration.Value)
            {
                nextExpiration = expiration;
            }
        }

        var dueTime = Timeout.InfiniteTimeSpan;
        if (nextExpiration is not null)
        {
            var timeUntilExpiration = nextExpiration.Value - now;
            dueTime = timeUntilExpiration > TimeSpan.Zero
                ? timeUntilExpiration
                : TimeSpan.Zero;
        }

        _expirationTimer.Change(dueTime, Timeout.InfiniteTimeSpan);
    }

    private bool MarkDisposed()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return false;
            }

            _disposed = true;
            return true;
        }
    }
}
