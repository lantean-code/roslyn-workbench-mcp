# Repository review plan

## Method

The review is defect-first and repository-wide, not diff-based. Each unit is reviewed in dependency order and includes its implementation, direct dependencies, direct consumers, boundary contracts, DI registrations, configuration, tests and representative runtime call paths. Candidate findings are recorded immediately in the [finding ledger](findings.md), then revalidated from current source after all later-consumer and cross-cutting passes. Review artefacts are the only files modified.

## Review units and order

### 1. Public selectors, results and resolver contracts

Scope: `Roslyn.Workbench.Mcp.Abstractions`, its package exposure through Plugins, direct implementation in Workspace, consumers in Plugins/Plugins.Core/CodeActions/Host and contract tests. Focus: snapshot identity, selector ambiguity, invalid representable states, bounded collection semantics, cancellation/API compatibility and abstraction ownership.

Output: [Public contracts](subsystems/01-public-contracts.md).

### 2. Workspace loading, resolution, state and query infrastructure

Scope: Workspace loading, selection, resolution, session state, operation gates/leases, project/reference services, caches, change monitors and coordination, plus Host registrations and consumers. Focus: disposal, path identity, MSBuild compatibility, stale snapshot rejection, query/exclusive concurrency, cache key/invalidation correctness and external-change transitions.

Output: [Workspace query infrastructure](subsystems/02-workspace-query-infrastructure.md).

### 3. Transaction staging, commit and recovery

Scope: Workspace mutation candidate processing, linked documents, transaction revision history, diffing, commit planning/locking/writing, atomic I/O, state directory security and recovery. Consumers include plugin and Code Action stagers and Host lifecycle tools. Focus: data loss, partial commits, rollback/reload behaviour, cross-process races, path containment, cancellation, manifest durability and state consistency.

Output: [Transactions and recovery](subsystems/03-transactions-and-recovery.md).

### 4. Third-party plugin API, execution and authoring analysers

Scope: Plugins public contracts/builders/attributes, preparation/materialisation/validation, execution contexts, result caching, Workspace adapters, package layout, public analyser rules and relevant tests/fixtures. Focus: public compatibility, type accessibility, cancellation propagation, handler lifetime/thread safety, mutation isolation, cache correctness and discrepancies between analyser-enforced and runtime-enforced contracts.

Output: [Plugin platform](subsystems/04-plugin-platform.md).

### 5. Bundled inspection and mutation tools

Scope: Plugins.Core request/response contracts, all handlers, shared services/projections and registrations, with unit and integration tests. Review related tools by semantic family rather than file order: structure/options; symbol resolution/navigation; references/call graphs; diagnostics/flow/operation; dependency/impact/API analysis; advisory analyses; formatting/rename. Focus: Roslyn API semantics, deterministic limits/order, multi-project and linked-document behaviour, stale location handling, compilation cancellation and plausible performance hot paths.

Output: [Bundled core tools](subsystems/05-bundled-core-tools.md).

### 6. Internal Code Action subsystem

Scope: composition/provider selection, analyzer activation/diagnostics, discovery/policy, reference cache/replay, fix-all, evaluation, contexts, staging, registrations and Host adapters. Focus: public Roslyn provider compatibility, action identity/replay determinism, snapshot and expiry enforcement, operation filtering, multi-document changes, cancellation, MEF/resource lifetimes and separation from plugins.

Output: [Code Actions](subsystems/06-code-actions.md).

### 7. Host startup, configuration, protocol and plugin loading

Scope: `Program`, startup composition, options resolution/reporting/validation, MSBuild registration, DI graph, MCP schemas/binding/envelopes, four adapter families, server-owned tools, plugin discovery/path policy/metadata/load contexts/composition/collisions and status. Focus: startup failure modes, option precedence, registration lifetimes, wire compatibility, validation timing, reserved-name integrity, dependency isolation, untrusted paths and stdout/stderr correctness.

Output: [Host configuration and protocol](subsystems/07-host-configuration-and-protocol.md).

### 8. Error reporting and network/privacy boundary

Scope: capture stores, projection, submission preparation/state, consent, availability, tools, logging/Sentry dispatch, lifecycle observers, configuration/build embedding and tests. Focus: secret/PII leakage, consent bypass, time-of-check/time-of-use, replay/duplicate submission, redirects/TLS, bounded retention, concurrent state transitions, Sentry flush/disposal and public status exposure.

Output: [Error reporting](subsystems/08-error-reporting.md).

### 9. Test architecture, fixtures and scenario runner

Scope: all test projects/support/fixtures/assets, category/build enforcement, acceptance harness, scenario runner implementation and checked-in suites. Focus: whether tests exercise the claimed real boundaries, false-positive unit isolation, missing end-to-end cases, fixture fidelity, process cancellation/cleanup, command execution safety, repository restoration and misleading coverage assumptions.

Output: [Tests and ScenarioRunner](subsystems/09-tests-and-scenario-runner.md).

## Repository-wide passes after subsystem review

1. Cross-project contract and abstraction-ownership mismatches.
2. Representative end-to-end traces for workspace open/query/mutation, Code Action replay, commit/recovery and error submission.
3. Complete DI registration/lifetime and configuration declaration/consumption audit.
4. Error, cancellation and retry propagation from MCP boundary to Roslyn/filesystem/network boundary.
5. Concurrency, singleton shared state, operation gates, cache coalescing and thread safety.
6. Transaction, filesystem and recovery consistency including cross-instance interference.
7. JSON/schema/plugin binary compatibility and bounded result semantics.
8. Security/trust boundary audit for paths, plugins, MSBuild, state storage, external commands and network egress.
9. Resource ownership/disposal audit for workspaces, watchers, streams, load contexts, processes, EventPipe sessions, caches and Sentry transport.
10. Plausible repository-scale performance risks, using existing audit/scenario evidence where applicable.
11. Missing or misleading integration/acceptance/scenario tests.
12. Duplicate, conflicting, unreachable or partially implemented behaviour across tools and lifecycle paths.

## Finding ledger and validation standard

Each candidate receives a stable `RWMCP-###` identifier in the [finding ledger](findings.md), with severity, confidence, exact line range, concrete failure scenario, call path/evidence, affected subsystems and remediation direction. A candidate is retained only when current source demonstrates a plausible meaningful correctness, security, data-loss, concurrency, consistency, compatibility, resource, performance or maintainability failure. The final independent validation revisits the cited source and relevant call sites/tests, merges duplicates and removes candidates adequately prevented elsewhere. The [final findings](final-findings.md) are ordered by severity and confidence and record test gaps and review limitations.
