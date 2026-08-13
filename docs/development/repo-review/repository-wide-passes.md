# Repository-Wide Validation Passes

Date: 2026-08-13

**Review stage:** Repository-wide validation after completion of all eight subsystem units

**Status:** Complete

## Evidence boundary and method

These passes were performed as fresh current-state analyses of the checked-out repository. Evidence came from current tracked source, tests, project/package definitions, build configuration, CI workflows, checked-in fixtures and scenario-runner assets. Git history, diffs, changed-file discovery, commits, branches, tags, stashes, reflogs, deleted or renamed review artefacts, external backups, historical audits, validation reports and remediation records were not used as evidence.

Each pass began at an executable, MCP or package-consumer boundary and retraced the participating implementations rather than treating the subsystem reports as proof. The passes tested earlier candidate conclusions against downstream consumers, registrations, protocol projection, persistence, disposal and claimed test coverage. Candidate observations below remain working evidence; independent final finding validation has not begun.

The current solution built successfully with the pinned .NET 10 SDK using `dotnet build Roslyn.Workbench.Mcp.slnx --no-restore --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`: 0 warnings and 0 errors. This compilation result establishes current project/package-reference consistency only; it does not disprove runtime candidates or substitute for boundary-specific tests. No acceptance or external-repository scenario suite was run during these documentation-only passes.

The Roslyn MCP server required by repository guidance was not available in this session, so current-source navigation used repository file inventory and exact symbol/text searches. No production or test source was modified.

## Fresh representative cross-project traces

The following traces were repeated across multiple passes because they join the repository's principal boundaries:

1. **Host startup and publication:** `Program.Main` redirects standard output to standard error, creates the generic Host, and calls `AddRoslynWorkbench`. `HostStartupComposer` resolves and validates startup options before construction; DI then registers Workspace, plugin, Code Action, error-reporting and protocol services. Hosted startup first reports fallbacks, then registers MSBuild, initialises the state directory and performs recovery, then loads and publishes the immutable plugin catalogue. `RoslynWorkbenchMcpServerOptionsConfiguration` installs plugin list/call fallbacks while SDK-registered server-owned and Code Action tools remain direct `McpServerTool` instances.
2. **Workspace-backed plugin query:** MCP call filtering enters `PluginMcpRequestHandler`, which resolves the immutable runtime catalogue and invokes `PluginQueryMcpServerTool`. The execution-context factory selects a Workspace, acquires its shared query gate, checks lifecycle/transaction ownership and external input change, creates snapshot-bound resolvers and cache scopes, executes the plugin handler, checks for an unexpected Workspace change, serialises an object-valued success or structured failure, and releases the lease.
3. **Plugin or Code Action mutation and commit:** The adapter obtains the exclusive mutation lease, validates the expected snapshot, obtains a changed Roslyn `Solution`, and sends it through candidate processing, linked-document reconciliation, diff creation, revision allocation and session replacement. `transaction-commit` rechecks the session, begins input certification, takes the cross-process lock, plans filesystem operations, persists recovery artefacts and manifest phases, revalidates targets, applies with non-cancellable atomic operations, validates applied state, promotes the Workspace snapshot and then completes/deletes recovery evidence.
4. **Code Action list and replay:** A Host-published Code Action tool binds the request, acquires a Workspace context, resolves the current document/range, discovers and flattens provider actions, and stores a snapshot-bound replay recipe. Stage resolves the reference against the current Workspace snapshot, rediscovers the provider/action path, evaluates supported `ApplyChangesOperation` output and sends the changed solution through the common mutation stager. Fix All uses a prepared reference and a later rediscovery/evaluation path before staging.
5. **Unexpected failure and reporting:** The configured fallback plugin call route enters `UnhandledToolExceptionFilter`, which captures a bounded correlated record, projects an `UnhandledException` envelope and makes the record available to prepare/review/submit tools; direct SDK-registered tool routing was checked separately because it does not share that fallback route. Preparation creates an allow-listed immutable payload and handle; submission checks consent, atomically acquires the prepared handle, dispatches to logging or Sentry, and completes or releases the handle. Workspace close/reload invalidates Workspace-scoped grants through lifecycle observers.

