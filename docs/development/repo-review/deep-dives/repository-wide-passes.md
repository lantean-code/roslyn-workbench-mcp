# Deep-dive repository-wide passes

Date: 2026-07-31

Status: Complete

## Scope and outcome

The final review reopened the conclusions from all seven implementation-depth units and traced the current repository from public contracts through Workspace, Plugins, Plugins.Core, CodeActions, the Host, persistence and operational validation. Every retained and rejected candidate was checked again against current source and its direct consumers. Twenty-nine active findings remain: two P1, twenty-five P2 and two P3. RWMCP-015 and RWMCP-024 remain rejected. No new candidate, duplicate requiring a new identifier or P0 defect was substantiated.

The older repository-review ledger was also reconciled. RWMCP-001, RWMCP-004, RWMCP-005 and RWMCP-007 remain resolved. Current source and 30 focused Plugins.Core tests show that RWMCP-002, RWMCP-003 and RWMCP-006 have also been remediated: logical-line counting normalises all newline forms, async analysis excludes nested executable bodies, and disposable analysis requires an unconditional reachable disposal region or supported `finally` pattern. None of those older findings belongs in the current active set.

## Representative end-to-end traces

### Workspace open, query and reload

`Program` → `HostStartupComposer` → Generic Host startup prerequisites → `WorkspaceOpenTool` → `WorkspaceLifecycleService` → `WorkspaceLoadWorkflow`/`MSBuildWorkspace` → advisory instance publication → input-manifest capture → `WorkspaceSessionStore` → plugin or Code Action query adapter → shared operation lease → snapshot-scoped resolver/services/cache → handler → structured MCP result. The ordinary ownership, lease, cache and cancellation boundaries are coherent. RWMCP-008 remains because Solution evaluation and manifest capture are not one certified observation; RWMCP-009 remains because the public identity tuple repeats after restart; RWMCP-010 through RWMCP-012 cover path/cancellation contract gaps; and RWMCP-013 remains because successful reload replaces local state without republishing `Ready` cross-process.

### Plugin query and mutation

Startup package discovery → PE inspection → package load context → MEF entry-point composition → plugin configuration/preparation → collision policy → typed registration materialisation → Host singleton adapter → Workspace query or mutation lease → plugin context → handler → query serialisation or Workspace-owned mutation staging. Project/package direction and the trusted in-process model remain coherent, and retained contexts do not expose a live staging capability after lease disposal, so RWMCP-024 remains rejected. RWMCP-020 through RWMCP-023 remain at the preparation and discovery boundaries. Runtime publication also reconnects plugin contracts to RWMCP-027, RWMCP-028 and RWMCP-030: nested selectors are not semantically validated, Full query schemas contradict null no-change results, and direct plugin stdout can corrupt the stdio transport.

### Code Action discovery, replay and Fix All

Host Code Action query adapter → Workspace lease → request resolution → provider/analyser discovery → action flattening and policy → GUID-backed replay recipe → list response; then mutation adapter → exclusive lease → exact snapshot/reference replay → operation evaluation → Workspace candidate processing → transaction revision. Fix All adds provider-scoped diagnostic collection, candidate evaluation, impact measurement and a second replay recipe before the same staging path. Exact replay, lifecycle invalidation and source-only staging remain coherent. RWMCP-025 and RWMCP-029 reinforce each other: advertised safe retries create additional random references, increasing the cache pressure that can silently erase applicable actions. RWMCP-026 retains plausible repository-scale impact because project Fix All repeatedly runs whole-compilation diagnostics per document during both preparation and staging.

### Transaction commit and recovery

Mutation candidate → `WorkspaceMutationCandidateProcessor` → revision append → `transaction-commit` → exclusive Workspace lease → commit plan → non-blocking cross-process lock → owner/artifact/manifest persistence → revalidation → non-cancellable application/restoration boundary → session promotion → cleanup and lifecycle invalidation. The ordering, containment, lock and partial-apply recovery model remain sound, and the non-blocking lock disproves RWMCP-015. RWMCP-014 is still the highest-severity consistency defect because asymmetric linked deletion reaches physical application and an inconsistent promoted Solution. RWMCP-016 through RWMCP-019 remain distinct: metadata loss, unstructured recovery-limit rejection, aggregate plan memory and adjacent-change rejection have separate failure boundaries. Commit promotion independently exercises RWMCP-008 when manifest capture occurs after file application.

### Unexpected failure and external reporting

