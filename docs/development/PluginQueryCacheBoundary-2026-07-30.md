# Plugin Query Cache Boundary

Date: 2026-07-30

Status: Complete

## Purpose

Replace the raw plugin-facing query-cache contract with a Host-created cache bound to the current query invocation, while retaining Workspace ownership of storage, eviction, coalescing and lifecycle invalidation.

Complete the related cache-composition work in the same change: physically isolate Workspace query results, plugin query results and replayable Code Action references; place every shared state implementation behind one internal interface; and remove registrations that expose one concrete instance through multiple resolving delegates.

This work is not required to correct `ReferenceDiscoveryService`. Its current entries are protected by immutable solution identity and Workspace invalidation. The migration is required before v1 because the public cache API currently relies on plugin authors manually reproducing invariants that the Host can enforce.

## Goals

- Make stale or cross-plugin cache reuse impossible through the supported public API.
- Keep plugin cache usage small and intentional: two get-or-create operations, dedicated semantic keys and no lifecycle controls.
- Coalesce identical concurrent misses without allowing one caller's cancellation to disrupt other callers.
- Preserve the current solution-based invalidation semantics for Host-owned Workspace query services.
- Use stricter exact-snapshot invalidation for plugin-owned query results.
- Prevent plugin results from evicting trusted Workspace results or replayable Code Action handles.
- Make cache capacity and effectiveness measurable through permanent scenario-runner evidence.
- Retain query correctness when an entry cannot be admitted, expires or is invalidated before storage.

## Non-Goals

- Do not expose cache invalidation, generations, storage or eviction controls to plugin authors.
- Do not make mutation handlers cache consumers.
- Do not reinterpret stale results against a newer Workspace snapshot.
- Do not combine replayable Code Action handles with ordinary query-result caching.
- Do not add caching to a query without measured reuse and an explicit key and value design.
- Do not publish cache settings or utilisation through `server-status`; they are not runtime decisions for an agent.
- Do not redesign component-local process-lifetime caches such as schema or reflection metadata.

## Current Architecture

`ReferenceDiscoveryService` and `ProjectTargetFrameworkResolver` consume the public `IQueryCache`. `QueryCache` stores sized entries in the application `IMemoryCache` with a 50,000-unit shared limit, ten-minute sliding expiration and per-Workspace invalidation token. `WorkspaceSessionStore` invalidates the Workspace partition when a Workspace closes, its epoch or effective `Solution` changes, or its state becomes stale or conflicted.

Reference-discovery keys contain the immutable `Solution` instance, semantic symbol identity and sorted selected-document IDs. Target-framework keys contain the immutable `Solution` instance and project path. Consequently, a changed solution cannot retrieve an older entry even before lifecycle eviction occurs.

`CodeActionReferenceStore` uses the same `IMemoryCache`. It stores replayable external handles with absolute expiry, post-eviction index maintenance and exact Workspace, transaction and snapshot invalidation.

The current DI composition also registers shared concrete state and resolves that same concrete through multiple interface delegates:

- `WorkspaceQueryCacheState` is registered concretely and then exposed as `IQueryCacheInvalidationTokenSource` and `IWorkspaceQueryCacheState`.
- `CodeActionReferenceStore` is registered concretely and then exposed as `ICodeActionReferenceStore` and `IWorkspaceSnapshotLifecycleObserver`.

The factory delegates preserve singleton identity, but the pattern signals that each concrete carries shared state and more than one service role.

## Problems

The public `IQueryCache` contract is exposed through singleton `IToolExecutionServices.QueryCache` and requires every caller to supply low-level identity and storage policy:

- the caller supplies the Workspace ID;
- the caller must include the immutable solution and every semantic input in its key;
- keys are not automatically partitioned by plugin or tool identity;
- entry size is an arbitrary caller-provided number;
- separate `TryGet` and `Store` operations allow identical concurrent requests to duplicate expensive work;
- a store completing after invalidation is not rejected by a captured generation; and
- a retained singleton cache can be used outside the query invocation and Workspace lease.