## Pass 1 — Cross-project and package contract mismatches

The production dependency chain remains coherent: Abstractions defines selectors/results/services; Workspace implements snapshot, resolver and transaction contracts; Plugins exposes authoring and execution contracts while adapting Workspace services; Plugins.Core consumes the public plugin surface; CodeActions consumes Abstractions and Workspace internally; the Host composes and publishes all catalogues. The packable Plugins project includes the Abstractions assembly and the authoring analyser, while its Workspace reference is private so internal implementation does not become a public package dependency. The external package-consumer asset builds against the package surface rather than Host internals.

Freshly joining plugin admission to actual response projection reproduced `RWMCP2-006`: the default `ToolOutputSchemaMode.Omit` skips response metadata preflight, public/analyser contracts permit scalar response types such as `string`, but `ToolResultEnvelopeSerializer.CreateSuccess` requires successful data to serialise as a JSON object. This is a package-to-runtime contract mismatch, not merely a schema-publication preference.

Joining public Workspace coordinate contracts to Code Action listing reproduced `RWMCP2-001`: the range-bearing list request has no caller snapshot precondition, although subsequent replay is correctly bound to whichever current snapshot was used during listing. Joining Fix All preparation to execution reproduced `RWMCP2-011`: the prepared reference does not commit to the exact reviewed operation selection. These are separate contracts—initial coordinate interpretation versus later prepared-operation identity—and were not merged.

No additional public/package mismatch was substantiated. The architecture map's project and package boundaries remain accurate.

## Pass 2 — Dependency direction and abstraction ownership

Current project references preserve the intended inward direction. Abstractions has no project dependency; Workspace depends only on Abstractions; Plugins depends publicly on Abstractions and privately on Workspace; Plugins.Core depends on Abstractions, Plugins and Workspace; CodeActions depends on Abstractions and Workspace; the executable Host depends on every production project. The analyser is a netstandard authoring component and does not depend on Host runtime code. Test-support projects intentionally depend outward on production assemblies, and the acceptance/scenario process boundaries communicate through published artefacts and MCP rather than linking Host implementation.

Shared Workspace behavior is owned by Workspace services rather than duplicated in plugins or Code Actions: both adapters acquire common execution leases and both mutation families converge at `IMutationStagingService`. Server-owned lifecycle and transaction tools are Host adapters over Workspace services. Code Action-specific replay and provider composition remain in CodeActions, while third-party discovery/load contexts remain Host-owned.

No dependency inversion, circular production project reference or misplaced second transaction implementation was found. Existing candidate defects arise inside otherwise correct ownership boundaries, so no candidates were consolidated on architectural-ownership grounds.

## Pass 3 — Representative end-to-end behaviour across every involved project

Fresh traces covered startup, Workspace open/select/query, plugin query, plugin mutation, Code Action list/replay/Fix All, transaction preview/commit/recovery, unexpected failure capture and report submission. They reach MSBuild/Roslyn, filesystem watcher and atomic-file boundaries, plugin assemblies/load contexts, stdio MCP transport, cross-process locks, recovery persistence and logging/Sentry dispatch.

The query and mutation routes consistently acquire snapshot-aware Workspace contexts and release leases in `finally`/`await using` paths. Mutation routes converge on the same candidate processor and staging service. The successful commit path intentionally becomes non-cancellable only after durable application begins. Error results generally remain typed tool envelopes through adapters.

