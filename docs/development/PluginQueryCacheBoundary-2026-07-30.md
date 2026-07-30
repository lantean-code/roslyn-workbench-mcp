# Plugin Query Cache Boundary

Date: 2026-07-30

Status: Proposed future work

## Purpose

Replace the raw plugin-facing query-cache contract with a Host-created, snapshot-bound cache scope while retaining Workspace ownership of storage, eviction and lifecycle invalidation.

This work is not required to correct `ReferenceDiscoveryService`. Its cache entries are currently protected by immutable solution identity and the shared Workspace invalidation path. The change is needed because the public cache API relies on every plugin author manually reproducing invariants that the Host can enforce.

## Current Architecture

`ReferenceDiscoveryService` and `ProjectTargetFrameworkResolver` consume `IQueryCache`. `QueryCache` owns bounded `IMemoryCache` entries with a ten-minute sliding expiration and a Workspace invalidation token. `WorkspaceSessionStore` invalidates the Workspace partition when a Workspace closes, its epoch or effective `Solution` changes, or its state becomes stale or conflicted.

Reference-discovery keys contain:

- the immutable `Solution` instance;
- semantic symbol identity; and
- the sorted selected-document IDs.

Consequently, a changed solution cannot retrieve an entry created for an older snapshot even before lifecycle eviction occurs. Workspace invalidation then releases the obsolete cache graph.

`CodeActionReferenceStore` remains a separate cache family. It stores replayable external handles and therefore requires absolute expiry, post-eviction index maintenance and precise Workspace, transaction and snapshot invalidation.

## Problem

The public `IQueryCache` contract exposes low-level storage operations through `IToolExecutionServices.QueryCache`:

- the caller supplies the Workspace ID;
- the caller must include the immutable solution and every semantic input in its key;
- keys are not automatically partitioned by plugin or tool identity;
- entry size is an arbitrary caller-provided value;
- separate `TryGet` and `Store` operations allow identical concurrent requests to duplicate expensive work; and
- a store performed after invalidation is not rejected by a captured snapshot generation.

Documentation can describe these requirements but cannot guarantee that third-party plugins follow them. Equal keys from different plugins can also share one Workspace partition unintentionally.

The query cache and Code Action reference store currently use the same configured `IMemoryCache`. This is semantically safe because both use sized entries and the reference store maintains eviction callbacks, but it couples two cache families to one capacity budget without an explicit product decision.

## Proposed Boundary

Use separate internal storage and public consumption contracts.

| Responsibility | Proposed owner | Contract |
| --- | --- | --- |
| Memory storage, bounds, expiry and invalidation generations | Workspace | Internal `IWorkspaceQueryCacheStore` |
| Workspace close, reload, solution replacement and stale-state invalidation | Workspace lifecycle | Internal invalidation contract |
| Snapshot-bound plugin cache access | Plugin execution context | Public `IQueryResultCache` |
| Replayable Code Action handles | CodeActions | Existing specialised reference store |
| Process-lifetime schema and reflection metadata | Host components | Dedicated component-local caches |

`IQueryResultCache` should be exposed on `IQueryContext`, not on singleton `IToolExecutionServices`, because its identity and lifetime semantics belong to the effective invocation snapshot.

The Host-created scope must capture:

- Workspace ID and epoch;
- current Workspace snapshot or immutable solution identity;
- plugin and tool identity;
- the current invalidation generation; and
- the applicable Host-owned cache policy.

Plugins must not supply or override these values.

## Illustrative Public Contract

The final API should be selected during implementation, but its shape should resemble:

```csharp
public interface IQueryResultCache
{
    ValueTask<TValue> GetOrCreateAsync<TValue>(
        object key,
        QueryCacheEntryCost cost,
        Func<CancellationToken, ValueTask<TValue>> valueFactory,
        CancellationToken cancellationToken)
        where TValue : class;
}
```

`GetOrCreateAsync` must:

- return a value only from the current plugin, tool and snapshot scope;
- store only successfully completed values;
- never store failed or cancelled operations;
- reject a late store when the captured invalidation generation is no longer current; and
- define whether concurrent identical misses are coalesced.

The cost model remains an implementation decision. It must be documented, range-validated and consistently applicable to arbitrary plugin-owned objects. If trustworthy relative sizing cannot be defined, prefer a Host-owned fixed entry charge plus a bounded optional weight rather than presenting caller-supplied numbers as precise memory accounting.

## Cache-Family Policy

Do not force all caches through one abstraction. Cache invalidation must match the semantics of the stored value:

- immutable snapshot query results use snapshot-qualified keys and coarse Workspace invalidation;
- replayable handles use exact Workspace, transaction and snapshot invalidation;
- process-lifetime metadata uses type or configuration identity and does not observe Workspace lifecycle events; and
- invocation-local dictionaries remain local and require no shared eviction system.

Decide explicitly whether query results and replayable Code Action references retain a shared memory budget or receive separately configured caches. Separate budgets provide isolation; a shared budget provides one process-wide bound but permits one family to evict the other.

## Migration

1. Define the internal Workspace store, invalidation generation and snapshot-bound scope.
2. Define `IQueryResultCache` in Abstractions and expose it through `IQueryContext`.
3. Pass stable plugin and tool identity from the Host transport adapter when constructing the query context.
4. Migrate `ReferenceDiscoveryService`, `ProjectTargetFrameworkResolver` and any other persistent query-cache consumers.
5. Remove `IToolExecutionServices.QueryCache`.
6. Remove the current public `IQueryCache` contract from Abstractions and update public API locks.
7. Update plugin-authoring documentation and composition tests.
8. Decide and implement cache-family budget isolation.

Because the current cache API has not been released as a stable v1 package contract, make this migration before treating that surface as compatibility-locked.

## Required Validation

Add focused tests proving:

- entries are isolated by Workspace, epoch, snapshot, plugin and tool;
- stable state transitions over the same immutable solution retain valid entries;
- staging, undo, redo, rollback, reload and changed-solution replacement cannot return stale entries;
- Workspace close and stale/conflicted transitions invalidate all relevant query entries;
- a computation completing after invalidation cannot insert an entry into the new generation;
- failed and cancelled factories are never cached;
- size and expiration policies evict entries as documented;
- concurrent identical misses follow the selected coalescing policy;
- cache eviction releases retained Roslyn solution, symbol and document graphs; and
- Code Action reference eviction remains correct under the selected shared or separate budget policy.

Use component tests across the real Workspace session store and cache implementation rather than relying only on mocked invalidation and storage seams.

## Non-Goals

- Do not make plugins responsible for Workspace lifecycle events.
- Do not expose cache invalidation to plugin authors.
- Do not reinterpret stale results against a newer Workspace snapshot.
- Do not combine Code Action replay references with ordinary query-result caching.
- Do not add caching to a query without measured reuse and an explicit key, cost and invalidation design.