Documentation cannot guarantee those invariants. Equal keys from different plugins can share a Workspace partition unintentionally, a mutable key can become unreachable or collide, and a plugin can cache mutable or resource-owning values whose lifetime the Host cannot safely manage.

The shared `IMemoryCache` also couples three semantically different families. Query pressure can evict replayable handles, plugin entries can displace trusted Host computations, and every consumer must use one compatible unit system.

## Agreed Cache Families

Use three physically separate cache instances and capacity budgets.

| Family | Consumers | Scope and invalidation | Capacity unit |
| --- | --- | --- | --- |
| Workspace query results | Host-owned services such as reference discovery and target-framework resolution | Workspace epoch and immutable solution identity, retaining current semantic invalidation behaviour | Host-calculated relative retained-result units |
| Plugin query results | Query handlers through `IQueryContext.QueryResultCache` | Exact Workspace snapshot, plugin ID and tool name | Entry count; every admitted value costs one |
| Code Action references | Code Action discovery, resolution and staging | Existing Workspace, transaction and snapshot lifecycle plus contractual absolute expiry | Existing replay-recipe size calculation |

Physical isolation is required. Cache-entry priority inside one `IMemoryCache` is insufficient because it does not reserve capacity or prevent one family filling the shared store.

Workspace and plugin query invalidation remains coordinated by Workspace lifecycle code, but the stores do not share memory capacity. Error diagnostics, prepared reports and process-lifetime metadata remain separate families outside this proposal.

## Composition and Registration

Shared state must be registered once behind an internal interface and injected into separate role-specific services. Do not register the state concrete, and do not expose one concrete implementation under multiple service contracts through resolving delegates.

The intended query-cache composition resembles:

```csharp
services.AddSingleton<IWorkspaceQueryCacheState, WorkspaceQueryCacheState>();
services.AddSingleton<IWorkspaceQueryCacheStore, WorkspaceQueryCacheStore>();
services.AddSingleton<IPluginQueryCacheState, PluginQueryCacheState>();
services.AddSingleton<IPluginQueryCacheStore, PluginQueryCacheStore>();
services.AddSingleton<IWorkspaceQueryCacheInvalidator, WorkspaceQueryCacheInvalidator>();
services.AddSingleton<IQueryResultCacheScopeFactory, QueryResultCacheScopeFactory>();
```

`IWorkspaceQueryCacheState` and `IPluginQueryCacheState` each own their dedicated cache, generation state and atomic in-flight state. The invalidator coordinates both query states without making either public or combining their capacity.

Apply the same rule to the Code Action cache while preserving its behaviour:

```csharp
services.AddSingleton<ICodeActionReferenceState, CodeActionReferenceState>();
services.AddSingleton<ICodeActionReferenceStore, CodeActionReferenceStore>();
services.AddSingleton<IWorkspaceSnapshotLifecycleObserver, CodeActionReferenceLifecycleObserver>();
```

`ICodeActionReferenceState` owns the dedicated cache, indexes and atomic registrations. `ICodeActionReferenceStore` exposes creation, lookup and removal. `CodeActionReferenceLifecycleObserver` handles Workspace, transaction and snapshot invalidation.

The concrete state implementations remain internal implementation details and are disposed by DI through their interface registrations.

## Public Plugin Contract

Expose the cache directly on `IQueryContext`:

```csharp
public interface IQueryContext : IToolExecutionContext
{
    IQueryResultCache QueryResultCache { get; }
}
```

Do not expose it on `IToolExecutionServices`, mutation contexts or plugin configuration. Each instance is created by the Host for one query invocation.

Require an explicit semantic key marker:

```csharp
public interface IQueryResultCacheKey
{
}
```

The public cache exposes only synchronous and asynchronous get-or-create operations:

```csharp
public interface IQueryResultCache
{
    TValue? GetOrCreate<TKey, TValue>(
        TKey key,
        Func<CancellationToken, TValue?> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class, IQueryResultCacheKey
        where TValue : notnull;

    ValueTask<TValue?> GetOrCreateAsync<TKey, TValue>(
        TKey key,
        Func<CancellationToken, ValueTask<TValue?>> valueFactory,
        CancellationToken cancellationToken)
        where TKey : class, IQueryResultCacheKey
        where TValue : notnull;
}
```

There is no public `TryGet`, `Store`, `Remove`, `Clear`, invalidation or generation operation.

The synchronous overload supports genuinely synchronous computations. The asynchronous overload supports Roslyn and other naturally asynchronous computations without blocking query threads. Both forms use the same entries and in-flight registry, so an identical synchronous and asynchronous request coalesces.

## Key Contract

The Host-owned composite identity contains:

- Workspace ID and epoch;
- exact Workspace snapshot identity;
- plugin ID;
- tool name;
- key runtime type;
- cached value type; and
- the plugin key value.

The plugin key contains only semantic inputs that distinguish results within that tool. It must be a named immutable reference type with stable value equality and hash code, normally a sealed record class. Bare framework reference types such as `string`, `object`, arrays and mutable collections are not supported as keys; a string identifier belongs in a property of a dedicated key.

The marker and `class` constraint provide compile-time opt-in and reject scalar, tuple and record-struct keys. The authoring analyser validates structural safety because the type system cannot prove immutability, equality correctness or semantic completeness.

An example key is:

```csharp
internal sealed record SymbolQueryCacheKey : IQueryResultCacheKey
{
    public required string SymbolId { get; init; }

    public required bool IncludeDefinitions { get; init; }
}
```

## Value Contract and Admission

Cached values may be any non-null immutable reference or value type. The same stored instance can be returned to multiple callers, so reference values must be treated as immutable.

A factory returning `null` denotes a completed but non-cacheable result. The `null` value is returned to all current coalesced callers and is not retained; a later request recomputes it. Immutable value types remain supported but cannot use `null` as the no-cache signal.

A value implementing `IDisposable` or `IAsyncDisposable` is also returned but never cached. The Host does not dispose it because ownership passes to the plugin caller. Retaining it would either return a disposed shared instance after one caller disposes it or require unsafe eviction-time ownership.

Every other normal factory return is semantically cacheable. The Host does not interpret plugin-owned success or failure envelopes. Plugin authors must not return a transient failure object from a cache factory unless they intend that value to be reused.

Admission is best-effort:

- a value is returned even when capacity refuses it;
- a value is returned to callers already operating on the captured snapshot when invalidation prevents a late store;
- failure to admit is not exposed through the public API; and
- query correctness must never depend on cache retention.

Exceptions and cancellations are observed by all current waiters but are never cached.

## Scope Lifetime

The public cache scope is valid only for its originating query invocation and Workspace lease. When the invocation ends, the scope becomes inactive. Any later call through a retained scope fails immediately with a clear `InvalidOperationException` and cannot run a factory or read retained entries.

The underlying entries remain available to new valid invocation scopes with matching identities. This prevents a plugin from escaping the Workspace operation gate by retaining a context or cache reference.

## Workspace Internal Contract

Host-owned Workspace services use an internal component-bound scope rather than the plugin scope. Its identity captures:

- Workspace ID and epoch;
- immutable solution identity;
- Host component identity; and
- the component-owned key and value types.

Host keys implement an internal `IWorkspaceQueryCacheKey` marker and are immutable component-specific types. The solution and Workspace ID move out of `ReferenceDiscoveryCacheKey` and `ProjectTargetFrameworkCacheKey` because the Host-created scope supplies them.

The internal store supports synchronous and asynchronous get-or-create factories. This preserves synchronous MSBuild target-framework evaluation without fake asynchrony while allowing `ReferenceDiscoveryService` to await `SymbolFinder.FindReferencesAsync`.

Workspace invalidation retains the current semantic model:

- invalidate when the effective immutable `Solution` changes;
- invalidate on Workspace epoch replacement or close;
- invalidate on stale or conflicted state; and
- retain results across state-only transitions when the exact `Solution` remains valid.

