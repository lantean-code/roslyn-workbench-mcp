# Post-remediation Release-Candidate Deep Dive Review

Date: 2026-08-16

**Review status:** Ready — the repository is clean and the required pre-review release validation has completed on Windows and WSL.

## Purpose

Perform a new implementation-depth review of the complete current repository state before the v1 release. This is not a diff, branch, commit or pull-request review. Existing behaviour is in scope regardless of when it was introduced.

The review must independently establish the current architecture, implementation behaviour and cross-project interactions before evaluating risks. It must not inherit findings, conclusions, accepted limitations, risk assessments or remediation decisions from any earlier review.

## Evidence boundary

Review only the repository's current checked-out tracked state. Do not inspect Git history, prior commits, branches, tags, stashes, reflogs, deleted or renamed review artefacts, external review backups, prior review findings or earlier conversation context. Git may be used only for read-only current-worktree checks, such as identifying tracked files or confirming that the review begins from a clean worktree.

Current normative product and architecture documentation may be used to establish intended behaviour. Historical audits, validation reports, remediation records and prior review conclusions must not be used as evidence. Every conclusion must be independently established from the current implementation, tests, configuration, package definitions and external boundaries.

Do not search for or reconstruct earlier finding identifiers. Use the fresh stable identifier series beginning at `RWMCP3-001`.

## Review principles

Each review stage must:

- inspect the implementation, its direct dependencies and its direct consumers;
- follow representative operations from executable or protocol entry points through every involved project and external boundary;
- validate contracts crossing project, package, process, persistence and filesystem boundaries;
- inspect dependency-injection registrations, service lifetimes and configuration declaration, validation and consumption;
- compare documented behaviour with tests that claim to prove it;
- revisit earlier subsystems whenever later consumers expose an additional risk;
- distinguish implementation defects from missing coverage, documentation drift and intentional product constraints;
- record only substantiated issues with a plausible failure scenario; and
- avoid production-code changes until the complete review and independent finding validation are finished.

Do not report pure formatting preferences, minor style disagreements, generic best-practice suggestions, speculative concerns without a plausible failure path or issues adequately prevented elsewhere in the real call path.

## Stage 1: Current architecture map

**Status:** Incomplete

Inspect the complete repository and write [`repo-review/architecture.md`](repo-review/architecture.md). Map:

- projects and project references;
- dependency direction;
- executable entry points and composition roots;
- major subsystems and responsibilities;
- public, package and cross-project contracts;
- persistence, messaging, filesystem, networking and other external boundaries;
- plugin, analyser and extension mechanisms;
- dependency-injection ownership and important service lifetimes;
- configuration declaration and consumption; and
- test projects and the behaviours or boundaries they claim to cover.

The map must be derived from current project files, source, configuration and tests.

## Stage 2: Dependency-ordered review plan

**Status:** Incomplete

Create [`repo-review/review-plan.md`](repo-review/review-plan.md) from the current architecture map. Divide the repository into the following coherent review units in dependency order. A later unit may reopen an earlier unit when consumer behaviour changes its risk assessment.

### 1. Public contracts and Workspace semantics

Review public selectors, result models, snapshot preconditions and service contracts; Workspace loading and selection; session state, leases and lifecycle transitions; query services, cache identity and invalidation; path identity, external inputs and change detection; and consumers in Plugins, Code Actions and Host.

Trace Workspace open, query acquisition, selector resolution, external-change detection, reload and close.

### 2. Transactions, commit and recovery

Review transaction admission and revisions; mutation validation and linked-document reconciliation; preview, history, rollback and commit planning; filesystem containment, locking and atomic writes; recovery persistence and startup recovery; and every mutation consumer.

Trace single-file and multi-project mutations, concurrent external edits, cancellation before and after durability, partial application, startup recovery and cross-process contention.

### 3. Plugin platform

Review the public plugin API, registration and handler contracts; Workspace context adaptation; discovery, assembly inspection and load contexts; dependency sharing and MEF composition; plugin identity and collisions; plugin-scoped services and caching; authoring analysers and package contents; and bundled or fixture consumers.

Trace valid and invalid plugin loading, query and mutation execution, cache invalidation, service disposal and an external consumer built only from the packaged authoring surface.

### 4. Bundled query and mutation tools

Review every bundled tool and published contract; binding, validation, schema and result semantics; Roslyn API selection; symbol, document and span identity; batch behaviour; mutation proposals; caching, continuation guidance and cancellation; and the tests or external scenarios that claim to prove each tool family.

Trace representative success, empty, invalid, stale, bounded, cancelled and failure outcomes from Host publication to Roslyn or mutation staging.

### 5. Code Actions

Review provider discovery, MEF composition, analyser activation and filtering; action identity and snapshot-bound references; discovery, nesting and replay; Fix All preparation and staging; operation evaluation; Workspace mutation integration; expiry and caching; and controlled plus real-provider tests.

Trace nested discovery and replay, multi-document staging, Fix All preparation and replay, changing provider output, Workspace changes, unsupported operations and exact source-byte preservation.

### 6. Host and protocol

Review startup and shutdown; MSBuild registration; configuration precedence and validation; dependency-injection registrations and lifetimes; MCP binding, validation, schemas, envelopes and serialization; every adapter family; lifecycle tools; stdout protocol integrity; stderr diagnostics; and exception handling.

Trace clean and invalid startup, every adapter family, malformed input, cancellation, expected protocol failures, unexpected failures and graceful stdin shutdown with open resources.

### 7. Error reporting and trust boundaries

Review unexpected-error capture and bounded retention; request and Workspace attribution; redaction and allow-listing; preparation, consent and submission transitions; logging and Sentry dispatch; lifecycle invalidation; concurrency; and user-visible status contracts.

