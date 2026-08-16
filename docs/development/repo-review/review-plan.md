# RWMCP3 dependency-ordered review plan

Date: 2026-08-16

**Review status:** Complete; all subsystem reviews, repository-wide passes and independent candidate validation finished on 2026-08-16.

## Scope and evidence boundary

This plan covers the complete current repository state described by [`architecture.md`](architecture.md). It is a whole-repository review, not a diff, branch, commit or pull-request review. Evidence is restricted to current source, project/package definitions, current normative product documentation, configuration, workflows, tests, fixtures, inert test assets and executable behaviour. Do not inspect or use Git history, prior commits, branches, tags, stashes, reflogs, deleted or renamed review artefacts, external backups, historical audit conclusions, earlier findings or conversation context.

Each unit must inspect its own implementation with its direct dependencies and consumers, follow representative operations through every involved project and external boundary, compare documented behaviour with tests that claim coverage, and record only substantiated candidates. Production code remains unchanged until all units, repository-wide passes and independent candidate validation are complete.

## Execution and durable evidence

| Order | Review unit | Durable report | Status |
| ---: | --- | --- | --- |
| 1 | Public contracts and Workspace semantics | `subsystems/01-public-contracts-and-workspace-semantics.md` | Completed |
| 2 | Transactions, commit and recovery | `subsystems/02-transactions-commit-and-recovery.md` | Completed |
| 3 | Plugin platform | `subsystems/03-plugin-platform.md` | Completed |
| 4 | Bundled query and mutation tools | `subsystems/04-bundled-query-and-mutation-tools.md` | Completed |
| 5 | Code Actions | `subsystems/05-code-actions.md` | Completed |
| 6 | Host and protocol | `subsystems/06-host-and-protocol.md` | Completed |
| 7 | Error reporting and trust boundaries | `subsystems/07-error-reporting-and-trust-boundaries.md` | Completed |
| 8 | Test and operational infrastructure | `subsystems/08-test-and-operational-infrastructure.md` | Completed |

Every report must distinguish implementation evidence, test claims, executable evidence actually obtained, package-owned or external behaviour not inspected, and open cross-unit questions. Each must end with assumptions exported to consumers, consumer evidence still required, candidate identifiers raised or affected, earlier units reopened, later units required to revisit it, and report status.

## Candidate ledger and reopening

[`findings.md`](findings.md) is the sole durable candidate ledger. Allocate `RWMCP3-###` identifiers monotonically and never reuse them. Each entry records status, severity, confidence, exact location, concrete scenario, call path/evidence, affected subsystems, remediation direction, originating unit and validation history. Link candidates from subsystem reports. Record corroboration, contradiction, duplication and rejection in the original entry; keep candidate status separate from subsystem completion. Rejected and duplicate candidates remain in the ledger but not the final report.

When later consumer evidence changes an earlier assumption, mark the earlier report `Reopened`, add a cross-unit addendum naming the consumer and complete call path, retrace implementation and tests, update the ledger and return the report to `Completed` only after resolution. Later units must not silently override earlier reports.

## Unit 1: Public contracts and Workspace semantics

**Durable output:** `subsystems/01-public-contracts-and-workspace-semantics.md` — **Completed**.

**Scope:** All Workspace-facing contracts in Abstractions; Workspace caching, change detection, configuration, coordination, diagnostics, contexts, hierarchy, lifecycle, loading, operations, paths, projects, references, resolution, results, selection, selectors and state; read-side IO identity; Host Workspace lifecycle/status consumers; and plugin/Code Action consumers of identity, leases and snapshots.

**Implementation:** Open/root resolution and allowlisted MSBuild properties; compatibility filtering; evaluated-input certification; admission/capacity/aliases; session snapshots; gates and leases; effective solutions and snapshot identities; lifecycle transitions; caches and invalidation; watcher/fingerprint detection; instance status; reload, close and shutdown.

**Dependencies:** Roslyn Workspaces and `MSBuildWorkspace`; filesystem abstractions and native path semantics; solution persistence; Abstractions; Host filesystem/options/MSBuild factories.