Plugin entries use the stricter exact `WorkspaceSnapshotIdentity`. Starting, staging, undoing, redoing, rolling back or committing a transaction moves plugin calls to a new scope even if a transition happens to retain an equivalent solution.

## Concurrent Misses

Coalesce identical misses across synchronous and asynchronous callers within the same complete scoped identity.

- The first caller starts the value factory.
- Later callers await or synchronously wait for that computation.
- Cancelling one caller stops only that caller's wait.
- The shared computation is cancelled when every waiter leaves.
- Workspace invalidation or Host shutdown cancels the computation immediately.
- Only one successful admission is attempted.
- The in-flight registration is removed after success, `null`, failure or cancellation.
- A completion from an invalid generation cannot insert into the new generation.

The factory receives a shared computation token rather than the first caller's cancellation token. The state tracks waiters so one cancelled request cannot disrupt callers that still require the value.

### Recursive Factory Protection

A factory requesting its own scoped key would otherwise wait for its own in-flight computation. Detect same-key re-entry through internal execution-context-local tracking around factory invocation.

The tracking flows across `await`, is restored in `finally`, and applies only to the flow executing the factory. `A → B` nesting is supported; `A → B → A` fails immediately with a clear `InvalidOperationException`. Ordinary concurrent callers for `A` still join the in-flight computation.

## Expiration

Workspace and plugin query results use sliding expiration only. The default is one hour since last access.

This matches the expected MCP server lifecycle: an agent may query a Workspace, spend substantial time editing or validating, and return to earlier unchanged information. Hot results should remain warm. Transaction and solution changes invalidate entries regularly, bounded capacity limits retained state, and process shutdown is the final lifetime boundary. An absolute lifetime would create periodic cold misses without improving snapshot correctness.

Configure the caches independently:

- `--workspace-query-cache-sliding-expiration`
- `--plugin-query-cache-sliding-expiration`

Each has the repository-standard environment-variable equivalent, defaults to `01:00:00`, must be positive and cannot exceed 24 hours. Invalid values fall back to the default and produce `StartupConfigurationFallback`.

Code Action references retain their existing absolute-expiration-only contract because callers receive replayable handles with an explicit lifetime.

## Capacity Configuration

Expose independent command-line and environment limits:

- `--workspace-query-cache-size-limit`
- `--plugin-query-cache-entry-limit`
- `--code-action-reference-cache-size-limit`

Caching is part of the performance contract and cannot be disabled. Zero is invalid. For each family, configuration accepts only a measured range from a supported minimum through a hard maximum. Values outside the range fall back to the default and emit `StartupConfigurationFallback`.

Do not select numeric minimums, defaults or maxima from intuition. Determine them from the scenario-runner calibration described below. The minimum should be close enough to the default that a supported configuration cannot silently defeat the intended performance benefit. The maximum must permit deliberate growth without allowing unbounded retention.

Do not add these effective settings to `server-status`. Publish them through command-line help and `Configuration.md`; retain invalid-value visibility through startup warnings.

## Plugin Authoring Analyser

Add two diagnostics to the packaged plugin authoring analyser.

### RWMCP020 — Invalid query-cache key

Severity: Error.

Report a cache key that is not a dedicated, structurally safe immutable reference type with meaningful value equality. Detect at least:

- bare framework reference types such as `string` and `object`;
- arrays and mutable collection types;
- writable instance fields or properties;
- types without suitable value equality;
- direct Roslyn snapshot objects such as `Solution`, `Project`, `Document` or symbols;
- unsafe members nested within records or other supported composite keys; and
- structurally unsafe inheritance or equality shapes that can invalidate the contract.

The generic marker constraint remains the compile-time boundary. The analyser cannot prove that every semantic input is included, so documentation and examples remain authoritative.

### RWMCP021 — Unsafe cached value

Severity: Warning.

