using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Retention;

/// <summary>
/// Provides a thread-safe in-memory store that removes expired values and applies an eviction policy at capacity.
/// </summary>
/// <typeparam name="TKey">The type used to identify stored values.</typeparam>
/// <typeparam name="TValue">The type of value retained by the store.</typeparam>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundedExpiringStore{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="policy">The capacity, expiration and eviction rules for stored values.</param>
    /// <param name="timeProvider">The time source used for expiry and timestamp calculations.</param>
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

    /// <summary>
    /// Adds a value or replaces the value already associated with the key.
    /// </summary>
    /// <param name="key">The key under which to store the value.</param>
    /// <param name="value">The value to retain.</param>
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

    /// <summary>
    /// Attempts to add a value without replacing an existing entry.
    /// </summary>
    /// <param name="key">The key under which to store the value.</param>
    /// <param name="value">The value to retain.</param>
    /// <returns><see langword="true"/> when the value was added; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Attempts to retrieve the unexpired value associated with a key.
    /// </summary>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="value">The stored value when found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an unexpired value was found; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(
        TKey key,
        [NotNullWhen(true)] out TValue? value)
    {
        TValue? storedValue = default;
        var wasFound = Execute(() => _entries.TryGetValue(key, out storedValue));
        value = storedValue;
        return wasFound;
    }

    /// <summary>
    /// Replaces an existing unexpired value using an atomic update function.
    /// </summary>
    /// <param name="key">The key of the value to update.</param>
    /// <param name="update">The function that produces a replacement for the stored value.</param>
    /// <returns>A result containing the original and replacement values, or a not-found result when the key has no unexpired value.</returns>
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

    /// <summary>
    /// Removes the value associated with a key.
    /// </summary>
    /// <param name="key">The key of the value to remove.</param>
    /// <returns><see langword="true"/> when a value was removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(TKey key)
    {
        return Execute(() => _entries.Remove(key));
    }

    /// <summary>
    /// Stops expiration processing and releases the store's timer.
    /// </summary>
    public void Dispose()
    {
        if (MarkDisposed())
        {
            _expirationTimer.Dispose();
        }
    }

    /// <summary>
    /// Asynchronously stops expiration processing and releases the store's timer.
    /// </summary>
    /// <returns>A task that completes when the timer has been released.</returns>
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