**Consumers and contracts:** Plugins, bundled tools, Code Actions, Host lifecycle/transaction/status/error attribution, integration/acceptance/scenarios. Verify selector omission/exclusivity, Workspace/epoch/revision/snapshot identity, UTF-16 locations, bounded results, effective-solution rules, external read-only inputs, operation errors and lifecycle projection.

**DI and configuration:** Audit `AddWorkspaceServices`, options projection, singleton loaders/lifecycle/session/resolution/cache/change/path/coordination services, Workspace-session resources and invocation leases. Trace result defaults, Workspace/query capacity, cache capacity/expiry, state directory and workspace-open MSBuild property retention across reload.

**Claimed tests:** Workspace unit and integration areas for caches, change detection, lifecycle, loading, paths, projects, resolution, selection/state/validation; Host Workspace tools/status/lifecycle; Workspace acceptance classes; IntegrationTestSupport and Workspace assets.

**Required traces:** Workspace open end to end; ordinary query acquisition/disposal; every selector family including stale/ambiguous cases; watcher/fingerprint invalidation; status; reload with new epoch; close and shutdown under active-query/out-of-date/transaction variants.

**External boundaries and risks:** Trusted MSBuild, in-root/external inputs, case/separators/symlinks, watcher delivery, polling, diagnostics and coordination files. Test snapshot consistency, identity agreement, partial/disposed sessions, external read-only enforcement, change-detection races, gate/cancellation safety and truthful result/continuation mapping.

**Dependencies between units:** No earlier unit. Units 2–7 may reopen it; Unit 8 validates its real-boundary claims.

## Unit 2: Transactions, commit and recovery

**Durable output:** `subsystems/02-transactions-commit-and-recovery.md` — **Completed**.

**Scope:** Workspace Transactions, Recovery, mutation IO and transaction-bearing state; lifecycle/change-detection integration; Host transaction tools/contracts; startup recovery/status; every plugin, bundled and Code Action mutation producer; normative transaction documentation.

**Implementation:** Global transaction admission, identities/history, start/preview/undo/redo/rollback, snapshot guards, candidate validation/identity/staging, graph and linked-document reconciliation, commit planning, byte/encoding preservation, containment/locking, recovery persistence, disk revalidation, cancellation boundary, atomic create/replace/delete, applied-state certification, snapshot promotion, cleanup and startup recovery.

**Dependencies:** Unit 1; Roslyn solution changes; filesystem/native atomic replacement; DiffPlex; state security and OS locks.

**Consumers and contracts:** Rename/format, Code Actions/Fix All, external mutations, Host tools, startup/status, acceptance, lock fixture and commit/conflict/cancellation/crash scenarios. Verify mutation candidates/preconditions/identity, revision progression, preview contracts, linked documents, representation preservation, cancellation, envelopes and sole-write-path guarantees.

**DI and configuration:** Audit all transaction/staging/validation/planning/content/lock/writer/recovery singletons; process-global owner, session history, operation lock and durable recovery lifetimes. Trace revision maximum, state directory, recovery limits, lock paths, retry and platform policy.

**Claimed tests:** Workspace unit/integration Transactions, Recovery, IO, State and ChangeDetection; lock fixture; Host transaction/startup/status; mutation/durable/recovery acceptance; plugin/Code Action staging integration; scenario commit families.

**Required traces:** Single and multi-project/linked mutation; external plugin and Code Action staging; edits before/during commit phases; cancellation around durability; all atomic operations and partial application; recovery states including malformed/inaccessible; process contention; bounded history.

**External boundaries and risks:** Source/state bytes, durability/permissions, JSON/artefacts, timestamps/hashes, reparse points, atomic capabilities, process termination. Test bypasses, graph/byte integrity, recoverable cancellation, partial-state classification, TOCTOU, stranded ownership/locks and exact invalidation.

**Dependencies between units:** Depends on Unit 1. Mutation consumers in Units 3–6 may reopen it; Unit 8 validates native/crash boundaries.

## Unit 3: Plugin platform

**Durable output:** `subsystems/03-plugin-platform.md` — **Completed**.

**Scope:** Plugins and Plugins.Analyzers; Host PluginLoading, catalogue and plugin adapters; package definitions; compiled fixtures and package-only consumer; current authoring/discovery documentation.