Report cached value types that are clearly mutable, implement `IDisposable` or `IAsyncDisposable`, or recursively contain an evidently unsafe retained shape. Also warn when a known tool-result or failure envelope is cached because transient failures may become sticky.

Static analysis cannot always determine ownership or whether a reference is treated immutably, so this diagnostic remains suppressible with a specific justification. Runtime admission still refuses actual disposable values.

Document both diagnostics in `PluginAuthoringDiagnostics.md`, include them in analyser release metadata and validate packaging into the Plugins NuGet package.

## Scenario-Runner Calibration and Evidence

Cache utilisation becomes permanent versioned scenario-runner evidence rather than a one-off calibration artifact.

Record per family:

- peak entry count;
- peak charged units;
- largest individual entry charge where the family uses weighted units;
- hits and misses;
- callers joining an in-flight computation;
- capacity admission refusals;
- evictions grouped by capacity, expiration and lifecycle invalidation;
- late-store rejection;
- factory failure and cancellation without admission; and
- process working-set impact.

Initial calibration uses deliberately generous but still bounded limits and covers:

- small, medium and large external repositories;
- repeated queries against an unchanged snapshot;
- idle gaps representative of agent reasoning, editing, build and test work;
- transaction staging, undo, redo, rollback and commit;
- reload, close, stale and conflicted transitions;
- ordinary Code Action and Fix All discovery and staging; and
- a fixture plugin that exercises repeated and distinct plugin-cache keys, synchronous and asynchronous factories, coalescing, `null` and disposable non-admission.

The fixture is required because the new public plugin cache has no representative third-party production consumer before release. Correlate logical units with retained process memory, then select and document the supported minimum, default and maximum for each family with evidence-backed headroom.

Retain the metrics in normal release scenario output so future releases expose changes in cache pressure, retained memory and effectiveness.

## Implementation Handoff

The implementing agent should use these existing entry points rather than rediscovering the composition path:

- `RoslynWorkbenchServiceCollectionExtensions` owns the current shared `IMemoryCache`, query-cache aliases and Code Action reference-store aliases.
- `PluginQueryMcpServerTool` owns the `PluginQueryRegistration`, whose `Tool.Plugin.PluginId` and `Tool.Metadata.Name` provide the stable scope identities. Expand the context-creation call to pass those identities; do not rediscover them from assemblies, handler types or ambient state.
- `PluginExecutionContextFactory` and `PluginQueryContext` adapt the neutral Workspace execution context into the public plugin context.
- `IWorkspaceExecutionContext` already carries the internal `WorkspaceSnapshotIdentity`. Use it to seed the plugin cache scope without exposing the internal identity type through the public Plugins API.
- `WorkspaceSessionStore` owns the current solution-based query invalidation decisions and exact Workspace snapshot lifecycle notifications.
- `ReferenceDiscoveryService` and `ProjectTargetFrameworkResolver` are the two production Workspace query-cache consumers.
- `CodeActionReferenceStore` currently combines reference operations, cache/index state and `IWorkspaceSnapshotLifecycleObserver`.
- `PluginDiagnosticDescriptors`, the plugin authoring analyser tests, `AnalyzerReleases.Unshipped.md` and `PluginAuthoringDiagnostics.md` define the established analyser delivery pattern.

The public `IQueryResultCache` and `IQueryResultCacheKey` contracts belong to the Plugins authoring surface. Keep Workspace storage, generations and in-flight coordination internal, and adapt them through the Plugins execution boundary. Do not move the author-facing contracts back into the minimal Workspace Abstractions assembly merely because the removed `IQueryCache` currently lives there.

After the three cache families own dedicated state and `MemoryCache` instances, check for remaining application-wide `IMemoryCache` consumers. If none remain, remove `AddMemoryCache`, `QueryCacheMemoryOptionsConfiguration` and the shared registration instead of retaining an unused fourth cache.

Compile-prototype the nullable generic signatures and shared synchronous/asynchronous in-flight primitive early. They are the most technically delicate parts of the contract and must be proven with the repository's target language version before the migration is propagated through public API locks, mocks and plugin fixtures.

