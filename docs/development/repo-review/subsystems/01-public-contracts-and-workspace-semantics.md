# Unit 1 — Public contracts and Workspace semantics

**Status:** Completed after consumer-side reopening and retrace.

## Scope and implementation evidence

The review covered Workspace-facing Abstractions; Workspace loading, selection, resolution, state, caching, change detection, coordination, execution contexts and lifecycle; Host lifecycle/status consumers; and plugin/Code Action consumers of Workspace identity, snapshots and leases.

- Public selectors enforce structural constraints and Workspace resolution binds locations to Workspace ID, epoch and transaction revision.
- Open resolves allowlisted MSBuild properties, capacity and uniqueness, recovery, certification, compatibility, containment, external-document content, input manifest, instance state and atomic session registration. Failure paths dispose partial resources.
- Each session owns its `MSBuildWorkspace`, manifest/monitor and gate. Reload retains MSBuild properties while allocating a new epoch and snapshot identity.
- Queries acquire shared non-waiting leases; mutation/lifecycle operations acquire exclusive leases. Acquisition rereads the session after taking the gate. Contexts capture one immutable effective solution and invocation identity.
- Session replacement/removal invalidates Workspace/plugin caches. Coalesced cache work is cancellation-aware and rejects late storage after invalidation.
- Selection omission is allowed only for one Workspace; supplied identity fields must agree. Project/document/symbol resolution exposes invalid, missing and ambiguous outcomes.
- Existing external evaluated documents are queryable and fingerprinted but remain outside mutation. In-root membership is recursively watched; external files are fingerprint-polled.
- Status refreshes fingerprints and state. Reload requires out-of-date, rejects transaction states, preserves the old session on failure, then replaces epoch/resources. Close is exclusive and shutdown drains all sessions.

## Required traces and candidates

Open, query acquisition/disposal, selectors, change detection/status, reload and close/shutdown were traced across Host, Workspace, Plugins and CodeActions. `RWMCP3-001` records missing detection for new external wildcard documents. `RWMCP3-002` records target-framework selection from an unrelated output-path ancestor. Unit 2 additionally raised `RWMCP3-003`, `RWMCP3-004` and `RWMCP3-006`, reopening this unit's owner, snapshot and recovery-admission assumptions.

## Claimed and executable evidence

Workspace unit/integration, Host lifecycle/status, Workspace acceptance, IntegrationTestSupport and Workspace assets claim broad coverage. Existing tests cover in-root additions and normal target-framework layouts, but not new external wildcard membership or unrelated ancestor collisions.

The reviewer confirmed SDK 10.0.102 and used dogfood to open/close the current 30-project solution. Missing restored analyser/package dependencies meant semantic results were not treated as complete evidence. No build/test ran under the read-only review.

## Limits and consumer follow-up

Windows/WSL watcher, case, symlink and coordination boundaries remain Unit 8 evidence. Unit 4 must revisit snapshot/selector consumers, Unit 5 Code Action Workspace mutation guards, Unit 6 Host stop ordering and concurrent transaction dispatch, and Unit 8 both initial candidates at real boundaries.

## Exported assumptions

A successful lease must own one stable effective solution and exact invocation identity. Project/document IDs are epoch-local. External documents are queryable but immutable. Reload changes epoch and retains resolved MSBuild properties.

**Candidates:** `RWMCP3-001`, `RWMCP3-002`; affected by `RWMCP3-003`, `RWMCP3-004`, `RWMCP3-006`.

**Later units required to revisit:** Units 4, 5, 6 and 8.

## Reopening resolution

Units 4–6 independently confirmed that query, mutation, Code Action and Host consumers trust the Workspace lease and selector/snapshot contracts without compensating checks. Unit 6 confirmed that concurrent protocol dispatch does not serialise transaction admission and that malformed recovery admission escapes through the server-owned boundary. Unit 8 found no real-boundary test that disproves the external wildcard, simultaneous transaction-start, public snapshot-alias or malformed recovery scenarios. The reopened assumptions remain represented by `RWMCP3-003`, `RWMCP3-004` and `RWMCP3-006`; no duplicate Unit 1 candidate is required.