**Implementation:** Public attributes/API/configuration/handlers/contexts/results/cache/services; analyser rules; package discovery/containment; metadata admission; load contexts/dependencies; MEF; identity/API/collisions; schema preflight; provider construction; closed generic materialisation; catalogue publication; invalidation and disposal.

**Dependencies:** Units 1–2, Roslyn, Composition, DI, Host schemas, filesystem/assembly loading and NuGet layout.

**Consumers and contracts:** Bundled core, external fixtures/consumer, Host startup/adapters and third parties. Verify packed assemblies/analyser, public docs/API, versioning, names/metadata, request/response admission, visitor/materialisation, service lifetimes, context/mutation mapping, cache restrictions, protocol exceptions and absence of implementation/MCP types.

**DI and configuration:** Audit Host discovery/loading/catalogue singletons and per-plugin validated singleton providers; handler thread safety, catalogue retention, invocation contexts and reverse disposal. Trace plugin directories, path deduplication, cache settings, API metadata, reserved names and dependency-sharing policy.

**Claimed tests:** Plugins and analyser unit projects; Host PluginLoading tests; Host integration plugin/package tests; external-plugin and distribution acceptance; all compiled fixtures and package consumer.

**Required traces:** Valid bundled/external admission; every invalid startup phase; managed/native dependency resolution; query/cache execution; mutation/illicit Workspace mutation; lifecycle invalidation; reverse shutdown; pack and compile external consumer.

**External boundaries and risks:** Trusted code, directories, PE metadata, load contexts, NuGet/analyser execution, stdout and private dependencies. Test pre-admission execution, type identity splits, analyser/runtime agreement, deferred invalidity, cache safety, provider leaks and package sufficiency.

**Dependencies between units:** Depends on Units 1–2. Units 4, 6 and 8 may reopen it.

## Unit 4: Bundled query and mutation tools

**Durable output:** `subsystems/04-bundled-query-and-mutation-tools.md` — **Completed**.

**Scope:** All Plugins.Core services/contracts/bases/handlers/projections/helpers and its 37 queries plus rename/format mutations; AsyncFixer provisioning; direct contracts from Units 1–3 and Host publication.

**Implementation:** Every request/metadata/validation path; Roslyn API choice; identity; deterministic ordering; bounds/counts/continuations; compilation/semantic/flow work; diagnostics/analysers; graph/batch work; cancellation/cache; mutation candidate and representation.

**Dependencies and consumers:** Units 1–3; Roslyn APIs; AsyncFixer; Host schemas/serialization; MCP clients, Code Action/Workspace selector consumers, integration/acceptance and scenario runner.

**Contracts, DI and configuration:** Verify names, JSON/schema, annotations, snapshots/selectors, bounds/empty semantics, warnings/continuations/cancellation and staging. Audit plugin-scoped services and singleton handlers for thread safety. Trace global and tool-specific limits/depths and ensure they bound relevant work and response semantics.

**Claimed tests:** Plugins.Core unit/integration; published inspection/catalogue/schema/selector/mutation acceptance; component helpers/assets; bounded/determinism/profile/mutation scenarios.

**Required traces:** Representative structural, navigation, relationship, diagnostic and flow tools; shared success/error/stale/bounded/cancelled paths; multi-project/linked/TFM selection; all cache outcomes; AsyncFixer activation; rename/format through commit; repository-scale deterministic limits.

**External boundaries and risks:** Roslyn/compiler/analysers, encodings, large graphs, generated/external documents and JSON projection. Test identity, bounded intermediate work, API scope, unsupported/linked documents, shared-base propagation, cache keys and transaction-only writes.

**Dependencies between units:** Depends on Units 1–3. Units 6 and 8 may reopen it.

## Unit 5: Code Actions

**Durable output:** `subsystems/05-code-actions.md` — **Completed**.

**Scope:** Entire CodeActions project; Workspace mutation dependencies; Host Code Action visitor/adapters/catalogue; current Code Action documentation.

**Implementation:** MEF/provider/analyser composition; diagnostics/policy/discovery/nesting/projection; opaque references/expiry; snapshot recipes and replay; Fix All preparation/replay; operation evaluation; candidate identity/representation; staging and consumption.

**Dependencies and consumers:** Units 1–2, Roslyn Features/CodeFix/Refactoring, Composition, project analysers, cache/time and Host. Consumers are Host/MCP, Workspace transactions, fixtures, audit, acceptance and scenarios.