Consumer-level tracing independently reinforced `RWMCP2-004`, `RWMCP2-005`, `RWMCP2-014`, `RWMCP2-015` and `RWMCP2-017`: those failures remain observable after their downstream Host/protocol or external boundary is included. Final SDK pipeline inspection later rejected `RWMCP2-016`. Final product-contract validation also rejected `RWMCP2-018`: lifecycle invalidation controls later grant reuse and does not retroactively revoke an explicit submission request which already obtained consent for its immutable payload.

## Pass 4 — Dependency-injection registration and lifetime consistency

The Host registers stateful Workspace sessions, cache states, transaction/recovery services, plugin catalogue state, Code Action reference state, error stores/consent and protocol services as singletons, matching their process-wide state and immutable-catalogue model. Multiple `IWorkspaceSnapshotLifecycleObserver` registrations are materialised by `WorkspaceSessionStore` and synchronously invalidate plugin cache, Code Action references and consent at snapshot transitions. Plugin-owned service providers are retained for catalogue lifetime and disposed in reverse order. Hosted prerequisites run before tool availability through generic Host lifecycle startup.

The lifetime pass reproduced `RWMCP2-002`: the singleton `WorkspaceSessionStore` owns session snapshots whose loaded Workspace and input manifest require disposal, but the store itself is not disposable and no hosted shutdown service drains open sessions. It also reproduced `RWMCP2-003`: explicit close removes the authoritative session before awaiting advisory instance-status closure, and a thrown close bypasses disposal of the loaded Workspace and manifest.

Cache states and bounded error stores are DI-owned disposables, cancellation is tied to Host stopping, and plugin catalogue state implements both synchronous and asynchronous disposal. No additional missing registration, alias mismatch or captive scoped dependency was substantiated.

## Pass 5 — Configuration declaration, precedence, validation and consumption

`StartupOptionsResolver` establishes defaults, reads command-line values and environment fallbacks, records fallback warnings and constructs one immutable startup snapshot. Command-line precedence is applied for ordinary scalar settings; plugin directories deliberately combine repeated command-line and environment entries with path-aware de-duplication; error-reporting consent explicitly warns and ignores the environment variable in favour of command line/default policy. `HostStartupComposer` validates before Host construction, and options validation is repeated on Host start.

Every declared setting was traced to consumption: result defaults and query concurrency/revision history enter `WorkspaceOptions`; Workspace and plugin cache limits/expiry enter their cache options; Code Action reference lifetime/size enter reference options; state directory enters persistence; plugin directories and output-schema mode enter catalogue/schema publication; error-retention, preparation and dispatch policy enter error-reporting services. Invalid bounded values fail validation, while parser fallbacks are exposed through stderr logging.

The configuration pass strengthens `RWMCP2-006` because output-schema omission controls more than publication in practice: it suppresses response preflight even though runtime success projection retains the object-only invariant. No other accepted-but-unused or declared-but-unvalidated option was found.

## Pass 6 — Error, cancellation and retry propagation

Workspace and tool services generally throw cancellation before state change, propagate caller cancellation through Roslyn/query work, and use typed rejected/conflict/faulted outcomes for expected operational failures. Commit honours cancellation through validation, planning, recovery persistence and pre-application revalidation, then deliberately switches application/restoration to `CancellationToken.None` once partial persistence would be unsafe. Error submission releases its in-progress handle after dispatcher failure, cancellation or exception, allowing a deliberate retry.

The top-level protocol route has two distinct inconsistencies. `RWMCP2-014` remains because every `OperationCanceledException` is rethrown even when the active request token was not cancelled, bypassing Workbench capture before the outer SDK dispatcher returns its generic error result. `RWMCP2-015` remains because deliberate `McpProtocolException` instances thrown by the plugin fallback for unknown/task-augmented calls are caught as unexpected failures and remapped to correlated tool envelopes. Final inspection of `ModelContextProtocol.Core` 1.4.1 rejected `RWMCP2-016`: direct/fallback selection occurs inside the base handler and the registered filters wrap that combined handler.