Treat all numeric capacity values used before calibration as provisional measurement ceilings. Do not publish, compatibility-lock or document them as v1 defaults until the required scenario evidence has selected the supported minimum, default and maximum for every family.

Implement in reviewable behavioural stages:

1. dedicated interface-registered storage and existing-behaviour preservation;
2. invalidation generations, shared sync/async single-flight and recursive-factory protection;
3. public invocation scope and Host registration identity flow;
4. Workspace consumer and Code Action lifecycle migration;
5. analyser, package and authoring documentation;
6. scenario metrics and calibration workloads; and
7. evidence-based capacity selection, CLI finalisation and release documentation.

Do not run acceptance tests unless separately requested under the repository test policy. Scenario-runner changes and scenario definitions must receive the representative external-repository coverage required by the repository instructions.

## Migration

1. Introduce separate interface-registered Workspace query, plugin query and Code Action reference states with dedicated cache instances.
2. Split query storage, invalidation and scope creation into role-specific services.
3. Split Code Action reference storage from its Workspace lifecycle observer while preserving exact behaviour.
4. Add invalidation generations, single-flight coordination, waiter cancellation and recursive-factory protection.
5. Add `IQueryResultCacheKey` and `IQueryResultCache` to the public Plugins API and expose the invocation scope through `IQueryContext`; keep Workspace storage internal and adapt it at the Plugins boundary.
6. Pass stable plugin and tool identity from each Host registration when constructing the query context.
7. Add the internal `IWorkspaceQueryCacheKey` and Host component scope.
8. Migrate `ReferenceDiscoveryService` and `ProjectTargetFrameworkResolver`.
9. Remove `IToolExecutionServices.QueryCache`, the current public `IQueryCache` and any remaining unused application-wide memory-cache registration.
10. Add `RWMCP020` and `RWMCP021`, documentation, release metadata and package validation.
11. Add versioned cache metrics and calibration workloads to the scenario runner.
12. Run calibration scenarios, select supported capacity ranges and publish the effective CLI contracts.
13. Update public API locks, plugin-authoring documentation, configuration documentation and composition tests.

Because the current cache API has not been released as a stable v1 package contract, complete this migration before compatibility-locking the Plugins package.

## Required Validation

Add focused component, integration, analyser, published-package and scenario coverage proving:

- plugin entries are isolated by Workspace, epoch, snapshot, plugin, tool, key type and value type;
- Host entries are isolated by Workspace, epoch, solution and component;
- stable state transitions over the same immutable solution retain valid Host entries;
- every transaction snapshot/revision transition prevents plugin reuse;
- staging, undo, redo, rollback, commit, reload and changed-solution replacement cannot return stale entries;
- Workspace close and stale/conflicted transitions invalidate relevant entries;
- a computation completing after invalidation cannot insert into the new generation;
- identical synchronous and asynchronous misses coalesce;
- one caller's cancellation does not cancel work retained by another caller;
- the last departing waiter, Workspace invalidation and Host shutdown cancel in-flight work;
- failed, cancelled, `null` and disposable factory results are never cached;
- capacity refusal and late-store rejection still return the computed value where one exists;
- recursive same-key factories fail without hanging;
- a retained invocation scope fails after lease completion;
- sliding expiration renews on access and uses the configured one-hour default;
- invalid duration and capacity settings follow fallback-warning policy;
- physical cache separation prevents plugin pressure from evicting Workspace results or Code Action handles;
- Code Action expiry, consumption, post-eviction indexes and exact lifecycle invalidation remain correct;
- cache eviction releases retained Roslyn solution, symbol and document graphs;
- `RWMCP020` and `RWMCP021` detect their supported unsafe shapes without rejecting documented valid keys and values;
- the Plugins package carries the updated contracts and analyser exactly once; and
- scenario metrics correlate cache pressure with retained working set across representative repositories.

Use real Workspace lifecycle and cache implementations for invalidation and concurrency tests rather than relying only on mocked seams.