**Contracts, DI and configuration:** Verify catalogue metadata/visitor, UTF-16 selections, IDs/recipes/snapshot binding, provider/nested identity, Fix All scope/preconditions/limits, operation rejection and Host projection; absence from plugin API/status. Audit Code Action singletons, typed registrations, lifecycle observer and invocation contexts. Trace reference lifetime/capacity, diagnostic/action/change limits and expiry/capacity rejection.

**Claimed tests:** CodeActions unit/integration/audit; Host adapters/schema/catalogue; CodeAction acceptance; controlled providers/assets/scenarios.

**Required traces:** Nested fix/refactoring discovery; replay outcomes including changed/missing state; all Fix All scopes and changing provider output; multi-document staging; unsupported operations/cancellation/provider failure; lifecycle/expiry/concurrent invalidation; exact representation through commit.

**External boundaries and risks:** Version-sensitive Roslyn providers, project analysers, nondeterminism, encodings, cache and opaque IDs. Test stale replay, identity ambiguity, prepared-output changes/limits, unsafe operations, duplicate staging and graceful composition degradation.

**Dependencies between units:** Depends on Units 1–2; Units 6 and 8 may reopen it.

## Unit 6: Host and protocol

**Durable output:** `subsystems/06-host-and-protocol.md` — **Completed**.

**Scope:** Program, Hosting, Configuration, Contracts, Protocol, server-owned lifecycle/transaction/status tools, all four adapters, startup publication and top-level exception boundary.

**Implementation:** Stdout isolation; Host/options/DI; MSBuild/state/recovery/plugin startup order; static/dynamic catalogue; binding/nested validation; schemas/annotations; serialization/envelopes; adapter acquisition/staging/mapping; exceptions/cancellation; lifecycle tools; stdin shutdown/cleanup.

**Dependencies and consumers:** Units 1–5; MCP SDK, Hosting/DI/Options/Logging, Build Locator, JSON and stdio. Consumers are official clients, acceptance/scenarios, plugins and users.

**Contracts, DI and configuration:** Verify every published/reserved name, catalogue merging, JSON/schema/runtime agreement, envelopes/continuations, protocol errors, cancellation, correlations, attribution, lifecycle DTOs and instructions. Audit the complete container and hosted-service order. Trace every startup input, precedence/fallback, options/status and build-time Sentry configuration.

**Claimed tests:** Host Architecture/Configuration/Contracts/Hosting/Protocol/Status/ToolExecution/Tools; Host integration Hosting/Protocol; published executable/lifetime/protocol/catalogue/schema/startup/failure acceptance; official-client harnesses.

**Required traces:** Clean and invalid startup; all adapter/server-owned families; malformed/unsupported serialization; cancellation phases; expected/protocol/unexpected errors; EOF with resources; plugin console output.

**External boundaries and risks:** SDK merging/JSON-RPC, stdio/stderr, process EOF, MSBuild, assemblies, environment and disposal. Test exact catalogue, stdout integrity, schema/binder agreement, failure semantics, readiness ordering, DI/resource safety and graceful durable shutdown.

**Dependencies between units:** Depends on Units 1–5 and may reopen them. Units 7–8 extend/validate it.

## Unit 7: Error reporting and trust boundaries

**Durable output:** `subsystems/07-error-reporting-and-trust-boundaries.md` — **Completed**.

**Scope:** ErrorReporting; exception filter/details; conditional tools; status/config/composition; Workspace attribution and lifecycle; Sentry configuration; current error documentation.

**Implementation:** Capture/projection/size/bounds/expiry; correlations; request/Workspace attribution; local details; allowlisting/redaction; immutable provider payload/preview; prepared handles; consent/elicitation; serialized/idempotent submission; stale preparation; stderr/Sentry dispatch; invalidation and failures.

**Dependencies and consumers:** Units 1 and 6; stores/time/JSON/hashing/Sentry. Consumers are unexpected calls, reporting tools/status, clients, stderr/Sentry and tests.