Tool exception → common filter → bounded local record → local details → allow-listed projection → immutable prepared payload/digest → consent evaluation → store acquisition → logging or Sentry dispatch → receipt. Local/external model separation, opaque handles, sequential same-handle exclusion and HTTPS destination policy remain coherent. RWMCP-031 remains P1 because the real Sentry client enriches the event after the reviewed preview boundary. RWMCP-032 remains physical sensitive-memory retention after logical expiry. RWMCP-033 remains a cross-service authorisation race between consent invalidation and dispatch acquisition.

### Published Host and external scenario validation

CI publish wrapper → isolated published executable → official MCP stdio client → Workspace/plugin/transaction calls → bounded shutdown and retained diagnostics provides a genuine distribution boundary. The ScenarioRunner path is repository selection → shared pinned checkout → preparation → Host/process diagnostics → query or destructive scenario → restoration/validation → result writing. RWMCP-034 and RWMCP-038 combine to mean the runner cannot currently certify the exact checkout passed to the Host under preparation side effects or concurrent runs. RWMCP-035, RWMCP-036 and RWMCP-037 separately invalidate commit trace capture, partial evidence retention and cancellation-gate correctness.

## Explicit repository-wide passes

### 1. Cross-project and package contract mismatches

The production project graph remains acyclic and public neutral contracts remain in Abstractions. `ITypeHierarchyService`, resolver, project structure, target-framework and reference-discovery services are implemented in Workspace and consumed through the plugin-facing abstraction boundary. No new binary or source contract mismatch was found. Active mismatches are already represented by RWMCP-009, RWMCP-010, RWMCP-012, RWMCP-020 through RWMCP-022, RWMCP-027 and RWMCP-028.

### 2. Dependency direction and abstraction ownership

Abstractions has no implementation dependency; Workspace owns mutable workspace and persistence state; Plugins owns third-party contracts and adapters; CodeActions remains an internal Host catalogue; Plugins.Core is a bundled consumer; and only the Host references the MCP SDK. The ScenarioRunner has no production project reference and uses the published protocol. No incorrect project reference, circular dependency or misplaced public implementation abstraction was substantiated.

### 3. End-to-end behaviour across projects

Workspace open/query/reload, plugin query/mutation, Code Action list/stage/Fix All, transaction commit/recovery, unexpected-error reporting and published/scenario execution were retraced as described above. Later consumers broadened RWMCP-008 to commit promotion and connected RWMCP-025 with RWMCP-029 and RWMCP-034 with RWMCP-038, but did not create a distinct additional defect.

### 4. Dependency-injection registration and lifetime consistency

The complete Host graph uses singleton catalogues, handlers and synchronised state owners with per-call leases and contexts. Framework-owned `TimeProvider`, `IHostApplicationLifetime`, Sentry client disposal and hosted startup prerequisites are registered once with correct alias identity. No captive scoped dependency, temporary provider, double-owned disposable or missing current registration was substantiated. RWMCP-021 remains a late-construction failure caused by schema preflight absence rather than a lifetime error.

### 5. Configuration declaration, precedence, validation and consumption

Every `StartupOptions` property is resolved from its documented command-line/environment source, synchronously validated, projected into its owning options type and consumed by the appropriate service. Error-reporting consent intentionally excludes environment configuration and invalid values fail closed. Startup state-directory initialisation precedes request admission. No accepted-but-unused or precedence defect was found. RWMCP-028 is a configured Full-schema behaviour contradiction, not an unused option.

### 6. Error, cancellation and retry propagation

MCP cancellation flows through adapters, Workspace acquisition, Roslyn/provider execution and staging; the common filter preserves `OperationCanceledException`. Commit deliberately stops observing cancellation after application begins and uses failure-safe restoration. Expected plugin/package failures are isolated into status where intended. Active defects remain RWMCP-011, RWMCP-017, RWMCP-029, RWMCP-035 through RWMCP-037. No additional swallowed exception, unsafe post-application cancellation or retry duplication path survived validation.

### 7. Concurrency, shared state, cache coalescing and thread safety

Workspace session state, operation gates, query caches, Code Action references, error stores, consent and prepared submissions each synchronise their mutable state. Query cache misses coalesce and waiter cancellation does not cancel other waiters. Cross-owner atomicity is missing only in the retained RWMCP-033 authorisation race; Code Action capacity semantics remain RWMCP-025; shared external checkout concurrency remains RWMCP-038. No additional lock-order, duplicate dispatch or cross-plugin cache leak was substantiated.

### 8. Transaction, persistence and cross-process consistency

One global in-process transaction owner, exclusive mutation leases, exact revision/snapshot guards, physical containment, durable recovery states, non-blocking OS locks and reverse restoration form a coherent consistency model. RWMCP-014 and RWMCP-016 through RWMCP-019 remain the discrete persistence defects; RWMCP-008 covers certification of the promoted Solution/manifest pair; RWMCP-013 covers stale advisory state. The rejected orphan race RWMCP-015 remains impossible under non-blocking acquisition.

