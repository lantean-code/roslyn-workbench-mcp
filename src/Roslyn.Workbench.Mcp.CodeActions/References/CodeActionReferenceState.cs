using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.References;

/// <summary>
/// Maintains the bounded cache and invalidation indexes for temporary Code Action references.
/// </summary>
internal sealed class CodeActionReferenceState : ICodeActionReferenceState, IDisposable
{
    private const long _preparedFixAllFixedCharge = 2;
    private const long _preparedDocumentIdentityFixedCharge = 18;

    private readonly IMemoryCache _cache;
    private readonly Dictionary<Guid, ReferenceRegistration> _registrations;
    private readonly Dictionary<WorkspaceSnapshotIdentity, HashSet<Guid>> _snapshotIndex;
    private readonly Lock _syncRoot;
    private readonly TimeProvider _timeProvider;
    private readonly bool _ownsCache;
    private readonly Dictionary<WorkspaceTransactionCacheKey, HashSet<Guid>> _transactionIndex;
    private readonly Dictionary<WorkspaceInstanceCacheKey, HashSet<Guid>> _workspaceIndex;
    private long _chargedUnits;
    private long _entryCount;
    private long _largestEntryCharge;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionReferenceState"/> class.
    /// </summary>
    /// <param name="options">The reference-cache capacity options.</param>
    /// <param name="timeProvider">The time source used for expiry and timestamp calculations.</param>
    public CodeActionReferenceState(
        IOptions<CodeActionReferenceCacheOptions> options,
        TimeProvider timeProvider)
    {
        var cacheOptions = new MemoryCacheOptions
        {
            SizeLimit = options.Value.SizeLimit,
        };

        _cache = new MemoryCache(cacheOptions);
        _registrations = [];
        _snapshotIndex = [];
        _syncRoot = new Lock();
        _timeProvider = timeProvider;
        _ownsCache = true;
        _transactionIndex = [];
        _workspaceIndex = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionReferenceState"/> class.
    /// </summary>
    /// <param name="cache">The cache that retains replayable Code Action references.</param>
    /// <param name="timeProvider">The time source used for expiry and timestamp calculations.</param>
    internal CodeActionReferenceState(
        IMemoryCache cache,
        TimeProvider timeProvider)
    {
        _cache = cache;
        _registrations = [];
        _snapshotIndex = [];
        _syncRoot = new Lock();
        _timeProvider = timeProvider;
        _transactionIndex = [];
        _workspaceIndex = [];
    }

    /// <summary>
    /// Releases resources held by this instance.
    /// </summary>
    public void Dispose()
    {
        if (_ownsCache)
        {
            _cache.Dispose();
        }
    }

    /// <summary>
    /// Attempts to cache a replay recipe and create a temporary reference for it.
    /// </summary>
    /// <param name="recipe">The replay recipe used to reconstruct the Code Action.</param>
    /// <param name="expiresAt">The time after which the stored value is no longer valid.</param>
    /// <param name="reference">The created reference when the cache admits the recipe.</param>
    /// <returns><see langword="true"/> when the cache admits the reference; otherwise, <see langword="false"/>.</returns>
    public bool TryCreate(
        CodeActionReplayRecipe recipe,
        DateTimeOffset expiresAt,
        [NotNullWhen(true)] out CodeActionReference? reference)
    {
        lock (_syncRoot)
        {
            var actionId = Guid.NewGuid();
            var candidate = new CodeActionReference(actionId, recipe, expiresAt);
            var size = CalculateSize(recipe);
            var registration = new ReferenceRegistration(actionId, recipe.SnapshotIdentity, size);
            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(expiresAt)
                .SetSize(size)
                .RegisterPostEvictionCallback(
                    (_, _, reason, _) => OnEntryEvicted(registration, reason));

            var key = new CodeActionReferenceCacheKey(actionId);
            AddRegistration(registration);
            _cache.Set(key, candidate, options);
            if (_cache.TryGetValue(key, out reference) && reference is not null)
            {
                registration.IsAdmitted = true;
                OnEntryAdmitted(size);
                return true;
            }

            RecordMetric("capacity-admission-refusals", 1);
            RemoveRegistrationLocked(registration);
            reference = null;
            return false;
        }
    }

    /// <summary>
    /// Attempts to retrieve an unexpired Code Action reference.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    /// <param name="reference">The matching unexpired reference, when found.</param>
    /// <returns><see langword="true"/> when an unexpired reference exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(
        Guid actionId,
        [NotNullWhen(true)] out CodeActionReference? reference)
    {
        var key = new CodeActionReferenceCacheKey(actionId);
        lock (_syncRoot)
        {
            if (!_registrations.TryGetValue(actionId, out var registration))
            {
                RecordMetric("misses", 1);
                reference = null;
                return false;
            }

            if (!_cache.TryGetValue(key, out reference)
                || reference is null)
            {
                RecordMetric("misses", 1);
                RemoveRegistrationLocked(registration);
                reference = null;
                return false;
            }

            if (reference.ExpiresAt >= _timeProvider.GetUtcNow())
            {
                RecordMetric("hits", 1);
                RecordCurrentPressure();
                return true;
            }

            RecordMetric("expiration-evictions", 1);
            RemoveRegistrationLocked(registration);
        }

        _cache.Remove(key);
        reference = null;
        return false;
    }

