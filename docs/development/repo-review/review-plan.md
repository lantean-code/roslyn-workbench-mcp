# Independent Deep-Dive Review Plan

Date: 2026-08-13

**Stage:** 5 — Independent candidate validation and final findings

**Status:** Complete

## Evidence boundary

Every review unit will use only the current checked-out source, tests, configuration, project/package definitions and current normative design documentation. Git history, diffs, commits, branches, tags, stashes, reflogs, deleted or renamed review artefacts, external backups, historical audits, validation/remediation records and previous findings are outside the evidence boundary. Existing behaviour remains in scope regardless of age.

The candidate ledger is `findings.md`; the next available identifier is `RWMCP2-021`. A candidate will be recorded only after its concrete scenario and complete current call path are substantiated. No production-code remediation will begin during subsystem review or independent validation.

## Stage status

| Stage | Status | Output |
| --- | --- | --- |
| 1. Current architecture map | Complete | `architecture.md` |
| 2. Dependency-ordered review plan | Complete | This document and the durable `findings.md` ledger |
| 3. Eight subsystem review units | Complete | Eight reports under `subsystems/` |
| 4. Repository-wide validation passes | Complete | `repository-wide-passes.md` |
| 5. Independent candidate validation and final findings | Complete | `final-findings.md` |

## Review-unit dependency order

The order follows the current production graph: public contracts before their Workspace implementation; Workspace state before persistence; shared Workspace/plugin execution before bundled tools; Code Actions after the Workspace mutation boundary; the Host after all catalogues/adapters it publishes; error reporting after the Host exception/protocol boundary; and test/operational infrastructure after the production claims it is intended to prove. A later consumer may reopen any earlier unit when it invalidates an assumption.

### 1. Public contracts and Workspace semantics

**Status:** Complete

**Why first:** Every plugin, bundled tool, Code Action and Host lifecycle adapter depends on Workspace selectors, identities, leases and result semantics.

**Scope:** All Abstractions public contracts; Workspace loading/root resolution/compatibility; multi-workspace selection; resolver/factory and selector factories; session snapshots/store/state machine; shared/exclusive operation gates and explicit leases; query contexts; input certification/watchers/change detection; Workspace and plugin cache identity, coalescing, limits and invalidation; path comparison/normalisation; project/reference/hierarchy services; direct consumers in Plugins, Plugins.Core, CodeActions and Host.

**Required traces:** Open `.sln`, `.slnx` and `.csproj`; reject malformed/unsupported/out-of-root input; resolve Workspace/project/document/symbol/location/scope selectors; acquire successful, busy, cancelled and stale query/mutation leases; detect watched and polled external changes; move ready/transaction sessions out of date or conflicted; reload with a new epoch; close and dispose; verify cache reuse and lifecycle invalidation across base and transaction snapshots.

**Evidence:** Production call paths plus focused unit tests, Workspace integration fixtures and relevant published-Host selection/reload/containment acceptance tests. Use narrow executable evidence only when source leaves a material uncertainty.

**Primary risks:** Ambiguous selection, stale spans/symbols, epoch/revision mismatch, cross-Workspace cache reuse, gate starvation or leakage, cancellation/disposal defects, path identity/containment errors, incomplete MSBuild input tracking and public-contract incompatibility.

**Output:** `subsystems/01-public-contracts-and-workspace-semantics.md`.

### 2. Transactions, commit and recovery

**Status:** Complete

**Depends on:** Unit 1's session, snapshot, change-detection, path and exclusive-lease semantics.

**Scope:** Single transaction ownership across workspaces; revision admission/history/undo/redo/rollback; candidate validation; added/removed/linked document processing; staging and diff projection; commit planning and pre-write validation; physical containment; cross-process locks; atomic create/replace/delete and permission handling; recovery owner/artifact/manifest persistence; commit phases, cancellation boundaries, restoration and startup recovery; Workspace/plugin/Code Action/Host consumers.

**Required traces:** Single-file mutation; no-change candidate; linked file in multiple projects/targets; add/remove/replace combination; bounded revision traversal; cancellation before and after durable application begins; external drift before planning and during application; lock contention; interrupted partial commit followed by fresh-process recovery; malformed/conflicting recovery evidence; successful commit followed by Workspace lifecycle effects.

