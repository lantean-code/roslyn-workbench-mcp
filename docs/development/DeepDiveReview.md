# Deep Dive Review

Date: 2026-07-31

**Review status:** Complete — all seven implementation-depth units and all repository-wide validation passes completed on 2026-07-31. Final validated findings are recorded in the [deep-dive final report](repo-review/deep-dives/final-findings.md); remediation and fix revalidation remain outstanding, and the final v1 release gate must not proceed until the two P1 findings are resolved.

## Purpose

The repository-wide review established the project architecture, reviewed every subsystem at a broad level and performed cross-project validation. The subsequent implementation-depth review of the bundled query and mutation tools found defects that the broad review had not exposed. This programme applies the same depth to the remaining repository areas before v1 release preparation.

This is a review of the complete current repository state, not a diff, branch, commit or pull request. Existing behaviour is in scope regardless of when it was introduced. The objective is to establish implementation-level assurance without losing the cross-project context provided by the original repository review.

The completed query and mutation tool review is the baseline for review depth and evidence quality. It does not need to be repeated unless another subsystem exposes a new interaction risk.

## Review principles

Each deep dive must:

- inspect the subsystem implementation, its direct dependencies and its direct consumers;
- follow representative operations from their executable or protocol entry point to every external boundary involved;
- validate contracts crossing project, package, process and persistence boundaries;
- inspect dependency-injection registrations, service lifetimes and configuration declaration, validation and consumption;
- compare documented and claimed behaviour with the tests that are intended to prove it;
- revisit previously reviewed code whenever a downstream consumer exposes an additional risk;
- distinguish implementation defects from missing coverage, documentation drift and intentional product constraints;
- record only substantiated issues with a plausible failure scenario; and
- avoid production-code changes until the deep dive and independent finding validation for that review unit are complete.

Pure formatting preferences, minor style disagreements, generic best-practice suggestions and concerns adequately prevented elsewhere in the call path are out of scope.

## Review order and units

Review units are ordered by dependency direction. A later unit may send an earlier unit back to review when consumer behaviour invalidates an earlier assumption.

### 1. Public contracts and Workspace semantics

**Status:** Complete — reviewed 2026-07-31; report: [Public contracts and Workspace semantics](repo-review/deep-dives/subsystems/01-public-contracts-and-workspace-semantics.md)

Scope:

- public selectors, result models, snapshot preconditions and resolver/service contracts in Abstractions;
- Workspace loading, solution and project selection, resolution and snapshot identity;
- session state, operation gates, query and mutation leases and lifecycle transitions;
- Workspace query services, cache keys, invalidation and change detection; and
- consumers in Plugins, Plugins.Core, CodeActions and the Host.

Representative traces include Workspace open, query context acquisition, selector resolution, external-change detection, reload and close.

Primary risks include ambiguous or stale selection, snapshot mismatch, incorrect lease behaviour, cache reuse across invalid states, path identity errors, MSBuild compatibility, cancellation, disposal and public-contract incompatibility.

### 2. Transactions, commit and recovery

**Status:** Complete — reviewed 2026-07-31; report: [Transactions, commit and recovery](repo-review/deep-dives/subsystems/02-transactions-commit-and-recovery.md)

Scope:

- transaction admission, ownership and revision history;
- mutation candidate validation and linked-document reconciliation;
- preview, undo, redo, rollback and commit planning;
- filesystem containment, locking, atomic writes and recovery manifests;
- startup recovery and conflict handling; and
- plugin, Code Action and Host consumers of the transaction boundary.

Representative traces include a single-file mutation, a multi-project linked-document mutation, a cancelled commit, an interrupted partial commit, startup recovery and cross-process contention.

Primary risks include data loss, partial application, stale revisions, inconsistent rollback, lock races, recovery ambiguity, permission changes, symlink or reparse traversal, cancellation at unsafe boundaries and resource leakage.

### 3. Plugin platform

**Status:** Complete — reviewed 2026-07-31; report: [Plugin platform](repo-review/deep-dives/subsystems/03-plugin-platform.md)

