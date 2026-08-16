# Independent Deep Dive Review

Date: 2026-08-13

**Review status:** Complete — all eight implementation-depth units, all fourteen repository-wide validation passes and independent candidate validation completed on 2026-08-13. The final dispositions are recorded in the [independent final report](repo-review/final-findings.md).

## Purpose

Perform a new implementation-depth review of the complete current repository state before v1 release preparation. This is not a diff, branch, commit or pull-request review. Existing behaviour is in scope regardless of when it was introduced.

The review must establish its own project-level and cross-project context before evaluating individual subsystems. It must not inherit findings, conclusions, risk assessments, accepted limitations or remediation decisions from an earlier review.

## Evidence boundary

Review only the repository's current checked-out state. Do not inspect Git history, deleted or renamed review artefacts, prior commits, branches, tags, stashes, reflogs or external backups for previous review findings or conclusions. Git may be used only for read-only current-worktree checks, such as identifying tracked files or confirming that the review begins from a clean working tree.

Current normative design documentation may be used to understand intended behaviour, but historical audits, validation reports, remediation records and prior review conclusions must not be used as evidence. Every conclusion must be independently established against the current implementation, tests, configuration and external boundaries.

Do not search for or attempt to reconstruct earlier finding identifiers. Use the fresh `RWMCP2-###` identifier series for this review.

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

## Stage 1: Current architecture map

**Status:** Complete — report: [Current repository architecture](repo-review/architecture.md)

Before reviewing any subsystem, inspect the complete current repository and write [`repo-review/architecture.md`](repo-review/architecture.md). Map:

- projects and project references;
- dependency direction;
- executable entry points and composition roots;
- major subsystems and their responsibilities;
- public, package and cross-project contracts;
- persistence, messaging, filesystem, networking and other external boundaries;
- plugin, analyser and extension mechanisms;
- dependency-injection ownership and important service lifetimes;
- configuration declaration and consumption; and
- test projects and the behaviours or boundaries they claim to cover.

This map must be derived from current project files, source, configuration and tests. Do not recreate or consult a previous architecture report.

## Stage 2: Review plan

**Status:** Complete — plan: [Independent deep-dive review plan](repo-review/review-plan.md)

Create [`repo-review/review-plan.md`](repo-review/review-plan.md) from the current architecture map. Review the following units in dependency order. A later unit may return an earlier unit to review when consumer behaviour invalidates an earlier assumption.

### 1. Public contracts and Workspace semantics

**Status:** Complete — report: [Public contracts and Workspace semantics](repo-review/subsystems/01-public-contracts-and-workspace-semantics.md)

Scope:

- public selectors, result models, snapshot preconditions and resolver/service contracts in Abstractions;
- Workspace loading, solution and project selection, resolution and snapshot identity;
- session state, operation gates, query and mutation leases and lifecycle transitions;
- Workspace query services, cache keys, invalidation and change detection; and
- consumers in Plugins, Plugins.Core, CodeActions and the Host.

Representative traces include Workspace open, query context acquisition, selector resolution, external-change detection, reload and close.

Primary risks include ambiguous or stale selection, snapshot mismatch, incorrect lease behaviour, cache reuse across invalid states, path identity errors, MSBuild compatibility, cancellation, disposal and public-contract incompatibility.

### 2. Transactions, commit and recovery

**Status:** Complete — report: [Transactions, commit and recovery](repo-review/subsystems/02-transactions-commit-and-recovery.md)

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

**Status:** Complete — report: [Plugin platform](repo-review/subsystems/03-plugin-platform.md)

Scope:

- the public plugin API, registration builders, handler contracts and execution outcomes;
- query and mutation context adaptation to Workspace;
- plugin discovery, assembly inspection, load contexts, dependency sharing and MEF composition;
- plugin and tool identity, collision handling and startup materialisation;
- plugin-scoped services, query-cache scoping and lifecycle behaviour;
- authoring analysers, package contents and external consumer compatibility; and
- bundled and fixture plugins as direct consumers of the public surface.