**Evidence:** Transaction/IO/recovery source, Workspace unit and integration projects, lock fixture, acceptance durability/startup tests and the narrowest checked-in scenario definitions needed to establish platform/process behaviour.

**Primary risks:** Data loss, partial application, stale revision commit, rollback inconsistency, unsafe cancellation, symlink/reparse escape, lock race, recovery ambiguity, lost permissions, orphaned artefacts and resource leakage.

**Output:** `subsystems/02-transactions-commit-and-recovery.md`.

### 3. Plugin platform

**Status:** Complete

**Depends on:** Units 1–2 for public Workspace contracts, context leases, cache lifecycle and mutation staging.

**Scope:** Packaged public API and Abstractions inclusion; authoring analyser/runtime agreement; attributes, entry-point and handler contracts; configuration builders/freezing; plugin-owned singleton services; registration materialisation; Workspace query/mutation adapters and result mapping; package discovery/path policy/metadata inspection; load-context dependency sharing; MEF composition; API compatibility, identity/collision/schema preflight; immutable runtime catalogue and disposal; bundled and fixture plugins plus external package consumer.

**Required traces:** Build solely against the packed authoring surface; compile-time analyser acceptance/rejection; load a valid dependency-bearing external package; reject malformed metadata, unsupported API, duplicate IDs/tools, reserved names, invalid schema/path and throwing configuration; invoke query and mutation handlers through prebuilt adapters; exercise plugin query-cache isolation, pressure, coalescing, null and disposable results; dispose catalogue and plugin providers.

**Evidence:** Plugins/Analyzers source, package targets and authoring documentation where normative; analyser/unit/integration/acceptance tests; all plugin fixtures and the package-consumer asset.

**Primary risks:** Binary/source incompatibility, missing package assets, analyser/runtime drift, dependency identity conflict, escaped package paths, cross-plugin state reuse, handler lifetime/thread-safety races, execution-time reflection, incomplete isolation claims and disposal failures.

**Output:** `subsystems/03-plugin-platform.md`.

### 4. Bundled query and mutation tools

**Status:** Complete

**Depends on:** Units 1–3 for selector/snapshot rules, result bounds, plugin execution, caching and mutation staging.

**Scope:** All 39 bundled tools and every published request/result contract; registration metadata and schema publication; common query/mutation base handlers; resolution/diagnostic/inspection/dependency services; Roslyn symbol, syntax, semantic, flow, operation, graph and solution APIs; projections and bounds; AsyncFixer activation; cache use; rename/format candidate construction; Host publication and all claimed test/scenario coverage.

**Required traces:** Every tool family, with a trace inventory that reaches success, empty, invalid, ambiguous/stale, bounded/truncated, cancelled and dependency failure outcomes where applicable. Trace both mutation tools through staging and Unit 2's commit boundary. Compare low/high/zero/one/default limits on representative repository-scale searches and identify discovery work performed before bounds.

**Evidence:** Each tool's implementation and direct services/callers, focused unit tests, component integration tests, published catalogue/schema acceptance tests and checked-in external-repository scenarios. Tests count only when their fixture crosses the boundary claimed.

**Primary risks:** Incorrect or misleading Roslyn results, unstable symbol identity, schema/runtime disagreement, wrong project/document scope, batch partiality, stale snapshots, unbounded work before projection, hidden cache/capacity failure, analyser ownership and tests limited to helper-level behaviour.

**Output:** `subsystems/04-bundled-query-and-mutation-tools.md`.

### 5. Code Actions

**Status:** Complete

**Depends on:** Units 1–2 for Workspace host services, snapshot identity and staging; Unit 4 provides comparison with ordinary tool publication but is not an implementation dependency.

**Scope:** Assembly resolution and MEF HostServices; C# provider discovery/selection/policy; built-in analyser index and activation; diagnostic collection; nested action flattening; replay recipe identity and bounded reference state; request/scope resolution; Fix All diagnostic provider/preparation/replay; operation evaluation; Workspace context adapters/staging; independent typed catalogue, Host adapters and provider/audit coverage.