Trace capture before and after Workspace acquisition, sensitive input projection, consent-required and pre-authorised submissions, stale prepared reports, concurrent submission, Workspace closure and transport failure.

### 8. Test and operational infrastructure

Review unit, integration, audit and acceptance structure; shared fixtures and packaged consumers; published-Host harnesses; external-repository scenario preparation, cache reuse, execution and restoration; process and EventPipe ownership; report production; CI workflows; and suite triggering.

Trace a real component fixture, published Host acceptance, first and repeated repository preparation, cancelled and failed child processes, source mutation and restoration, crash recovery and report production after failure.

## Stage 3: Repository-wide validation passes

**Status:** Incomplete

After all subsystem reviews, write [`repo-review/repository-wide-passes.md`](repo-review/repository-wide-passes.md) covering explicit passes for:

1. cross-project and package contract mismatches;
2. dependency direction and abstraction ownership;
3. end-to-end behaviour across project boundaries;
4. dependency-injection registration and lifetime consistency;
5. configuration declaration, precedence, validation and consumption;
6. error, cancellation, continuation and retry propagation;
7. concurrency, shared state, cache coalescing and thread safety;
8. transaction, persistence and cross-process consistency;
9. serialization, schema, binary and package compatibility;
10. security and trust boundaries;
11. resource ownership and disposal;
12. performance problems with plausible repository-scale impact;
13. missing or misleading integration, acceptance, audit and scenario coverage; and
14. duplicate, conflicting, unreachable or partially implemented behaviour.

Trace representative operations again across the complete repository. Reopen subsystem reports when repository-wide evidence changes an earlier conclusion.

## Stage 4: Independent candidate validation

**Status:** Incomplete

Maintain [`repo-review/findings.md`](repo-review/findings.md) as the durable candidate ledger throughout the review. Every candidate must have a stable `RWMCP3-###` identifier and include:

- severity;
- confidence;
- exact file and line range;
- a concrete failure scenario;
- supporting call path or evidence;
- affected projects or subsystems;
- a concise remediation direction; and
- validation or rejection history.

After the subsystem and repository-wide passes, independently retrace every candidate against current source. Remove duplicates, reject candidates that cannot be substantiated and ensure no issue is already prevented elsewhere in the complete call path.

Write the final validated report to [`repo-review/final-findings.md`](repo-review/final-findings.md), ordered by severity and then confidence. Include a concise repository-level assessment, validated findings, notable test gaps and areas that could not be reviewed confidently.

Do not create remediation groupings, implementation plans or completion statuses during the independent review. Those are follow-on activities after the final findings have been accepted.

## Review artefacts

All working evidence must be durable and independent of conversation context. Create a fresh [`repo-review/`](repo-review/) workspace containing:

- [`architecture.md`](repo-review/architecture.md) for the current repository map;
- [`review-plan.md`](repo-review/review-plan.md) for execution order and status;
- [`findings.md`](repo-review/findings.md) for the candidate ledger;
- one report per numbered unit under [`repo-review/subsystems/`](repo-review/subsystems/);
- [`repository-wide-passes.md`](repo-review/repository-wide-passes.md) for cross-cutting analysis; and
- [`final-findings.md`](repo-review/final-findings.md) for independently validated results.

Do not inspect or reuse content from any removed, renamed or externally backed-up review artefact.

## Validation expectations

Use the narrowest executable evidence that proves or disproves each candidate. Do not modify production code during the review. Review-only tests and temporary diagnostic experiments must follow repository policy and must not change checked-in behaviour.

Tests are evidence only when the fixture and assertions exercise the real boundary implicated by the candidate failure scenario. Identify areas that depend on platform-specific behaviour, third-party providers, external repositories, packaged consumers or network transport when representative evidence is unavailable.

Reuse current successful validation evidence only when it is supplied to the reviewer, remains current for the exact reviewed baseline and genuinely exercises the relevant boundary. Do not rerun equivalent expensive suites merely to reproduce unchanged evidence.

## Completion criteria

The review is complete only when:

- the architecture map and dependency-ordered plan are complete;
- every review unit has an implementation-depth report;
- representative traces cross their complete production boundaries;
- every repository-wide pass is complete;
- every candidate has been independently revalidated;
- the final report contains validated findings, notable test gaps and review limitations; and
- no conclusion depends on prior review evidence, Git history or earlier conversation context.

## Finding-remediation and commit review gate

After the final RWMCP3 findings are accepted, remediate each approved work item through this sequence:

1. Select the first incomplete finding in the approved implementation order and revalidate it against current source.
2. Explain the failure scenario, examples, affected boundaries and evidence.
3. Propose the complete production, contract, documentation and test design, including alternatives and material trade-offs.
4. Obtain explicit user approval before changing production code.
5. Implement the approved change and run the required formatting, build, analyser and test validation.
6. Present the implementation and evidence for the user's first code review; address requested corrections until the user confirms it is ready for independent review.
7. Stage the complete first-confirmed baseline, verify its scope, then send it to a fresh context-free Review Agent subagent. Supply exact current validation evidence so equivalent commands are not repeated unnecessarily.
8. Keep any review-driven corrections unstaged so the user can compare them with the first-confirmed staged baseline. Revalidate and repeat the fresh Review Agent pass after material corrections.
9. Present the final implementation, review findings and corrections for the user's second review and final confirmation.
10. Update durable status only after final confirmation. The user commits the independently reviewed item from a clean, fully staged baseline.
11. Before selecting another item, publish the exact committed `HEAD` to a new dogfood candidate, smoke-test it, promote the configured `current` target and restart the MCP client connection.

No finding is ready to commit while actionable Review Agent feedback or a material validation gap remains.