Representative traces include loading a valid external plugin, rejecting incompatible or conflicting packages, invoking query and mutation handlers, invalidating cached results and building an external plugin solely from the packaged authoring surface.

Primary risks include binary or source incompatibility, runtime/analyser contract mismatch, dependency identity conflicts, unsafe escaped contexts, cross-plugin state reuse, incorrect isolation claims, handler lifetime races and incomplete package composition.

### 4. Bundled query and mutation tools

**Status:** Complete — report: [Bundled query and mutation tools](repo-review/subsystems/04-bundled-query-and-mutation-tools.md)

Scope:

- every bundled query and mutation tool and its published contract;
- request binding, validation, schema publication and result semantics;
- Roslyn API selection, symbol and span semantics and batch-operation behaviour;
- mutation proposal construction and interaction with the transaction boundary;
- analyser activation and diagnostic reporting;
- caching, continuation guidance, cancellation and resource ownership; and
- unit, component, acceptance and external-repository coverage that claims to prove tool behaviour.

Representative traces must include every tool family and representative success, empty, invalid, stale, cancelled and failure outcomes. Follow each trace from Host publication through plugin execution, Workspace/Roslyn services and any mutation staging or external boundary.

Primary risks include misleading results, incomplete or unstable identity, schema/runtime disagreement, misuse of lower-level Roslyn APIs, incorrect batch semantics, stale snapshot use, partial mutations, hidden capacity failure, excessive repository-scale work and tests that prove only isolated helpers.

### 5. Code Actions

**Status:** Complete — report: [Code Actions](repo-review/subsystems/05-code-actions.md)

Scope:

- provider discovery, MEF composition, analyser activation and policy filtering;
- action discovery, nesting, identity and snapshot-bound replay references;
- Fix All preparation and execution;
- `CodeActionOperation` evaluation and unsupported-operation handling;
- mutation staging through Workspace; and
- Host registrations, adapters, expiry behaviour and provider-focused tests.

Representative traces include listing and replaying nested actions, staging a multi-document action, preparing and executing Fix All, replay after Workspace change and provider output containing unsupported operations.

Primary risks include unstable replay identity, provider-version assumptions, incomplete operation evaluation, stale action reuse, incorrect multi-document changes, cache expiry races, cancellation, MEF resource ownership and divergence between controlled fixtures and real providers.

### 6. Host and protocol

**Status:** Complete — report: [Host and protocol](repo-review/subsystems/06-host-and-protocol.md)

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

### 7. Error reporting and trust boundaries

**Status:** Complete — report: [Error reporting and trust boundaries](repo-review/subsystems/07-error-reporting-and-trust-boundaries.md)

Scope:

- unexpected-error capture, bounded retention and correlation;
- external report projection, redaction and allow-listing;
- prepare, review, consent and submission state transitions;
- logging and Sentry dispatch, network configuration and disposal;
- Workspace lifecycle invalidation and concurrent requests; and
- user-facing availability and status contracts.

Representative traces include capturing an unexpected failure, preparing a report containing sensitive input, consent-required and consent-free submissions, repeated or concurrent submission, Workspace closure and network failure.

Primary risks include secret or source disclosure, consent bypass, time-of-check/time-of-use errors, replay or duplicate submission, unsafe redirects or destinations, unbounded retained data, cross-Workspace correlation and incomplete transport flushing.

### 8. Test and operational infrastructure

**Status:** Complete — report: [Test and operational infrastructure](repo-review/subsystems/08-test-and-operational-infrastructure.md)

Scope:

- unit, integration, audit and acceptance project structure and enforcement;
- shared fixtures, mocks, test assets and published-package consumers;
- Host process and MCP acceptance harnesses;
- external-repository scenario preparation, execution, restoration and cache reuse;
- EventPipe, process and performance metric collection; and
- CI workflows and the relationship between ordinary, audit, acceptance and release-only validation.