**Contracts, DI and configuration:** Verify truncation/correlation, allowlist, preview-byte-digest identity, expiry/single submission, consent/epoch binding, tool omission, Sentry envelope and transport failures. Audit singleton stores/services/observer/dispatcher and per-handle serialization. Trace consent, capacities/lifetimes/sizes, build-time DSN, environment warning and fail-closed publication/status.

**Claimed tests:** Host ErrorReporting/filter/config/registration/status; Host integration Sentry/attribution; published failure acceptance; throwing fixtures.

**Required traces:** Failures before/after acquisition; sensitive projection; immutable preparation; all consent paths; repeated/concurrent submission; expiry/stale lifecycle; stderr/Sentry outcomes; capacity/truncation/size races.

**External boundaries and risks:** Sensitive data, elicitation, stderr, HTTPS/SDK, DSN, retention and consent. Test disclosure, preview/dispatch equivalence, replay, consent scope, capture recursion, bounded diagnostic value and truthful availability.

**Dependencies between units:** Depends on Units 1 and 6; Unit 8 may reopen it.

## Unit 8: Test and operational infrastructure

**Durable output:** `subsystems/08-test-and-operational-infrastructure.md` — **Completed**.

**Scope:** Every test/support/fixture/asset project; acceptance infrastructure/wrappers; complete ScenarioRunner; CI workflows, test-count script and repository build/test configuration; current testing policy.

**Implementation:** Taxonomy/categories/ownership; support boundaries; fixture fidelity; mock versus real seams; package consumers/assets; MSBuild/temp cleanup; published Host/client/process lifetime; wrapper parity; external cache/preparation/NuGet isolation; mutation/restoration; child cancellation; EventPipe; crash observation; reporting; CI selection/counts.

**Dependencies and consumers:** All production units plus SDK/test framework/MSBuild/MCP client/Git/network/process/filesystem/EventPipe/CI. Consumers are developers, reviewers, CI and every report relying on evidence.

**Contracts, DI and configuration:** Verify references/categories, support leakage, fixture/package fidelity, asset exclusion, acceptance publish/copy/wrapper contracts, official client, scenario schema/pins/cache/restoration/reports and CI matrix. Inspect every helper-created production container and all fixture/process/session/cache lifetimes. Trace SDK/packages/categories/filters/artifacts/fixtures/scenarios/repos/env/timeouts/platform choices.

**Claimed evidence:** Cross-check every Units 1–7 claim against owning unit/contract, four components, audit, Host integration, acceptance, fixtures/package consumers, scenarios and workflows. Names/traits/descriptions alone are not proof.

**Required traces:** Real component fixture; published Release Host; first/repeated external preparation; failed/cancelled child; mutation restoration; crash/recovery; EventPipe races; failure report; CI trigger/filter/matrix/count.

**External boundaries and risks:** Git/network/NuGet, process trees/pipes/EventPipe, permissions/links/locks, platforms/CI, publish layout/temp/cache/results. Test real-boundary fidelity, hidden helper defects, suite selection, distribution parity, drift, cache safety, cleanup and diagnostic preservation.

**Dependencies between units:** Depends on all units and may reopen any of them.

## Repository-wide sequencing

After all unit reports are `Completed` and reopenings resolved, write `repository-wide-passes.md` covering in order: cross-project/package contracts; dependency direction; end-to-end behaviour; DI/lifetimes; configuration; error/cancellation/continuation/retry; concurrency/cache/thread safety; transaction/persistence/process consistency; serialization/schema/binary/package compatibility; security/trust; resources/disposal; plausible-scale performance; missing/misleading evidence; and duplicate/conflicting/unreachable/partial behaviour.

Each pass retraces complete operations, links reports and candidates, and reopens units when assumptions change. Stage 3 completes only when reopenings are resolved or recorded as evidence limits.

## Independent candidate validation sequencing

After Stage 3, freeze the ledger candidate set and independently retrace every candidate against current implementation, dependencies, consumers, configuration, DI, tests and full call path. Use the narrowest executable evidence and do not repeat equivalent expensive evidence supplied for the unchanged boundary. Reject unsubstantiated, duplicate or already-prevented issues; reopen reports for incorrect assumptions. Write only validated non-duplicates to `final-findings.md`, ordered by severity then confidence, with repository assessment, notable gaps and limitations. Do not add remediation grouping, implementation plans or completion statuses until the final report is accepted.