### 9. Serialisation, schema, binary and package compatibility

System.Text.Json binding and the MCP SDK schema provider agree for ordinary supported contracts; success/failure envelopes remain distinguishable; package identity sharing and clean external package-consumer builds cover the authoring surface. RWMCP-020, RWMCP-021, RWMCP-022, RWMCP-027 and RWMCP-028 remain the validated compatibility gaps. No additional `$defs` collision, package-content omission or response-envelope ambiguity was demonstrated.

### 10. Security and trust boundaries

Workspace/state paths use physical containment and owner-only state artifacts; plugins are explicitly trusted in-process rather than sandboxed; stdout remains a protocol integrity boundary; local diagnostic data is separated from external allow-listed reports; submission destinations are fixed HTTPS endpoints. RWMCP-030 is cooperative extension containment, RWMCP-031 is the final outbound privacy violation, RWMCP-032 is sensitive-memory retention and RWMCP-033 is consent revocation atomicity. No direct source-write bypass through supported plugin contracts, mutable report destination or automatic network submission was found.

### 11. Resource ownership and disposal

Loaded Workspaces, manifests, monitors, operation leases, cache scopes, MEF containers, file locks, state handles, Host processes, diagnostic sessions and DI-owned singleton disposables have identifiable owners and normal/failure cleanup paths. RWMCP-016 concerns metadata rather than handle ownership; RWMCP-023 is avoidable buffering; RWMCP-032 lacks expiry-driven release. No additional retained Workspace, process, stream, lock or analyser resource was substantiated.

### 12. Plausible repository-scale performance

Request result limits, cache budgets, bounded histories, metadata polling and scenario measurement are present. RWMCP-018, RWMCP-023 and RWMCP-026 retain plausible real-world memory or CPU impact. RWMCP-011 can extend lease occupancy during large solution evaluation. Other allocation or LINQ opportunities were not reported because no material call-path impact was demonstrated.

### 13. Missing or misleading integration, acceptance, audit and scenario coverage

The test topology correctly separates six Unit/Contract projects, four component-integration projects, Code Action audit, published-Host acceptance and external scenarios. Ordinary CI and retained evidence cover genuine component and process boundaries, but the gaps listed in the [final findings](final-findings.md) leave every active finding uncontradicted. The ScenarioRunner has no hermetic test project, and RWMCP-034 through RWMCP-038 show that compilation and suite deserialisation alone are insufficient release evidence. Acceptance, audit and external scenarios were not executed during this review under repository policy.

### 14. Duplicate, conflicting, unreachable or partially implemented behaviour

RWMCP-015 and RWMCP-024 remain rejected rather than duplicated. RWMCP-008 deliberately owns the common uncertified Solution/manifest pairing across open, reload and commit instead of splitting three findings. RWMCP-025 and RWMCP-029 remain separate because one is result correctness under capacity refusal and the other is wire metadata/retry behaviour. RWMCP-034 and RWMCP-038 remain separate because preparation can dirty a single run without concurrency, while concurrency can corrupt an otherwise side-effect-free preparation. No unreachable tool registration, conflicting implementation family or separate partially implemented path justified RWMCP-039.

## Final revalidation disposition

- Retained unchanged: RWMCP-009 through RWMCP-014, RWMCP-016 through RWMCP-023, RWMCP-025 through RWMCP-038.
- Retained with cross-project evidence reconfirmed: RWMCP-008 across open, reload and commit promotion; RWMCP-025/RWMCP-029 across Code Action retry and cache pressure; RWMCP-034/RWMCP-038 across scenario checkout certification.
- Rejected again: RWMCP-015 and RWMCP-024.
- Resolved older findings confirmed against current source: RWMCP-001 through RWMCP-007.
- New candidate identifiers allocated: none.

## Validation and limitations

The previously completed solution-wide fast gate passed 1,943 Unit/Contract cases. The final pass additionally ran 30 focused current-source Plugins.Core tests covering the resolved logical-line, async-boundary and disposable-control-flow findings; all passed. The ScenarioRunner still builds and its suite loads, but external repositories were not mutated. Acceptance, Code Action audit and external-repository scenarios were not run because repository policy reserves them for explicit or release-stage execution.

Windows source metadata behaviour, real external-plugin stdout contamination, a final processed Sentry envelope, real network failure/drain, third-party provider diversity and concurrent external checkout processes were not dynamically exercised. Their retained findings are supported by direct current-source call paths and, where noted in the subsystem reports, pinned dependency source or prior native evidence. Roslyn MCP tooling was unavailable in this session, so local source, project files and compiler/test evidence were used for navigation and validation.