No infinite automatic retry or retry that repeats a non-idempotent committed filesystem effect was found. Expected transaction and reporting retry guidance is explicit in returned actions or handle state.

## Pass 7 — Concurrency, shared state, cache coalescing and thread safety

Workspace host snapshots are replaced under a single lock; readers receive immutable snapshot records. Per-Workspace operation gates separate bounded shared queries from exclusive mutation/lifecycle operations. Transaction ownership is process-global and checked during acquisition. Query-cache partition generations, invalidation tokens, in-flight computation dictionaries and waiter counts are lock-protected; a lifecycle invalidation prevents late results from being admitted, and Host stopping cancels shared factories. Code Action references and bounded error/preparation stores use their own synchronisation and capacity/expiry policies. Plugin catalogue publication is one-time atomic and invocation reads an immutable frozen dictionary.

`SubmitErrorReportTool` reads consent before acquiring the prepared submission, while Workspace close or session suppression can update the consent store between those operations. Final validation established that the sequence is consistent with the current contract because invalidation applies to later decisions rather than revoking an already-authorised explicit request; `RWMCP2-018` is rejected. Error capture still attempts Workspace attribution from raw request arguments rather than the already resolved execution context, preserving `RWMCP2-017` under concurrent multi-Workspace use.

No additional cache coalescing race, cross-plugin cache-key collision, reference-store resurrection or session-snapshot torn read was substantiated. Potentially related lifecycle invalidations are already represented by their concrete consumer failures rather than a generic concurrency candidate.

## Pass 8 — Transaction, persistence and cross-process consistency

The mutation path is transaction-only and session promotion is separate from source-file commit. Commit uses a Workspace-root lock shared across processes, writes owner/status/recovery manifests and artefacts under the configured state directory, revalidates target existence/hash/mode immediately before each operation, applies atomically, validates the applied state twice around input-manifest promotion, and retains non-terminal recovery evidence on conflicts/incomplete restoration. Startup recovery takes the same Workspace lock before restoration or cleanup.

Two persistence gaps survive those controls. `RWMCP2-004` remains because the only comparison with the loaded input manifest occurs before lock acquisition/planning; if external bytes arrive before planner capture, those bytes become the plan's `OriginalHash` and backup and are later overwritten as though they were the transaction baseline. `RWMCP2-005` remains because recovery validates target state but does not hash staged, backup or delete-marker artefact contents against manifest hashes before applying/restoring them; corrupted artefacts can therefore become authoritative source bytes.

These candidates are not duplicates: one loses a legitimate concurrent external edit during a live commit, while the other trusts corrupted durable evidence during application/recovery. Cross-process lock acquisition and physical path containment do not prevent either scenario.

## Pass 9 — Serialisation, schema, binary and package compatibility

The request binder uses web-default JSON semantics, recursive object-graph validation, nullability/required metadata and typed request construction. Protocol schemas are produced from the same request/response types and optional output-schema mode. Result projection consistently emits object envelopes with structured success, failure, diagnostics, warnings and continuation guidance. The public plugin package carries its public dependency assembly and authoring analyser; plugin metadata inspection rejects incompatible API identities before load; shared dependency policy prevents private copies of server-shared assemblies from silently redefining contracts.

`RWMCP2-006` is the concrete schema/runtime disagreement found in this pass. `RWMCP2-017` is also reinforced by wire compatibility: ordinary binding accepts case-insensitive property names and all public selector forms, while raw error-attribution parsing recognises only an exact lower-camel Workspace ID shape. Code Action references remain process-local opaque identifiers rather than claimed durable/binary contracts.

No new malformed-envelope, request-schema nullability, package-content omission or binary load-context conflict was substantiated in current source and package definitions.

## Pass 10 — Security and trust boundaries