**Required traces:** Compose built-in and controlled providers; list nested refactorings and diagnostic-backed fixes; replay exact action; expire/invalidate references on Workspace/revision changes; prepare and stage document/project/solution Fix All; stage multi-document/create-file changes; handle duplicate/changed/provider-unavailable replay; reject unsupported/multiple/no `ApplyChangesOperation`; observe cancellation and cache pressure.

**Evidence:** CodeActions source, unit/integration/audit projects, controlled provider fixtures, relevant acceptance tests and representative real-provider scenario cases.

**Primary risks:** Unstable replay identity, provider-version assumptions, stale action reuse, incomplete operation handling, incorrect Fix All diagnostics/scope, multi-document loss, expiry/invalidation races, MEF resource ownership and controlled-fixture divergence from real providers.

**Output:** `subsystems/05-code-actions.md`.

### 6. Host and protocol

**Status:** Complete

**Depends on:** Units 1–5 because the Host composes and projects each underlying catalogue and lifecycle boundary.

**Scope:** `Program`, stdout protection and generic Host lifetime; startup composer and hosted prerequisite order; MSBuild registration; complete DI graph/aliases/lifetimes; command-line/environment precedence and option mapping; MCP server configuration, list/call routing and stdio shutdown; request binder/object validation; schema builders and SDK integration; result envelopes/continuations/serialisation; four typed adapters; server-owned lifecycle/transaction/status tools; plugin and Code Action publication; unexpected-exception/cancellation mapping at the protocol boundary.

**Required traces:** Clean and invalid/fallback startup; startup prerequisite failure status; tool catalogue listing; each adapter family; every server-owned lifecycle/transaction operation; malformed/missing/unknown/enum-invalid arguments; optional output-schema modes; cancellation at binding/context/handler/staging boundaries; unexpected exception; client stdin closure and Host/service disposal.

**Evidence:** Host/config/protocol/tool source, DI registration, Host unit/contract/integration projects and published-process acceptance tests. Compare schemas with actual binder and serializer behaviour rather than reflection alone.

**Primary risks:** Wire incompatibility, schema/runtime mismatch, unused or wrongly mapped configuration, startup order races, singleton leakage, incomplete catalogue publication, stdout contamination, cancellation remapping, exception leakage and shutdown/resource failures.

**Output:** `subsystems/06-host-and-protocol.md`.

### 7. Error reporting and trust boundaries

**Status:** Complete

**Depends on:** Unit 6's top-level exception capture, MCP capabilities/elicitation, tool publication and Host lifetime; Unit 1's Workspace identity/lifecycle invalidation.

**Scope:** Capture classification and bounded request/exception retention; correlation and local details; external allow-listed projection; payload size/immutability/digest; prepared-submission state machine; consent modes and Workspace/session lifecycle; concurrent/repeated submission; logging and Sentry dispatchers, transport options/allow-list and disposal; status/availability contracts.

**Required traces:** Capture an unexpected handler failure containing sensitive request/source/path data; retrieve local details; prepare a bounded reviewed payload; never/prompt/always and unavailable-elicitation paths; allow-once/Workspace/session, decline/cancel/suppress; concurrent submission and retry after dispatch failure/cancellation; repeated completed submission; Workspace close/reload invalidation; stderr fallback and isolated Sentry acceptance/rejection/flush behaviour.

**Evidence:** Error-reporting and filter source, Host tests, Sentry integration test and relevant published-process behaviour. Any real network assumption must be identified explicitly; use only configured test transports/destinations.

**Primary risks:** Secret/source/path disclosure, consent bypass, mutable-preview TOCTOU, replay/duplicate dispatch, cross-Workspace correlation, unsafe destination/redirect behaviour, unbounded memory, lost transport flush and misleading availability.

**Output:** `subsystems/07-error-reporting-and-trust-boundaries.md`.

### 8. Test and operational infrastructure

**Status:** Complete

**Depends on:** Units 1–7 so test claims can be checked against the real production boundaries and failure modes they purport to exercise.

