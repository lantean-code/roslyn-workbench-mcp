using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Caching.Memory;

namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal sealed class CodeActionReferenceStore : ICodeActionReferenceStore, IWorkspaceSnapshotLifecycleObserver
{
    private readonly IMemoryCache _cache;
    private readonly Dictionary<Guid, ReferenceRegistration> _registrations;
    private readonly Dictionary<WorkspaceSnapshotIdentity, HashSet<Guid>> _snapshotIndex;
    private readonly Lock _syncRoot;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<WorkspaceTransactionCacheKey, HashSet<Guid>> _transactionIndex;
    private readonly Dictionary<WorkspaceInstanceCacheKey, HashSet<Guid>> _workspaceIndex;

    public CodeActionReferenceStore(IMemoryCache cache, TimeProvider timeProvider)
    {
        _cache = cache;
        _registrations = [];
        _snapshotIndex = [];
        _syncRoot = new Lock();
        _timeProvider = timeProvider;
        _transactionIndex = [];
        _workspaceIndex = [];
    }

    public bool TryCreate(
        CodeActionReplayRecipe recipe,
        DateTimeOffset expiresAt,
        [NotNullWhen(true)] out CodeActionReference? reference)
    {
        var actionId = Guid.NewGuid();
        var candidate = new CodeActionReference(actionId, recipe, expiresAt);
        var registration = new ReferenceRegistration(actionId, recipe.SnapshotIdentity);
        var options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(expiresAt)
            .SetSize(CalculateSize(recipe))
            .RegisterPostEvictionCallback(
                (_, _, _, _) => RemoveRegistration(registration));

        var key = new CodeActionReferenceCacheKey(actionId);
        lock (_syncRoot)
        {
            AddRegistration(registration);
            _cache.Set(key, candidate, options);
            if (_cache.TryGetValue(key, out reference) && reference is not null)
            {
                return true;
            }

            RemoveRegistrationLocked(registration);
            reference = null;
            return false;
        }
    }

    public bool TryGet(
        Guid actionId,
        [NotNullWhen(true)] out CodeActionReference? reference)
    {
        var key = new CodeActionReferenceCacheKey(actionId);
        lock (_syncRoot)
        {
            if (!_registrations.TryGetValue(actionId, out var registration))
            {
                reference = null;
                return false;
            }

            if (!_cache.TryGetValue(key, out reference)
                || reference is null)
            {
                RemoveRegistrationLocked(registration);
                reference = null;
                return false;
            }

            if (reference.ExpiresAt >= _timeProvider.GetUtcNow())
            {
                return true;
            }

            RemoveRegistrationLocked(registration);
        }

        _cache.Remove(key);
        reference = null;
        return false;
    }

    public void Remove(Guid actionId)
    {
        lock (_syncRoot)
        {
            if (_registrations.TryGetValue(actionId, out var registration))
            {
                RemoveRegistrationLocked(registration);
            }
        }

        _cache.Remove(new CodeActionReferenceCacheKey(actionId));
    }

    public void InvalidateWorkspace(string workspaceId, long workspaceEpoch)
    {
        var workspaceKey = new WorkspaceInstanceCacheKey(workspaceId, workspaceEpoch);
        RemoveIndexedReferences(_workspaceIndex, workspaceKey);
    }

    public void InvalidateTransaction(
        string workspaceId,
        long workspaceEpoch,
        WorkspaceTransactionId transactionId)
    {
        var transactionKey = new WorkspaceTransactionCacheKey(
            workspaceId,
            workspaceEpoch,
            transactionId);

        RemoveIndexedReferences(_transactionIndex, transactionKey);
    }

    public void InvalidateSnapshots(IReadOnlyList<WorkspaceSnapshotIdentity> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            RemoveIndexedReferences(_snapshotIndex, snapshot);
        }
    }

    private static long CalculateSize(CodeActionReplayRecipe recipe)
    {
        var size = 1L
            + recipe.ProviderId.Length
            + recipe.Title.Length
            + (recipe.EquivalenceKey?.Length ?? 0)
            + (recipe.SnapshotIdentity.WorkspaceId?.Length ?? 0)
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

        return size;
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
            _cache.Remove(new CodeActionReferenceCacheKey(actionId));
        }
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

        public ReferenceRegistration(
            Guid actionId,
            WorkspaceSnapshotIdentity snapshotIdentity)
        {
            ActionId = actionId;
            SnapshotIdentity = snapshotIdentity;
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
        public string WorkspaceId { get; }

        public long WorkspaceEpoch { get; }

        public WorkspaceInstanceCacheKey(string workspaceId, long workspaceEpoch)
        {
            WorkspaceId = workspaceId;
            WorkspaceEpoch = workspaceEpoch;
        }
    }

    private sealed record WorkspaceTransactionCacheKey
    {
        public string WorkspaceId { get; }

        public long WorkspaceEpoch { get; }

        public WorkspaceTransactionId TransactionId { get; }

        public WorkspaceTransactionCacheKey(
            string workspaceId,
            long workspaceEpoch,
            WorkspaceTransactionId transactionId)
        {
            WorkspaceId = workspaceId;
            WorkspaceEpoch = workspaceEpoch;
            TransactionId = transactionId;
        }
    }
}