Scope:

- the public plugin API, registration builders, handler contracts and execution outcomes;
- query and mutation context adaptation to Workspace;
- plugin discovery, assembly inspection, load contexts, dependency sharing and MEF composition;
- plugin and tool identity, collision handling and startup materialisation;
- query-cache scoping and lifecycle behaviour;
- authoring analysers, package contents and external consumer compatibility; and
- bundled and fixture plugins as direct consumers of the public surface.

Representative traces include loading a valid external plugin, rejecting incompatible or conflicting packages, invoking query and mutation handlers, invalidating cached results and building an external plugin solely from the packaged authoring surface.

Primary risks include binary or source incompatibility, runtime/analyser contract mismatch, dependency identity conflicts, unsafe escaped contexts, cross-plugin state reuse, incorrect isolation claims, handler lifetime races and incomplete package composition.

### 4. Code Actions

**Status:** Complete — reviewed 2026-07-31; report: [Code Actions](repo-review/deep-dives/subsystems/04-code-actions.md)

Scope:

- provider discovery, MEF composition, analyser activation and policy filtering;
- action discovery, nesting, identity and snapshot-bound replay references;
- Fix All preparation and execution;
- `CodeActionOperation` evaluation and unsupported-operation handling;
- mutation staging through Workspace; and
- Host registrations, adapters, expiry behaviour and provider-focused tests.

Representative traces include listing and replaying nested actions, staging a multi-document action, preparing and executing Fix All, replay after Workspace change and provider output containing unsupported operations.

Primary risks include unstable replay identity, provider-version assumptions, incomplete operation evaluation, stale action reuse, incorrect multi-document changes, cache expiry races, cancellation, MEF resource ownership and divergence between controlled fixtures and real providers.

### 5. Host and protocol

**Status:** Complete — reviewed 2026-07-31; report: [Host and protocol](repo-review/deep-dives/subsystems/05-host-and-protocol.md)

Scope:

- process startup and shutdown, MSBuild registration and composition roots;
- command-line and environment configuration precedence, validation and diagnostics;
- dependency-injection registrations, aliases, lifetimes and hosted startup prerequisites;
- MCP request binding, validation, generated schemas, result envelopes and serialisation;
- plugin, Code Action and server-owned tool adapters;
- Workspace and transaction lifecycle tools; and
- stdout protocol integrity, stderr diagnostics and unexpected-exception handling.

Representative traces include clean startup, invalid configuration, Workspace lifecycle operations, each adapter family, malformed MCP input, cancellation, unexpected handler failure and graceful stdin shutdown.

Primary risks include wire incompatibility, schema/runtime disagreement, incorrect validation timing, singleton state leakage, incomplete registration, configuration accepted but unused, stdout contamination, cancellation remapping and startup or shutdown resource leaks.

### 6. Error reporting and trust boundaries

**Status:** Complete — report: [Error reporting and trust boundaries](repo-review/deep-dives/subsystems/06-error-reporting-and-trust-boundaries.md)

Scope:

- unexpected-error capture, bounded retention and correlation;
- external report projection, redaction and allow-listing;
- prepare, review, consent and submission state transitions;
- logging and Sentry dispatch, network configuration and disposal;
- Workspace lifecycle invalidation and concurrent requests; and
- user-facing availability and status contracts.

Representative traces include capturing an unexpected failure, preparing a report containing sensitive input, consent-required and consent-free submissions, repeated or concurrent submission, Workspace closure and network failure.

Primary risks include secret or source disclosure, consent bypass, time-of-check/time-of-use errors, replay or duplicate submission, unsafe redirects or destinations, unbounded retained data, cross-Workspace correlation and incomplete transport flushing.

### 7. Test and operational infrastructure

**Status:** Complete — report: [Test and operational infrastructure](repo-review/deep-dives/subsystems/07-test-and-operational-infrastructure.md); five validated P2 findings: RWMCP-034 through RWMCP-038

