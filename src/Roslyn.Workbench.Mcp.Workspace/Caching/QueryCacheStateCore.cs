using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal sealed class QueryCacheStateCore : IDisposable
{
    private static readonly AsyncLocal<HashSet<QueryCacheEntryIdentity>?> _executingFactories = new();

    private readonly MemoryCache _cache;
    private readonly Dictionary<QueryCacheEntryIdentity, InFlightQueryComputation> _inFlightComputations;
    private readonly Dictionary<object, PartitionState> _partitions;
    private readonly string _cacheFamily;
    private readonly long _sizeLimit;
    private readonly TimeSpan _slidingExpiration;
    private readonly CancellationToken _stoppingToken;
    private readonly Lock _syncRoot;
    private bool _disposed;
    private long _entryCount;
    private long _chargedUnits;
    private long _largestEntryCharge;

    public QueryCacheStateCore(
        long sizeLimit,
        TimeSpan slidingExpiration,
        IHostApplicationLifetime applicationLifetime,
        string cacheFamily)
    {
        var cacheOptions = new MemoryCacheOptions
        {
            SizeLimit = sizeLimit,
        };

        _cache = new MemoryCache(cacheOptions);
        _inFlightComputations = [];
        _partitions = [];
        _cacheFamily = cacheFamily;
        _sizeLimit = sizeLimit;
        _slidingExpiration = slidingExpiration;
        _stoppingToken = applicationLifetime.ApplicationStopping;
        _syncRoot = new Lock();
    }

    public QueryCacheScopeIdentity CreateScope(object partition, object scope)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_partitions.TryGetValue(partition, out var partitionState))
            {
                partitionState = new PartitionState(partition);
                _partitions.Add(partition, partitionState);
            }

            return new QueryCacheScopeIdentity(partitionState.Generation, scope);
        }
    }

    public TValue? GetOrCreate<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        Func<TValue, long> sizeCalculator,
        Func<TValue, bool> admissionPredicate,
        CancellationToken cancellationToken)
        where TKey : notnull
    {
        var entryIdentity = CreateEntryIdentity<TKey, TValue>(scopeIdentity, key);
        var computation = GetOrStartComputation(
            entryIdentity,
            scopeIdentity,
            token => new ValueTask<object?>(valueFactory(token)),
            value => sizeCalculator((TValue)value),
            value => SelectCacheValue(value, admissionPredicate),
            cancellationToken,
            out var cachedValue);

        if (computation is null)
        {
            return (TValue?)cachedValue;
        }

        try
        {
            var result = computation.ResultTask.WaitAsync(cancellationToken).GetAwaiter().GetResult();
            return (TValue?)result;
        }
        finally
        {
            ReleaseWaiter(computation);
        }
    }

    public async ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, ValueTask<TValue?>> valueFactory,
        Func<TValue, long> sizeCalculator,
        Func<TValue, bool> admissionPredicate,
        CancellationToken cancellationToken)
        where TKey : notnull
    {
        var entryIdentity = CreateEntryIdentity<TKey, TValue>(scopeIdentity, key);
        var computation = GetOrStartComputation(
            entryIdentity,
            scopeIdentity,
            async token => await valueFactory(token),
            value => sizeCalculator((TValue)value),
            value => SelectCacheValue(value, admissionPredicate),
            cancellationToken,
            out var cachedValue);

        if (computation is null)
        {
            return (TValue?)cachedValue;
        }

        try
        {
            var result = await computation.ResultTask.WaitAsync(cancellationToken);
            return (TValue?)result;
        }
        finally
        {
            ReleaseWaiter(computation);
        }
    }

    public TResult GetOrCreateProjected<TKey, TValue, TResult>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        Func<CancellationToken, TResult> resultFactory,
        Func<TResult, TValue?> cacheValueSelector,
        Func<TValue, TResult> cachedResultSelector,
        Func<TValue, long> sizeCalculator,
        CancellationToken cancellationToken)
        where TKey : notnull
        where TValue : notnull
        where TResult : notnull
    {
        var entryIdentity = CreateEntryIdentity<TKey, TValue>(scopeIdentity, key);
        var computation = GetOrStartComputation(
            entryIdentity,
            scopeIdentity,
            token => new ValueTask<object?>(resultFactory(token)),
            value => sizeCalculator((TValue)value),
            result => cacheValueSelector((TResult)result),
            cancellationToken,
            out var cachedValue);

        if (computation is null)
        {
            if (cachedValue is not TValue typedCachedValue)
            {
                throw new InvalidOperationException(
                    "The query cache returned an unexpected projected cache value.");
            }

            return cachedResultSelector(typedCachedValue);
        }

        try
        {
            var result = computation.ResultTask.WaitAsync(cancellationToken).GetAwaiter().GetResult();
            if (result is not TResult typedResult)
            {
                throw new InvalidOperationException(
                    "The query-cache projected value factory returned an unexpected null result.");
            }

            return typedResult;
        }
        finally
        {
            ReleaseWaiter(computation);
        }
    }

    public bool TryGet<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        out TValue? value)
        where TKey : notnull
    {
        var entryIdentity = CreateEntryIdentity<TKey, TValue>(scopeIdentity, key);
        ThrowIfRecursive(entryIdentity);

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        if (_cache.TryGetValue(entryIdentity, out var cachedValue))
        {
            RecordMetric("hits", 1);
            RecordCurrentPressure();
            value = (TValue?)cachedValue;
            return true;
        }

        RecordMetric("misses", 1);
        value = default;
        return false;
    }

    public void Store<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key,
        TValue value,
        Func<TValue, long> sizeCalculator)
        where TKey : notnull
        where TValue : notnull
    {
        var entryIdentity = CreateEntryIdentity<TKey, TValue>(scopeIdentity, key);
        if (!IsCurrentGeneration(scopeIdentity.Generation))
        {
            RecordMetric("late-store-rejections", 1);
            return;
        }

        var size = sizeCalculator(value);
        AdmitValue(
            entryIdentity,
            value,
            size,
            scopeIdentity.Generation.InvalidationToken);
    }

    public void InvalidatePartition(object partition)
    {
        CancellationTokenSource? invalidationSource;
        lock (_syncRoot)
        {
            var partitionState = _partitions.GetValueOrDefault(partition);
            invalidationSource = partitionState?.InvalidationSource;
            _partitions.Remove(partition);
        }

        if (invalidationSource is null)
        {
            return;
        }

        RecordMetric("lifecycle-invalidations", 1);
        invalidationSource.Cancel();
        invalidationSource.Dispose();
    }

    public void Dispose()
    {
        CancellationTokenSource[] invalidationSources;
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            invalidationSources = [.. _partitions.Values.Select(static state => state.InvalidationSource)];
            _partitions.Clear();
        }

        foreach (var invalidationSource in invalidationSources)
        {
            invalidationSource.Cancel();
            invalidationSource.Dispose();
        }

        _cache.Dispose();
    }

    private InFlightQueryComputation? GetOrStartComputation(
        QueryCacheEntryIdentity entryIdentity,
        QueryCacheScopeIdentity scopeIdentity,
        Func<CancellationToken, ValueTask<object?>> resultFactory,
        Func<object, long> sizeCalculator,
        Func<object, object?> cacheValueSelector,
        CancellationToken cancellationToken,
        out object? cachedValue)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfRecursive(entryIdentity);

        if (_cache.TryGetValue(entryIdentity, out cachedValue))
        {
            RecordMetric("hits", 1);
            RecordCurrentPressure();
            return null;
        }

        RecordMetric("misses", 1);
        InFlightQueryComputation? computation;
        var startsComputation = false;
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_cache.TryGetValue(entryIdentity, out cachedValue))
            {
                RecordMetric("hits", 1);
                RecordCurrentPressure();
                return null;
            }

            if (!_inFlightComputations.TryGetValue(entryIdentity, out computation))
            {
                var computationSource = CancellationTokenSource.CreateLinkedTokenSource(
                    scopeIdentity.Generation.InvalidationToken,
                    _stoppingToken);

                computation = new InFlightQueryComputation(entryIdentity, computationSource);
                _inFlightComputations.Add(entryIdentity, computation);
                startsComputation = true;
            }
            else
            {
                RecordMetric("in-flight-joins", 1);
            }

            computation.AddWaiter();
        }

        if (computation is null)
        {
            throw new InvalidOperationException(
                "The query-cache in-flight computation was unexpectedly unavailable.");
        }

        if (startsComputation)
        {
            var executionTask = RunFactoryAsync(
                computation,
                scopeIdentity,
                resultFactory,
                sizeCalculator,
                cacheValueSelector);

            computation.SetExecutionTask(executionTask);
        }

        cachedValue = null;
        return computation;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The cache must complete every shared waiter with any exception raised by a plugin or Host value factory.")]
    private async Task RunFactoryAsync(
        InFlightQueryComputation computation,
        QueryCacheScopeIdentity scopeIdentity,
        Func<CancellationToken, ValueTask<object?>> resultFactory,
        Func<object, long> sizeCalculator,
        Func<object, object?> cacheValueSelector)
    {
        var executingFactories = _executingFactories.Value;
        if (executingFactories is null)
        {
            executingFactories = [];
            _executingFactories.Value = executingFactories;
        }

        executingFactories.Add(computation.EntryIdentity);
        try
        {
            var result = await resultFactory(computation.ComputationSource.Token);
            object? cacheValue = null;
            if (result is not null)
            {
                cacheValue = cacheValueSelector(result);
            }

            if (cacheValue is not null)
            {
                if (IsCurrentGeneration(scopeIdentity.Generation))
                {
                    var size = sizeCalculator(cacheValue);
                    AdmitValue(
                        computation.EntryIdentity,
                        cacheValue,
                        size,
                        scopeIdentity.Generation.InvalidationToken);
                }
                else
                {
                    RecordMetric("late-store-rejections", 1);
                }
            }

            computation.SetResult(result);
        }
        catch (OperationCanceledException) when (computation.ComputationSource.IsCancellationRequested)
        {
            RecordMetric("factory-cancellations", 1);
            computation.SetCanceled(computation.ComputationSource.Token);
        }
        catch (Exception exception)
        {
            RecordMetric("factory-failures", 1);
            computation.SetException(exception);
        }
        finally
        {
            executingFactories.Remove(computation.EntryIdentity);
            if (executingFactories.Count == 0)
            {
                _executingFactories.Value = null;
            }

            lock (_syncRoot)
            {
                _inFlightComputations.Remove(computation.EntryIdentity);
            }

            computation.ComputationSource.Dispose();
        }
    }

    private static object? SelectCacheValue<TValue>(
        object value,
        Func<TValue, bool> admissionPredicate)
    {
        if (!admissionPredicate((TValue)value))
        {
            return null;
        }

        return value;
    }

    private bool IsCurrentGeneration(QueryCacheGeneration generation)
    {
        lock (_syncRoot)
        {
            return !_disposed
                && _partitions.TryGetValue(generation.Partition, out var partitionState)
                && ReferenceEquals(partitionState.Generation, generation);
        }
    }

    private void AdmitValue(
        QueryCacheEntryIdentity entryIdentity,
        object value,
        long size,
        CancellationToken invalidationToken)
    {
        if (size <= 0 || size > _sizeLimit)
        {
            RecordMetric("capacity-admission-refusals", 1);
            return;
        }

        var expirationToken = new CancellationChangeToken(invalidationToken);
        var registration = new QueryCacheEntryRegistration(size);
        var options = new MemoryCacheEntryOptions()
            .SetSize(size)
            .SetSlidingExpiration(_slidingExpiration)
            .AddExpirationToken(expirationToken)
            .RegisterPostEvictionCallback(
                (_, _, reason, _) => OnEntryEvicted(registration, reason));

        _cache.Set(entryIdentity, value, options);
        if (_cache.TryGetValue(entryIdentity, out _)
            && registration.TryAdmit())
        {
            OnEntryAdmitted(size);
            return;
        }

        RecordMetric("capacity-admission-refusals", 1);
    }

    private void ReleaseWaiter(InFlightQueryComputation computation)
    {
        lock (_syncRoot)
        {
            if (computation.RemoveWaiter() == 0
                && !computation.ResultTask.IsCompleted)
            {
                computation.ComputationSource.Cancel();
            }
        }
    }

    private static QueryCacheEntryIdentity CreateEntryIdentity<TKey, TValue>(
        QueryCacheScopeIdentity scopeIdentity,
        TKey key)
        where TKey : notnull
    {
        return new QueryCacheEntryIdentity(
            scopeIdentity,
            typeof(TKey),
            typeof(TValue),
            key);
    }

    private static void ThrowIfRecursive(QueryCacheEntryIdentity entryIdentity)
    {
        if (_executingFactories.Value?.Contains(entryIdentity) == true)
        {
            throw new InvalidOperationException(
                "A query-cache value factory cannot recursively request its own scoped key.");
        }
    }

    private void OnEntryAdmitted(long size)
    {
        var entries = Interlocked.Increment(ref _entryCount);
        var units = Interlocked.Add(ref _chargedUnits, size);
        UpdateMaximum(ref _largestEntryCharge, size);
        RecordMetric("admissions", 1);
        RecordMetric("peak-entry-count", entries);
        RecordMetric("peak-charged-units", units);
        RecordMetric("largest-entry-charge", size);
    }

    private void RecordCurrentPressure()
    {
        RecordMetric("peak-entry-count", Volatile.Read(ref _entryCount));
        RecordMetric("peak-charged-units", Volatile.Read(ref _chargedUnits));
        RecordMetric("largest-entry-charge", Volatile.Read(ref _largestEntryCharge));
    }

    private static void UpdateMaximum(ref long target, long candidate)
    {
        var current = Volatile.Read(ref target);
        while (candidate > current)
        {
            var observed = Interlocked.CompareExchange(ref target, candidate, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    private void OnEntryEvicted(
        QueryCacheEntryRegistration registration,
        EvictionReason reason)
    {
        if (registration.TryEvictAdmitted())
        {
            Interlocked.Decrement(ref _entryCount);
            Interlocked.Add(ref _chargedUnits, -registration.Size);
        }

        var metric = reason switch
        {
            EvictionReason.Capacity => "capacity-evictions",
            EvictionReason.Expired => "expiration-evictions",
            EvictionReason.TokenExpired => "lifecycle-evictions",
            _ => null,
        };

        if (metric is not null)
        {
            RecordMetric(metric, 1);
        }
    }

    private void RecordMetric(string metric, long value)
    {
        WorkbenchPerformanceEventSource.Log.CacheMetric(_cacheFamily, metric, value);
    }

    private sealed class InFlightQueryComputation
    {
        private readonly TaskCompletionSource<object?> _resultSource;
        private Task? _executionTask;
        private int _waiterCount;

        public QueryCacheEntryIdentity EntryIdentity { get; }

        public CancellationTokenSource ComputationSource { get; }

        public Task<object?> ResultTask => _resultSource.Task;

        public InFlightQueryComputation(
            QueryCacheEntryIdentity entryIdentity,
            CancellationTokenSource computationSource)
        {
            EntryIdentity = entryIdentity;
            ComputationSource = computationSource;
            _resultSource = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void AddWaiter()
        {
            _waiterCount++;
        }

        public int RemoveWaiter()
        {
            return --_waiterCount;
        }

        public void SetExecutionTask(Task executionTask)
        {
            _executionTask = executionTask;
        }

        public void SetResult(object? value)
        {
            _resultSource.TrySetResult(value);
        }

        public void SetCanceled(CancellationToken cancellationToken)
        {
            _resultSource.TrySetCanceled(cancellationToken);
        }

        public void SetException(Exception exception)
        {
            _resultSource.TrySetException(exception);
        }
    }

    private sealed class QueryCacheEntryRegistration
    {
        private int _state;

        public long Size { get; }

        public QueryCacheEntryRegistration(long size)
        {
            Size = size;
        }

        public bool TryAdmit()
        {
            return Interlocked.CompareExchange(ref _state, 1, 0) == 0;
        }

        public bool TryEvictAdmitted()
        {
            return Interlocked.Exchange(ref _state, 2) == 1;
        }
    }

    private sealed class PartitionState
    {
        public CancellationTokenSource InvalidationSource { get; }

        public QueryCacheGeneration Generation { get; }

        public PartitionState(object partition)
        {
            InvalidationSource = new CancellationTokenSource();
            Generation = new QueryCacheGeneration(partition, InvalidationSource.Token);
        }
    }
}