    /// <summary>
    /// Removes a Code Action reference from the cache and its invalidation indexes.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    public void Remove(Guid actionId)
    {
        lock (_syncRoot)
        {
            if (_registrations.TryGetValue(actionId, out var registration))
            {
                RemoveRegistrationLocked(registration);
            }
        }

        var key = new CodeActionReferenceCacheKey(actionId);
        _cache.Remove(key);
    }

    /// <summary>
    /// Determines whether the reference identifies a prepared Fix All operation.
    /// </summary>
    /// <param name="actionId">The action identifier.</param>
    /// <returns><see langword="true"/> when the reference exists and represents a prepared Fix All operation; otherwise, <see langword="false"/>.</returns>
    public bool IsPreparedFixAll(Guid actionId)
    {
        return TryGet(actionId, out var reference)
            && reference.Recipe.PreparedFixAll is not null;
    }

    /// <summary>
    /// Invalidates references owned by the specified workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceEpoch">The workspace epoch that the operation expects.</param>
    public void InvalidateWorkspace(Guid workspaceId, long workspaceEpoch)
    {
        var workspaceKey = new WorkspaceInstanceCacheKey(workspaceId, workspaceEpoch);
        RemoveIndexedReferences(_workspaceIndex, workspaceKey);
    }

    /// <summary>
    /// Invalidates references owned by the specified transaction.
    /// </summary>
    /// <param name="workspaceId">The workspace identifier.</param>
    /// <param name="workspaceEpoch">The workspace epoch that the operation expects.</param>
    /// <param name="transactionId">The transaction identifier.</param>
    public void InvalidateTransaction(
        Guid workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId)
    {
        var transactionKey = new WorkspaceTransactionCacheKey(
            workspaceId,
            workspaceEpoch,
            transactionId);

        RemoveIndexedReferences(_transactionIndex, transactionKey);
    }