The server explicitly treats loaded workspaces, MSBuild project logic, analyzers and plugins as fully trusted; it does not claim sandboxing. Within that model, filesystem mutation and recovery paths are canonicalised and physically checked for strict containment, plugin discovery rejects escaped package paths and incompatible shared dependencies, stdout is reserved for MCP while diagnostics use stderr, and external error reports use allow-listed projection and bounded immutable preview bytes. Sentry configuration fixes destination and event fields rather than forwarding arbitrary captured request/source content.

No path traversal, symlink/reparse escape, arbitrary report destination, direct source-content disclosure or consent-admission defect was substantiated. Workspace attribution can still be lost as described by `RWMCP2-017`, affecting which scoped grant/status is offered. The apparent consent race was rejected because the contract does not retroactively revoke an explicit request after it has obtained valid consent.

The scenario runner intentionally invokes Git and project preparation in external repositories as an operator-authorised release tool. Its poisoned-cache defect is an operational integrity issue (`RWMCP2-019`), not a production sandbox escape. `RWMCP2-020` was rejected because a failed command is not accepted as release evidence and is rerun in full.

## Pass 11 — Resource ownership and disposal

Normal Workspace close disposes input watchers/manifests and Roslyn/MSBuild workspaces; execution leases release gates; input certifications are scoped disposables; commit locks and status handles own file streams; plugin service providers are disposed in reverse order; cache invalidation sources and memory caches are disposed; bounded-store timers support sync/async disposal; Sentry dispatch uses a DI-owned client; scenario Host/process helpers attempt termination and stream/diagnostic cleanup.

The pass independently reproduces `RWMCP2-002` and `RWMCP2-003` at the generic Host and close-failure boundaries. It does not merge them: generic EOF/Host shutdown has no session-drain owner, whereas explicit close has an owner but its cleanup ordering skips resources after an advisory status-stream failure. Scenario-runner results are aggregate command outputs rather than independently owned durable iteration records; final validation therefore rejected `RWMCP2-020`.

No additional undisposed plugin load-context provider, query lease, cross-process lock or cache timer was substantiated.

## Pass 12 — Performance problems with plausible repository-scale impact

Repository-scale work is generally bounded at projection and instrumented with EventSource phases. Workspace and plugin caches have configured limits and invalidation. Query gates cap concurrent Roslyn work. Many tool contracts expose per-collection limits and truncation metadata, and dependency graph construction accepts node/edge bounds.