Scope:

- unit, integration, audit and acceptance project structure and enforcement;
- shared fixtures, mocks, test assets and published-package consumers;
- Host process and MCP acceptance harnesses;
- external-repository scenario preparation, execution, restoration and cache reuse;
- EventPipe, process and performance metric collection; and
- CI workflows and the relationship between ordinary, audit, acceptance and release-only validation.

Representative traces include a real component fixture lifecycle, published-Host acceptance, repeated external-repository preparation, cancelled or failed child processes, source mutation and restoration, and report production after partial scenario failure.

Primary risks include tests failing before exercising their claim, mocks that hide integration defects, platform-specific false confidence, destructive or incomplete repository restoration, orphaned processes, lost diagnostics, incomparable performance evidence and important suites that are never executed at the required gate.

## Repository-wide validation passes

**Status:** Complete — reviewed 2026-07-31; report: [Repository-wide passes](repo-review/deep-dives/repository-wide-passes.md); final report: [Deep-dive validated findings](repo-review/deep-dives/final-findings.md)

After all seven units are complete, perform explicit repository-wide passes for:

1. cross-project and package contract mismatches;
2. dependency direction and abstraction ownership;
3. representative end-to-end behaviour across every involved project;
4. dependency-injection registration and lifetime consistency;
5. configuration declaration, precedence, validation and consumption;
6. error, cancellation and retry propagation;
7. concurrency, shared state, cache coalescing and thread safety;
8. transaction, persistence and cross-process consistency;
9. serialisation, schema, binary and package compatibility;
10. security and trust boundaries;
11. resource ownership and disposal;
12. performance problems with plausible repository-scale impact;
13. missing or misleading integration, acceptance, audit and scenario coverage; and
14. duplicate, conflicting, unreachable or partially implemented behaviour.

These passes must trace representative operations again using the conclusions from every deep dive. Earlier review units must be reopened when later evidence changes their risk assessment.

## Review artefacts

Working evidence should be durable and independent of conversation context. Store it under [`docs/development/repo-review/deep-dives/`](repo-review/deep-dives/) using:

- [`review-plan.md`](repo-review/deep-dives/review-plan.md) for the current execution order and status;
- [`findings.md`](repo-review/deep-dives/findings.md) as the candidate finding ledger;
- one report per numbered review unit under [`subsystems/`](repo-review/deep-dives/subsystems/);
- [`repository-wide-passes.md`](repo-review/deep-dives/repository-wide-passes.md) for the final cross-cutting analysis; and
- [`final-findings.md`](repo-review/deep-dives/final-findings.md) for independently validated results.

Continue the existing stable `RWMCP-###` finding sequence. Every candidate finding must include severity, confidence, exact file and line range, a concrete failure scenario, supporting call path or evidence, affected projects or subsystems and a concise remediation direction.

The candidate ledger must retain status and validation history while a deep dive is active. The final report must remove duplicates, reject candidates that cannot be substantiated against current source and order retained findings by severity and then confidence.

## Validation expectations

Use the narrowest test or executable evidence that proves or disproves each candidate, followed by the relevant non-acceptance project suites when source changes are later authorised. Acceptance, Code Action audit and external-repository scenarios remain subject to their explicit repository execution policies.

Tests are evidence, not a substitute for source analysis. A passing test supports a conclusion only when the fixture and assertions exercise the real boundary implicated by the candidate failure scenario.

Areas that depend on platform-specific behaviour, third-party Roslyn providers, external repositories, packaged consumers or network transport must be identified explicitly when the current environment cannot provide representative evidence.

## Completion criteria

The deep-dive programme is complete when:

- every review unit has an implementation-depth report;
- every representative trace has been followed across its complete call path;
- all repository-wide validation passes have been completed;
- every candidate has been independently revalidated against the then-current source;
- the final report identifies validated findings, notable test gaps and review limitations; and
- any release-blocking finding is resolved and revalidated before the v1 release candidate proceeds through the final release gate.