Representative traces include a real component fixture lifecycle, published-Host acceptance, repeated external-repository preparation, cancelled or failed child processes, source mutation and restoration, and report production after partial scenario failure.

Primary risks include tests failing before exercising their claim, mocks that hide integration defects, platform-specific false confidence, destructive or incomplete repository restoration, orphaned processes, lost diagnostics, incomparable performance evidence and important suites that are never executed at the required gate.

## Stage 3: Repository-wide validation passes

**Status:** Complete — reports: [Repository-wide validation passes](repo-review/repository-wide-passes.md) and [independent final findings](repo-review/final-findings.md)

After all eight units are complete, perform explicit repository-wide passes for:

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

These passes must trace representative operations again using conclusions established during this review. Earlier review units must be reopened when later evidence changes their risk assessment.

## Review artefacts

Working evidence must be durable and independent of conversation context. Generate a fresh [`repo-review/`](repo-review/) directory containing:

- [`architecture.md`](repo-review/architecture.md) for the current repository map;
- [`review-plan.md`](repo-review/review-plan.md) for execution order and status;
- [`findings.md`](repo-review/findings.md) as the candidate finding ledger;
- one report per numbered review unit under [`subsystems/`](repo-review/subsystems/);
- [`repository-wide-passes.md`](repo-review/repository-wide-passes.md) for the final cross-cutting analysis; and
- [`final-findings.md`](repo-review/final-findings.md) for independently validated results.

Use stable identifiers beginning at `RWMCP2-001`. Every candidate finding must include severity, confidence, exact file and line range, a concrete failure scenario, supporting call path or evidence, affected projects or subsystems and a concise remediation direction.

The candidate ledger must retain status and validation history while the review is active. The final report must remove duplicates, reject candidates that cannot be substantiated against current source and order retained findings by severity and then confidence.

Do not create remediation groupings, implementation plans or completion statuses during the independent review. Those are follow-on activities after the final findings have been accepted.

## Validation expectations

Use the narrowest test or executable evidence that proves or disproves each candidate. Do not modify production code while performing the review. Review-only test execution and temporary diagnostic experiments must follow repository policy and must not change checked-in behaviour.

Tests are evidence, not a substitute for source analysis. A passing test supports a conclusion only when the fixture and assertions exercise the real boundary implicated by the candidate failure scenario.

Areas that depend on platform-specific behaviour, third-party Roslyn providers, external repositories, packaged consumers or network transport must be identified explicitly when the current environment cannot provide representative evidence.

## Completion criteria

The independent deep-dive review is complete when:

- the current architecture map and dependency-ordered review plan are complete;
- every review unit has an implementation-depth report;
- every representative trace has been followed across its complete call path;
- all repository-wide validation passes have been completed;
- every candidate has been independently revalidated against the current source;
- the final report identifies validated findings, notable test gaps and review limitations; and
- no conclusion depends on prior review evidence or Git history.

## Remediation and commit review gate

**Status:** Complete — all 18 remediation worklist items are confirmed complete; the post-remediation release-candidate review remains blocked until the final remediation is committed, the worktree is clean and the complete release validation gate succeeds.

Each validated finding must be remediated as one independently confirmable work item unless the user explicitly approves a combined item. Use the following sequence for every item:

1. Select the first incomplete finding in the approved implementation order and revalidate it against the current source.
2. Explain the concrete failure scenario, representative examples, affected boundaries and supporting call path.
3. Propose the complete production, contract, documentation and test changes, including alternatives and material trade-offs.
4. Obtain explicit user approval before changing production code.
5. Implement the approved change.
6. Run all formatting, build, analyser, unit, integration, acceptance and scenario validation required by repository policy and the affected boundary.
7. Present the complete implementation, code changes and validation evidence to the user for review and await the first confirmation. Answer requests for clarification and implement requested corrections, repeating implementation, validation and user review until the user confirms that the change is ready for independent review.
8. Immediately after that first confirmation, stage every file in the user-approved implementation and verify that the staged diff contains the complete approved change set and no unrelated work. Then spawn a fresh subagent without conversation context and require that subagent to read and invoke the Review Agent skill for an independent final review of the staged baseline. The primary implementation agent must not perform the Review Agent pass itself. Give the subagent only the review target, applicable repository instructions and required review scope. The handoff must list the exact validation commands already run, their results and whether any reviewed file changed afterwards. The review must inspect the surrounding current implementation, direct dependencies and consumers, cross-project contracts, DI/configuration effects and whether the tests exercise the real failure boundary; it must not be limited to superficial diff observations. The subagent must reuse current successful validation evidence and must not repeat equivalent commands against the unchanged baseline unless the evidence is missing or stale, a suspected defect requires reproduction, the existing coverage does not exercise the reviewed behaviour, or a materially different command is needed. Its report must distinguish supplied validation evidence from commands it ran itself. The subagent must remain read-only and return its findings without changing or staging files.
9. Resolve every substantiated Review Agent defect or regression gap, but leave all review-driven corrections unstaged through the second user review. The staged diff must continue to show the implementation the user first confirmed, while the unstaged diff must isolate the corrections introduced because of independent review. Rerun affected validation and, after any material correction, spawn another fresh subagent without conversation context to repeat the Review Agent pass against the staged baseline plus the isolated unstaged corrections that form the proposed final worktree. A finding is not ready for final confirmation while actionable review feedback remains.
10. Present the final implementation, validation evidence and Review Agent outcome to the user for a second review, explicitly summarising every Review Agent finding and its correction. The user must be able to compare the staged first-confirmed baseline with the unstaged review corrections before giving final confirmation.
11. Update the durable finding status only after final confirmation. The user then commits the independently reviewed item so the next remediation begins from a clean worktree.
12. Before selecting the next finding, publish the exact committed `HEAD` to a new dogfood candidate directory, smoke-test that published Host through the MCP client, and promote it to the configured `current` dogfood target only after validation succeeds. Restart the MCP client connection so subsequent repository queries use that committed build. Do not reuse a publication produced from an uncommitted worktree, even when its source is byte-for-byte equivalent to the eventual commit.

If the proposed commit changes after its successful Review Agent pass for anything other than a demonstrably mechanical correction, repeat that pass before the second confirmation and commit. The per-item review is a required commit gate, not a replacement for implementation-time reasoning, executable validation or either user review.

## Post-remediation release-candidate review

**Status:** Blocked — run only after every validated RWMCP2 finding is either confirmed complete or explicitly rejected, each remediation is committed, the worktree is clean and the complete release validation gate has succeeded.

The RWMCP2 remediation will materially change transaction, recovery, Code Action, plugin, Host and test boundaries. After it is complete, use a new agent with no conversation context to perform a fresh independent release-candidate review of the complete current repository. The review should concentrate on whether each remediation closes its complete failure path, interactions between remediations, newly introduced ownership/concurrency/compatibility problems, representative end-to-end operations and whether tests cross the production boundaries they claim to prove, while retaining the repository-wide coverage and independent candidate validation requirements of this programme.

Before starting that review, back up the completed RWMCP2 review artefacts outside the repository evidence boundary and reset the in-repository review workspace without carrying forward findings, conclusions, accepted limitations or remediation decisions. The new reviewer must not inspect Git history, the backed-up RWMCP2 artefacts or prior conversation context. Use a new stable identifier series beginning at `RWMCP3-001` and create fresh architecture, plan, subsystem/cross-cutting evidence, candidate ledger and final findings artefacts. This review is the final independent release-readiness gate; it must not begin early merely to review partially remediated state.