**Scope:** All fifteen solution test/support projects; seven plugin fixtures; package and Workspace assets; test category/build enforcement; shared Moq/in-memory and real-component fixtures; published Host acceptance harness and wrappers; test-count gates; Code Action audit; scenario runner configuration, repository/cache/command/restoration management, MCP Host client, concurrency/conflict/crash/state-sequence runners, EventPipe/process/memory collection, result validation/reporting; CI workflows and suite triggering.

**Required traces:** One representative unit mock graph; one real component Workspace fixture; one external plugin package fixture; published Host build/start/call/stdin shutdown; complete acceptance wrapper; audit inventory enforcement; first and repeated external-repository preparation; failed/cancelled child command; source mutation and restoration; forced Host crash and recovery; profile attach/detach; partial scenario failure and final validation/report production.

**Evidence:** Test source/project/build targets, fixtures/assets, CI YAML, wrappers, runner source and scenario JSON. Inspect whether assertions and test-count gates reach the claimed production boundary and whether platform matrices cover platform-specific code.

**Primary risks:** Tests failing before the claimed action, mocks hiding integration faults, false platform confidence, suites omitted at required gates, destructive/incomplete repository restoration, cache contamination, orphaned processes, lost diagnostics, invalid performance comparisons and misleading success reports after partial failure.

**Output:** `subsystems/08-test-and-operational-infrastructure.md`.

## Per-unit working method

For each unit:

1. Re-read its current implementation, direct project/package dependencies and direct consumers; do not rely on this architecture summary as proof.
2. Build a contract inventory covering public/internal inputs, outputs, state, lifetimes, cancellation and disposal.
3. Follow every required representative trace from executable/protocol entry to all external boundaries, including failure and cleanup paths.
4. Inspect DI registration and configuration declaration, validation and consumption for every participating service.
5. Match each behavioural claim to its tests and verify that fixtures/assertions exercise the actual boundary.
6. Record only substantiated candidate findings in `findings.md`, with exact current lines, concrete scenario, call path, affected areas and concise remediation direction.
7. Use the narrowest safe test or temporary diagnostic needed to prove or disprove a candidate; do not change production behaviour.
8. Reopen earlier reports when downstream evidence invalidates an assumption, retaining validation history in the ledger.

## Repository-wide validation after all units

After all eight reports are complete, perform and document fresh passes for: cross-project/package contracts; dependency ownership; end-to-end paths; DI lifetimes; configuration; error/cancellation/retry; concurrency/cache/thread safety; transaction/persistence/cross-process consistency; schema/serialisation/binary/package compatibility; security/trust; resource ownership; repository-scale performance; test/acceptance/audit/scenario coverage; and duplicate/conflicting/unreachable/partial behaviour.

Only after those passes will every candidate be independently revalidated against current source. Duplicates will be consolidated, unsupported candidates rejected with validation history retained in the ledger, and validated findings written to `final-findings.md` ordered by severity then confidence. Remediation groupings, implementation plans and completion statuses remain outside this independent review.

## Current stopping point

The independent review is complete through Stage 5. Every candidate was retraced against the complete current repository, including its consumers, product contracts and claimed prevention paths. Seventeen candidates were validated. `RWMCP2-016` was rejected after current MCP SDK pipeline inspection established prevention; `RWMCP2-018` was rejected because lifecycle invalidation governs later consent decisions rather than retroactively revoking an already-authorised explicit submission; and `RWMCP2-020` was rejected because the documented release-evidence unit is a successfully completed command rather than a resumable partial run. No candidates were merged or added. `final-findings.md` contains the severity- and confidence-ordered result. At that original stopping point, no production-code remediation, remediation grouping, implementation ordering or work-item completion assessment had begun; subsequent user-directed remediation progress is recorded below.

## Post-review remediation progress

**Status:** In progress

The primary remediation tracker in `final-findings.md` is complete through required worklist order 15. `RWMCP2-017` was remediated by sharing production request binding and selection before Workspace acquisition and by carrying immutable execution-time Workspace attribution from all four adapters through the top-level exception filter after acquisition. Unit, multi-Workspace selector integration and real PluginQuery adapter-to-filter MCP stream coverage passed; the first fresh context-free Review Agent identified and drove correction of replacement-Workspace misattribution; the final fresh Review Agent returned no findings; and the user confirmed the completed change on 2026-08-15. The next pending item is `RWMCP2-019`.