    /// <summary>
    /// Invalidates references whose workspace snapshots are stale.
    /// </summary>
    /// <param name="snapshots">The snapshot identities whose Code Action references must be invalidated.</param>
    public void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            RemoveIndexedReferences(_snapshotIndex, snapshot);
        }
    }

    private void AddRegistration(ReferenceRegistration registration)
    {
        _registrations.Add(registration.ActionId, registration);
        AddToIndex(_workspaceIndex, registration.WorkspaceKey, registration.ActionId);
        AddToIndex(_snapshotIndex, registration.SnapshotIdentity, registration.ActionId);

        if (registration.TransactionKey is not null)
        {
            AddToIndex(_transactionIndex, registration.TransactionKey, registration.ActionId);
        }
    }

    private void RemoveRegistration(ReferenceRegistration registration)
    {
        lock (_syncRoot)
        {
            RemoveRegistrationLocked(registration);
        }
    }

    private void RemoveRegistrationLocked(ReferenceRegistration registration)
    {
        if (!_registrations.TryGetValue(registration.ActionId, out var currentRegistration)
            || !ReferenceEquals(currentRegistration, registration))
        {
            return;
        }

        _registrations.Remove(registration.ActionId);
        RemoveFromIndex(_workspaceIndex, registration.WorkspaceKey, registration.ActionId);
        RemoveFromIndex(_snapshotIndex, registration.SnapshotIdentity, registration.ActionId);

        if (registration.TransactionKey is not null)
        {
            RemoveFromIndex(_transactionIndex, registration.TransactionKey, registration.ActionId);
        }
    }

    private void RemoveIndexedReferences<TKey>(
        Dictionary<TKey, HashSet<Guid>> index,
        TKey key)
        where TKey : notnull
    {
        Guid[] actionIds;
        lock (_syncRoot)
        {
            if (!index.TryGetValue(key, out var indexedActionIds))
            {
                return;
            }

            actionIds = [.. indexedActionIds];
            RecordMetric("lifecycle-evictions", actionIds.Length);
            foreach (var actionId in actionIds)
            {
                if (_registrations.TryGetValue(actionId, out var registration))
                {
                    RemoveRegistrationLocked(registration);
                }
            }
        }

        foreach (var actionId in actionIds)
        {
            var referenceKey = new CodeActionReferenceCacheKey(actionId);
            _cache.Remove(referenceKey);
        }
    }

    private void OnEntryAdmitted(long size)
    {
        var entries = Interlocked.Increment(ref _entryCount);
        var units = Interlocked.Add(ref _chargedUnits, size);
        _largestEntryCharge = Math.Max(_largestEntryCharge, size);
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

    private void OnEntryEvicted(ReferenceRegistration registration, EvictionReason reason)
    {
        lock (_syncRoot)
        {
            RemoveRegistrationLocked(registration);
            if (registration.IsAdmitted)
            {
                registration.IsAdmitted = false;
                Interlocked.Decrement(ref _entryCount);
                Interlocked.Add(ref _chargedUnits, -registration.ChargedSize);
            }
        }

        var metric = reason switch
        {
            EvictionReason.Capacity => "capacity-evictions",
            EvictionReason.Expired => "expiration-evictions",
            _ => null,
        };

        if (metric is not null)
        {
            RecordMetric(metric, 1);
        }
    }

    private static long CalculateSize(CodeActionReplayRecipe recipe)
    {
        var size = 1L
            + recipe.ProviderId.Length
            + recipe.Title.Length
            + (recipe.EquivalenceKey?.Length ?? 0)
            + 16
            + recipe.DocumentPath.Length
            + recipe.ProjectId.Length
            + recipe.ActionPath.Count
            + recipe.Diagnostics.Count
            + 3;

        foreach (var diagnosticId in recipe.DiagnosticIds)
        {
            size += diagnosticId.Length;
        }

        foreach (var diagnostic in recipe.Diagnostics)
        {
            size += diagnostic.Id.Length + diagnostic.Message.Length;
        }

        if (recipe.PreparedFixAll is not null)
        {
            size += CalculateSize(recipe.PreparedFixAll);
        }

        return size;
    }

    private static long CalculateSize(PreparedFixAllReplayData preparedFixAll)
    {
        var size = _preparedFixAllFixedCharge;
        foreach (var document in preparedFixAll.CandidatePrecondition.ExpectedIdentity.Documents)
        {
            size += _preparedDocumentIdentityFixedCharge
                + document.DocumentPath.Path.Length
                + document.ContentHash.Length
                + document.SerializedBytesHash.Length
                + document.EncodingName.Length;
        }

        return size;
    }

    private static void AddToIndex<TKey>(
        Dictionary<TKey, HashSet<Guid>> index,
        TKey key,
        Guid actionId)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var actionIds))
        {
            actionIds = [];
            index.Add(key, actionIds);
        }

        actionIds.Add(actionId);
    }

    private static void RemoveFromIndex<TKey>(
        Dictionary<TKey, HashSet<Guid>> index,
        TKey key,
        Guid actionId)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var actionIds))
        {
            return;
        }

        actionIds.Remove(actionId);
        if (actionIds.Count == 0)
        {
            index.Remove(key);
        }
    }

    private static void RecordMetric(string metric, long value)
    {
        WorkbenchPerformanceEventSource.Log.CacheMetric(
            WorkbenchPerformanceEventSource.CodeActionReferenceCacheFamily,
            metric,
            value);
    }

    private sealed record CodeActionReferenceCacheKey
    {
        private Guid ActionId { get; }

        public CodeActionReferenceCacheKey(Guid actionId)
        {
            ActionId = actionId;
        }
    }

    private sealed class ReferenceRegistration
    {
        public Guid ActionId { get; }

        public WorkspaceSnapshotIdentity SnapshotIdentity { get; }

        public WorkspaceInstanceCacheKey WorkspaceKey { get; }

        public WorkspaceTransactionCacheKey? TransactionKey { get; }

        public long ChargedSize { get; }

        public bool IsAdmitted { get; set; }

        public ReferenceRegistration(
            Guid actionId,
            WorkspaceSnapshotIdentity snapshotIdentity,
            long chargedSize)
        {
            ActionId = actionId;
            SnapshotIdentity = snapshotIdentity;
            ChargedSize = chargedSize;
            WorkspaceKey = new WorkspaceInstanceCacheKey(
                snapshotIdentity.WorkspaceId,
                snapshotIdentity.WorkspaceEpoch);

            if (snapshotIdentity.TransactionId is not null)
            {
                TransactionKey = new WorkspaceTransactionCacheKey(
                    snapshotIdentity.WorkspaceId,
                    snapshotIdentity.WorkspaceEpoch,
                    snapshotIdentity.TransactionId.Value);
            }
        }
    }

    private sealed record WorkspaceInstanceCacheKey
    {
        public Guid WorkspaceId { get; }

        public long WorkspaceEpoch { get; }

        public WorkspaceInstanceCacheKey(Guid workspaceId, long workspaceEpoch)
        {
            WorkspaceId = workspaceId;
            WorkspaceEpoch = workspaceEpoch;
        }
    }

    private sealed record WorkspaceTransactionCacheKey
    {
        public Guid WorkspaceId { get; }

        public long WorkspaceEpoch { get; }

        public WorkspaceTransactionId TransactionId { get; }

        public WorkspaceTransactionCacheKey(
            Guid workspaceId,
            long workspaceEpoch,
            WorkspaceTransactionId transactionId)
        {
            WorkspaceId = workspaceId;
            WorkspaceEpoch = workspaceEpoch;
            TransactionId = transactionId;
        }
    }
}