`RWMCP2-009` remains the clearest compute/stack risk: dependency-cycle detection builds the full graph, recursively executes Tarjan traversal without cancellation checks inside traversal, materialises and sorts all cycles, and applies `cyclesLimit` only afterward. A sufficiently deep or large repository can monopolise a query slot, ignore cancellation during traversal or overflow the stack. Current [.NET documentation](https://learn.microsoft.com/dotnet/api/system.stackoverflowexception?view=net-10.0) confirms that user code cannot catch stack exhaustion and the process terminates by default, retaining P1/High. `RWMCP2-010` remains distinct: several outer result limits cap only top-level items while nested locations/members/occurrences/reasons remain potentially large, allowing response allocation and serialisation to scale beyond the advertised bound.

The pass found no additional repository-scale defect with a concrete failure scenario beyond these two candidates. Scenario performance tooling has broad calibration and EventPipe support; partial-run durability is outside its current completed-command evidence contract.

## Pass 13 — Missing or misleading integration, acceptance, audit and scenario coverage

The current topology has unit/contract projects, real-component Workspace and Code Action integration projects, Host integration, published-process acceptance on Linux/Windows, a separately scheduled/path-triggered built-in Code Action audit, external plugin fixtures, a packed-package consumer, cross-process lock fixture and an independent external-repository scenario runner. CI uses TRX artefacts, hang diagnostics and minimum discovered-count gates. The solution build includes all current projects and fixtures.

Coverage is nevertheless misleading at specific claimed boundaries. `RWMCP2-012` remains because the current full Code Action audit has one reproducible implement-interface case failure (119/120 in the prior unit's fresh execution), so its gate is not currently green. `RWMCP2-013` remains because the audit's “replay” checks evaluate operations on the originally discovered Roslyn action object rather than taking the Workbench action ID through reference lookup, rediscovery and staging. The passing audit cases therefore prove provider offer/operation compatibility, not production replay compatibility.

Boundary gaps preserve the confidence of `RWMCP2-001` through `RWMCP2-006`, `RWMCP2-014`, `RWMCP2-015` and `RWMCP2-017`: existing tests do not join the exact stale range, planning drift, corrupted artefact, default-mode scalar response, cancellation classification, protocol-error remapping or multi-Workspace attribution. Direct-tool filter coverage is absent, but SDK implementation inspection prevents that gap from substantiating `RWMCP2-016`. Scenario-runner failure injection remains absent for incomplete first preparation, preserving `RWMCP2-019`; partial evidence persistence is not a claimed contract and does not retain `RWMCP2-020`. These are test-confidence observations attached to concrete candidates, not standalone “missing tests” findings.

## Pass 14 — Duplicate, conflicting, unreachable or partially implemented behaviour

All twenty candidates were compared by trigger, failing boundary, effect and remediation direction. No exact duplicate was found. The following overlap clusters were retained separately:

- `RWMCP2-001` and `RWMCP2-011`: stale caller coordinates during listing versus prepared Fix All operation identity during replay.
- `RWMCP2-002` and `RWMCP2-003`: missing generic shutdown ownership versus exceptional ordering in explicit close.
- `RWMCP2-004` and `RWMCP2-005`: live commit baseline drift versus integrity of durable recovery artefacts.
- `RWMCP2-006` and `RWMCP2-015`: incompatible successful plugin response admission and deliberate fallback protocol-error remapping. The former `RWMCP2-016` overlap was removed after SDK inspection established that direct tools share the filter path.
- `RWMCP2-009` and `RWMCP2-010`: unbounded computation/recursion versus unbounded nested response payloads.
- `RWMCP2-012` and `RWMCP2-013`: a currently failing provider fixture versus a harness that bypasses the replay path even for passing cases.
- `RWMCP2-017` and rejected `RWMCP2-018`: loss of Workspace attribution remains a defect, while the apparent revocation race is consistent with the explicit-submission contract.
- `RWMCP2-019` and rejected `RWMCP2-020`: poisoned reusable repository state prevents subsequent runs, while a failed command's partial results are intentionally not authoritative evidence.

Partially implemented behavior is already represented by those concrete candidates. The project-details contract exposes preprocessor symbols but current projection always omits them (`RWMCP2-007`); flow analysis reports a normalized region different from the region passed to Roslyn (`RWMCP2-008`); and the audit labels replay compatibility while bypassing reference replay (`RWMCP2-013`). No validated candidate became unreachable after tracing its Host consumer. The later independent SDK check rejected `RWMCP2-016`; no candidates required merging.

## Candidate reconciliation outcome

At the end of the repository-wide-pass stage, candidates `RWMCP2-001` through `RWMCP2-020` remained provisional and the next identifier remained `RWMCP2-021`. Subsequent independent validation retained seventeen candidates, reduced `RWMCP2-011` confidence to Medium, rejected `RWMCP2-016` after current SDK implementation inspection established prevention, and rejected `RWMCP2-018` and `RWMCP2-020` after their current product contracts were applied. The durable ledger and `final-findings.md` contain the superseding dispositions.

The architecture map was revisited against current project references, entry points, registrations, package contents and external boundaries. Final SDK inspection corrected the exception-filter route in the architecture map and Units 6–8; no other project, dependency, entry-point, composition-root or external-boundary correction was required.

## Stage validation and stopping point

All fourteen repository-wide passes required by Stage 3 of `DeepDiveReview.md` are complete. Independent final validation is also complete, with the final state recorded in the review plan, durable ledger and `final-findings.md`. No production code was changed.
