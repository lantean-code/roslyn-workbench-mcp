# Published Host Acceptance Coverage Audit

Date: 2026-07-23

## Purpose

This audit defines the acceptance contract for a Roslyn Workbench release. It starts from the supported user and agent behaviour in the release documentation, not from the tests or scenario runner that happen to exist today.

Acceptance proves that the distributable Host, public MCP protocol, Workspace lifecycle, tool execution families, plugin boundary and transaction guarantees work together. It does not replace unit branch coverage, schema contract tests, component integration tests, Code Action compatibility audits or release performance measurements.

The audit answers seven questions for every release capability:

1. What user-visible promise is being made?
2. What evidence already exists, and at which test layer?
3. Which part genuinely needs the published executable and stdio protocol?
4. What observable result makes the capability accepted?
5. Which operating systems must exercise it?
6. Does it gate pull requests, scheduled platform support or release validation?
7. What remains unproven?

## Authoritative release surface

The release contract comes from:

- [Getting started](../GettingStarted.md);
- [Configuration](../Configuration.md);
- [Tool discovery and results](../ToolDiscovery.md);
- [Workspaces and transactions](../WorkspacesAndTransactions.md); and
- [Third-party plugin authoring](../PluginAuthoring.md).

Documents under `docs/development` provide implementation evidence but do not extend the release contract.

The current supported distribution boundary is the Release-published `Roslyn.Workbench.Mcp` executable described by `GettingStarted.md`. The project does not currently configure a .NET tool package or another installable release format. Acceptance should consume the exact publish output produced by CI. If release packaging later wraps or replaces that output, acceptance must move outward to the new user-facing artifact.

MCP `tools/list` remains authoritative for the enabled tool inventory. Acceptance should therefore validate the published catalogue and representative execution architectures, not copy an aspirational development catalogue into fixed test expectations.

## Test-layer and execution policy

### Evidence ownership

| Layer | Owns | Does not prove |
|---|---|---|
| Unit | Branch behaviour, validation, result invariants and isolated failure mapping | Packaging, MSBuild, filesystem or MCP transport |
| Contract | JSON, schemas, defaults, annotations, public plugin API and exported surface | End-to-end execution |
| Component integration | Real Roslyn, MSBuild, filesystem, plugin composition, transaction services and native locks | Published executable composition and stdio mapping |
| Code Action audit | Supported provider catalogue, classification and replay compatibility | General Host lifecycle or release packaging |
| Published-Host acceptance | Release artifact, startup, stdio, public MCP workflow, composition and cleanup | Repository-scale performance or exhaustive handler branches |
| Scenario runner | Release-scale correctness, destructive recovery, platform behaviour and performance trends | Fast deterministic pull-request regression coverage |

An acceptance case is warranted when a defect could exist despite the owning unit, contract and component tests passing because the failure crosses packaging, process, transport, public mapping or production-composition boundaries.

### Execution tiers

| Tier | Trigger | Platforms | Gate |
|---|---|---|---|
| Fast development | Developer choice and fast CI job | Developer environment and Ubuntu CI | Unit and Contract only |
| Pull-request acceptance | Every pull request | Native Ubuntu and Windows | Correctness, cleanup and process termination |
| Best-effort platform validation | Public v1 release-candidate preparation, release branches and explicit manual dispatch | macOS | Advisory acceptance, Workspace and curated scenario evidence |
| Release scenario validation | Release branches and explicit manual dispatch | Native Windows and Linux; curated macOS evidence; WSL evidence where the path environment is relevant | Windows and Linux correctness and cleanup gate the release; macOS and performance evidence are advisory |

The current `tests.yml` workflow already runs the published-Host acceptance project on every pull request on Ubuntu and Windows. The coverage expansion therefore changes tests, fixtures and minimum counts rather than adding another ordinary pull-request workflow.

The external-repository scenario runner must not run on ordinary pull requests or pushes. The repository does not yet define a release-branch naming convention; its workflow trigger cannot be finalised until that convention is selected.

## Current acceptance baseline

Before Batches 2 and 3, the acceptance project contained twelve tests:

- eight launch the published Roslyn Workbench Host;
- one launches a deliberately broken `dotnet` command to validate startup diagnostics; and
- three validate Host-path configuration without starting a process.

Current published-Host evidence covers:

- catalogue and selected schema-default publication;
- full server status and invalid-option fallback;
- one direct SDK project open, semantic query, status, close and empty-state rejection;
- one bundled rename transaction through durable commit;
- one token-based Code Action through staging, preview and rollback;
- one valid external query plugin with a private dependency;
- deterministic external query and mutation packages plus known-request-ID invocation readiness;
- blocked-recovery reporting from a synthetic `RecoveryConflict` manifest; and
- stdin end-of-stream shutdown.

This is a useful smoke suite, but it does not yet represent the complete release capability set.

## Release-capability matrix

The `Gap` column below records the baseline that produced the implementation batches. The dated implementation status and evidence under each batch are authoritative for completed work; the next manual/CI acceptance execution supplies runtime evidence.

### Distribution, process and configuration

| Release capability | Existing evidence | Required published acceptance | Tier/platform | Gap |
|---|---|---|---|---|
| Release-published executable starts as a local stdio MCP server | CI publishes then launches the Host; protocol acceptance initialises it | Continue to run the exact Release publish output and verify initialisation, process identity and zero protocol output on stderr | PR: Ubuntu, Windows | Covered for the current distribution format |
| Version and component status are observable | Protocol acceptance checks non-empty server and Roslyn versions | Assert product version is the version embedded in the tested release artifact and MSBuild availability is actionable | PR: Ubuntu, Windows | Product-version identity is not locked to the artifact being tested |
| Protocol uses stdout and operational logging uses stderr | Successful MCP traffic and stderr capture exist separately | Retain successful protocol traffic plus failed-startup stderr diagnostics; ensure logs never corrupt stdout | PR: Ubuntu, Windows | Covered representatively |
| Stdin EOF stops the process cleanly | Dedicated lifetime acceptance | Retain exit-code and no-forced-termination evidence | PR: Ubuntu, Windows | Covered |
| Command-line values override environment values; repeated scalars use the last value | Unit and Host integration configuration coverage | Start the published Host with conflicting environment and command-line values, then verify the non-sensitive effective projection | PR: Ubuntu, Windows | No published-boundary precedence case |
| Invalid configuration falls back with `StartupConfigurationFallback` | Published acceptance covers one invalid scalar | Retain one representative invalid scalar and assert warning, effective default and continued operation | PR: Ubuntu, Windows | Covered representatively |
| Repeatable plugin roots combine and deduplicate | Unit and integration coverage | Exercise only through the plugin package matrix; do not duplicate every parser branch | PR: Ubuntu, Windows | Covered when multi-package acceptance is added |
| Sensitive paths are omitted from public status | Unit/contract coverage | Assert state and plugin directory paths are absent from full status while effective non-sensitive values remain visible | PR: Ubuntu, Windows | No explicit public-boundary privacy assertion |
| Default and explicit `Full` output-schema modes work | Contract/integration schema coverage; default acceptance samples input schemas | Start one Host in each mode; require output schemas absent by default and real family-specific schemas in `Full` mode | PR: Ubuntu, Windows | Full-mode publication is not accepted end to end |

### Tool discovery and public result contracts

| Release capability | Existing evidence | Required published acceptance | Tier/platform | Gap |
|---|---|---|---|---|
| Catalogue is fixed for process lifetime and independent of Workspace state | Unit/integration composition; initial acceptance list | Compare catalogue identity before open, during transaction and after close | PR: Ubuntu, Windows | Stability across state transitions is not asserted |
| Names, titles, descriptions, annotations and input schemas are published | Contract tests; acceptance samples three names and two defaults | Validate representative server, query, bundled mutation, Code Action and external plugin metadata; keep exhaustive schema ownership in Contract tests | PR: Ubuntu, Windows | Execution-family metadata sampling is incomplete |
| Curated defaults are published and applied | Contract/integration audit covers all built-in limits; acceptance samples two | Invoke one bounded tool with omitted and explicit limits, proving the published default matches execution | PR: Ubuntu, Windows | Runtime use of a discovered default is not accepted |
| Bounded collections are deterministic and report `hasMore` | Tool tests and release runner | Repeat one bounded query, compare ordered response, then raise the limit and verify prefix equivalence | PR: Ubuntu, Windows | No small published-boundary determinism case |
| Zero limits return no items without changing semantics | Unit/tool coverage and EF release scenarios | One representative published query is sufficient | PR: Ubuntu, Windows | No published zero-bound case |
| Success, rejection, conflict and failure envelopes retain the common shape | Host adapter unit tests; acceptance samples success and `WorkspaceNotOpen` | Sample all four result classes through real public workflows and assert error code, message, optional next action and absence of internal exception detail | PR: Ubuntu, Windows | Conflict and isolated failure envelopes are missing |
| Malformed MCP arguments remain protocol-level failures | Host/MCP binding tests | Send one malformed request through the official client or raw request API and require protocol failure rather than a fabricated tool result | PR: Ubuntu, Windows | No published malformed-request case |

### Plugin ecosystem

| Release capability | Existing evidence | Required published acceptance | Tier/platform | Gap |
|---|---|---|---|---|
| Bundled plugin is composed through production startup | Protocol and status acceptance | Retain catalogue, status and real bundled query/mutation execution | PR: Ubuntu, Windows | Bundled query and mutation are covered across existing workflows |
| Valid external package loads with a private dependency | Existing external-plugin acceptance | Retain enabled status and invocation | PR: Ubuntu, Windows | Covered |
| External mutation returns a candidate and Host stages it through the transaction pipeline | Component and adapter tests; external mutation fixture exists | Package and invoke the external mutation, preview it, roll back, and prove disk remains unchanged | PR: Ubuntu, Windows | No external mutation through the published package boundary |
| Invalid plugin metadata or contracts disable only that package | Discovery, validation and integration tests | Load one valid and one invalid package together; require actionable diagnostics and continued bundled/valid-plugin availability | PR: Ubuntu, Windows | No failure-isolation acceptance |
| Duplicate IDs and tool collisions are deterministic | Integration coverage | One multi-package published case should sample duplicate ID or reserved-name collision; exhaustive combinations remain integration-owned | PR: Ubuntu, Windows | No public diagnostic/collision sample |
| Configure, materialisation or execution exceptions are isolated and sanitised | Unit/integration and throwing fixture coverage | Require disabled or failed plugin result without exception detail, then successfully invoke an unaffected tool | PR: Ubuntu, Windows | No process-boundary exception-isolation case |
| Plugin set changes only after restart | Startup architecture and docs | Add a package after initialisation, prove catalogue unchanged, restart and prove discovery | PR: Ubuntu, Windows | No lifecycle acceptance |
| Code Actions are not reported as plugins | Existing Code Action acceptance | Retain | PR: Ubuntu, Windows | Covered |
| Plugins are trusted in-process rather than sandboxed | Documentation and future authoring analyser | No adversarial acceptance test; validate only supported contracts and Host containment of ordinary failures | Not an acceptance target | Explicitly excluded |

### Workspace loading, selection and lifecycle

| Release capability | Existing evidence | Required published acceptance | Tier/platform | Gap |
|---|---|---|---|---|
| Server starts with no Workspace | Existing protocol and lifecycle acceptance | Retain no-workspace status/rejection | PR: Ubuntu, Windows | Covered |
| Absolute `.csproj` opens and remains queryable | Existing SDK-project acceptance | Retain | PR: Ubuntu, Windows | Covered |
| Absolute `.sln` opens | Component integration and release runner | Add a small checked-in solution acceptance | PR: Ubuntu, Windows | Missing |
| Absolute `.slnx` opens | Component integration assets exist | Add a small checked-in hierarchy acceptance | PR: Ubuntu, Windows | Missing |
| Mixed solutions ignore unsupported projects and retain supported C# projects | Workspace integration uses a mixed solution | Open the copied mixed solution, assert diagnostics/counts and query a supported project | PR: Ubuntu, Windows | Missing at published boundary |
| Workspaces with no supported project or malformed input reject actionably | Component integration | Sample one no-supported or malformed load through MCP | PR: Ubuntu, Windows | Missing |
| Load diagnostics are returned without preventing usable supported projects | Component integration and release EF scenario | Assert warning shape and successful query in the mixed fixture | PR: Ubuntu, Windows | Missing in small acceptance |
| Project, alias and canonical path selectors identify a loaded Workspace | Unit/component selection tests; acceptance uses ID and alias on open | Select the same Workspace through all supported selector forms | PR: Ubuntu, Windows | Alias/path selection not accepted |
| Omitting selector is allowed for one Workspace and rejected for many | Unit/component tests | Query without selector with one Workspace, then open a second and require `WorkspaceSelectorRequired` | PR: Ubuntu, Windows | Missing |
| Multiple Workspaces remain isolated and only one owns the transaction slot | Component integration and release concurrency scenario | Query two copied Workspaces, transfer transaction ownership after rollback and verify `workspace-list` owner | PR: Ubuntu, Windows | Missing |
| Duplicate open, maximum loaded count and close-with-transaction return structured state errors | Unit/component tests | Sample duplicate open and close-with-transaction; maximum-count branch can remain component-owned unless packaging changes it | PR: Ubuntu, Windows | Two public state mappings missing |
| External source/configuration changes make results stale | Component integration and release state sequence | Warm query, edit source externally, require `WorkspaceOutOfDate` and `ReloadWorkspace` | PR: Ubuntu, Windows | Missing |
| Reload advances the epoch and returns refreshed semantics | Component integration and release state sequence | Reload the stale fixture and prove the semantic result changes | PR: Ubuntu, Windows | Missing |
| Advisory cross-instance state is reported without blocking queries | Component integration | Start two published Hosts on the same copied root, inspect advisory state and run a query | Release correctness: Windows, Linux | Too process-heavy for initial PR expansion; no published two-Host evidence |
| WSL access to `/mnt/<drive>` is warned about | Release runner/manual evidence | Validate from WSL against a Windows-mounted path | Release/manual WSL | Hosted PR runners do not represent WSL |
| Close disposes the selected Workspace and leaves others available | Existing single close plus component multi-Workspace tests | Close one of two and query the remaining Workspace | PR: Ubuntu, Windows | Missing multi-Workspace close case |

### Query execution families and selectors

Acceptance does not need one test per query tool. It must sample every distinct public execution and resolution boundary while unit tests retain exhaustive tool branches.

| Execution or selector family | Representative acceptance | Existing evidence | Gap |
|---|---|---|---|
| Server-owned lifecycle query | `server-status` and `workspace-status` | Existing | Covered |
| Bundled plugin query | `search-symbols` plus one bounded structural query | Existing search only | Add bounded/default evidence |
| External plugin query | Packaged query with private dependency | Existing | Covered |
| Code Action query | `list-code-actions` with executable and filtered actions | Existing executable action | Add diagnostic/fix filtering only if needed by mutation representative |
| Solution scope | Structure or symbol query over a small solution | Component/runner | Missing |
| Project scope and target framework | Project-qualified query in a multi-target fixture | Unit/component and runner selectors | Missing |
| Document scope | Document outline/options or formatting target | Tool tests | Missing at published boundary |
| Span location and copied selection | Resolve or context query, including stale snapshot | Unit/component | Missing |
| Documentation ID symbol | Existing rename and search workflow | Existing mutation uses documentation ID | Covered as a successful resolver |
| Project-qualified symbol | Query linked or multi-target source | Component/runner | Missing |
| Ambiguous selector | Multi-target document or project ambiguity returns structured rejection | Component | Missing |
| Snapshot mismatch | Reuse an old span or mutation precondition after a revision | Unit/runner | Missing |
| Query against staged solution | Query sees revision change before commit and baseline after rollback | Component/runner | Missing |
| Cache invalidation | Warm, externally reload or commit, then receive refreshed response | Unit/runner | Missing |
| Deep/recursive bounded graph | Release runner only; Contract tests own defaults | Runner | No PR acceptance needed unless mapping differs |

### Mutation systems and transaction workflow

The Host has distinct bundled/external plugin and internal Code Action paths. Each production execution architecture needs a published representative.

| Release capability | Existing evidence | Required published acceptance | Tier/platform | Gap |
|---|---|---|---|---|
| Bundled mutation stages but does not write before commit | Existing rename commit; Code Action rollback checks disk | Explicitly check disk before bundled preview/commit | PR: Ubuntu, Windows | Pre-commit disk invariant is not explicit for bundled mutation |
| External plugin mutation proposes and Host stages | Adapter/component tests | Package, invoke, preview and roll back external mutation | PR: Ubuntu, Windows | Missing |
| Token-based Code Action mutation stages and rolls back | Existing raw-string action | Retain | PR: Ubuntu, Windows | Covered |
| Dedicated or replay Code Action mutation executes | Code Action integration/audit and release runner | Use one deterministic dedicated/replay tool through published Host | PR: Ubuntu, Windows | Missing |
| No-change result does not create a revision | Tool unit tests and runner scenario | Invoke one deterministic no-change mutation and inspect transaction revision | PR: Ubuntu, Windows | Missing |
| Rejected mutation retains transaction and disk state | Unit/component tests | Use a stale snapshot or invalid target, then prove transaction remains usable | PR: Ubuntu, Windows | Missing |
| Create and replace operations commit and promote | Release Code Action scenario; component durability | Commit a small Code Action that creates one file and replaces another | PR: Ubuntu, Windows | Missing |
| Delete operation commits and recovers | Synthetic component coverage only; no genuine built-in action | Add only when a deterministic supported action removes a document | Conditional release scenario | No real user workflow exists |
| Multiple revisions expose preview, undo and redo | Component integration and release state sequence | Stage two compatible operations, traverse history and query selected revision | PR: Ubuntu, Windows | Missing |
| Revision capacity returns actionable rejection | Unit/component tests | Keep component-owned unless configuration/protocol mapping changes | Component sufficient | No acceptance case required now |
| Rollback discards staged state without disk changes | Existing Code Action rollback | Retain and extend to ownership transfer | PR: Ubuntu, Windows | Covered for one mutation family |
| Commit is the only public source-write boundary | Architecture, unit and component evidence | Pre/post disk assertions around representative plugin and Code Action commits | PR: Ubuntu, Windows | Partially covered |
| Post-commit queries use promoted state | Existing rename commit | Retain and add created-document case | PR: Ubuntu, Windows | Covered for replacement only |

### Durability, conflicts and recovery

| Release capability | Existing evidence | Required acceptance or release evidence | Tier/platform | Gap |
|---|---|---|---|---|
| Single and multi-file replacement is atomic from the public workflow | Existing one-file acceptance; component multi-file; runner large rename | Add small multi-file or linked-document commit and validate exact disk set | PR: Ubuntu, Windows | Missing small multi-file published regression |
| Duplicate physical targets are merged or rejected before writing | Unit/component coverage after runner-discovered Windows defect | Commit a linked/multi-target rename and prove one physical write per target | PR: Ubuntu, Windows | Critical regression not accepted |
| Encoding and line endings are preserved | Component integration | Add only if the acceptance fixture crosses Host projection differently; otherwise component evidence is sufficient | Component sufficient | No acceptance case required now |
| Pre-write external drift rejects before durable application | Component and release runner | Stage, externally edit, commit, require `TransactionConflicted`, then roll back without overwriting external bytes | PR: Ubuntu, Windows | Missing |
| Application-phase conflict restores server-written files and preserves external divergence | Component and EF release scenario | Retain in release runner because deterministic orchestration is destructive and repository-scale | Release: Windows, Linux | Covered at release tier |
| Cancellation before application leaves staged transaction and no disk changes | Unit/component and release runner | Retain durable boundary case in release runner | Release: Windows, Linux | Covered at release tier |
| Cancellation after application begins does not interrupt durability | Unit/component and release runner | Retain in release runner | Release: Windows, Linux | Covered at release tier |
| Abrupt Host termination is recovered by a fresh Host | Component synthetic recovery and release runner real termination | Retain real create/replace interruption in release runner | Release: Windows, Linux | Covered at release tier |
| Blocked recovery is visible and prevents unsafe open | Existing synthetic acceptance | Retain and additionally assert unsafe Workspace open rejection | PR: Ubuntu, Windows | Status covered; open rejection missing |
| Completed commit leaves no recovery state and survives restart | Existing commit and separate restart tests | Restart after a small committed mutation, inspect status and query promoted state | PR: Ubuntu, Windows | Missing |
| Inter-process lock serialises the final commit and releases after crash | Native Workspace integration with external fixture | Keep native integration as PR evidence; add published two-Host case only if release evidence differs | Windows PR component; Linux component | No demonstrated published gap |
| State, journals, temporary files and processes are cleaned | Acceptance fixture cleanup, component checks and runner validation | Every acceptance workflow asserts terminal state where relevant; CI retains failure roots | PR and release | Present but not systematically required by all cases |

### Cancellation, concurrency and isolation

| Release capability | Existing evidence | Required published acceptance | Tier/platform | Gap |
|---|---|---|---|---|
| MCP cancellation reaches the server handler token | Host integration through MCP SDK; release runner query cancellation | Use a deterministic cancellable external query fixture and known request ID | PR: Ubuntu, Windows | No published executable case |
| Cancelled query releases its shared lease | Release runner | After cancellation, start and roll back a transaction | PR: Ubuntu, Windows | Missing |
| Queries run concurrently up to configured bound | Operation-gate units and release runner | Use a deterministic blocking external query with `max-concurrent-queries=1`; second request must return `WorkspaceBusy` and `Retry` | PR: Ubuntu, Windows | Missing deterministic small case |
| Retry succeeds after lease release | Release runner | Release/cancel first query, retry second, require stable success | PR: Ubuntu, Windows | Missing |
| Exclusive lifecycle or mutation operations reject rather than queue | Unit/component | Hold deterministic query, attempt transaction start, require `WorkspaceBusy` and `Retry` | PR: Ubuntu, Windows | Missing public mapping |
| Different Workspaces have independent query gates but one transaction owner | Component and release concurrency scenario | Query two Workspaces while transferring transaction ownership | PR: Ubuntu, Windows | Missing |
| Independent Host processes use isolated default state roots | Component lifecycle test | Published two-Host evidence is unnecessary unless default-root composition changes | Component sufficient | No acceptance case required now |

### Failure safety, paths and operational diagnostics

| Release capability | Existing evidence | Required published acceptance | Tier/platform | Gap |
|---|---|---|---|---|
| Invalid selectors and state return actionable codes and next actions | Unit/component and one acceptance rejection | Cover Workspace required, selector required, ambiguous target, snapshot mismatch, conflict and retry families across other workflows | PR: Ubuntu, Windows | Several public mappings missing |
| Unexpected tool/plugin exceptions do not terminate Host or expose details | Host adapter tests and throwing fixtures | Invoke throwing external query or mutation, inspect sanitised error and then invoke `server-status` successfully | PR: Ubuntu, Windows | Missing |
| Workspace/project-relative paths cannot escape allowed roots | Unit/component path validation | Sample one public `WorkspaceProjectOutsideRoot` or mutation target traversal rejection | PR: Ubuntu, Windows | Missing published path rejection |
| Symlink and traversal protections hold | Unit/component filesystem coverage | Retain component ownership; hosted symlink policy varies | Component sufficient | No acceptance case required now |
| Long Windows paths and atomic replacement work | Windows Workspace integration | Retain Windows component gate and release scenarios | Windows PR component/release | No separate acceptance required now |
| WSL-to-Windows access warns about performance | Production warning and manual evidence | Validate in WSL release environment | Release/manual WSL | Not representable on ordinary hosted runners |
| Failure diagnostics retain scenario root, process details and stderr | Existing fixture and CI artifact upload | Extend retained evidence to every new fixture asset/state root | PR: Ubuntu, Windows | Infrastructure exists |
| Successful runs leave no Host process, transaction, recovery or copied-root residue | Fixture disposal and runner validation | Make terminal cleanup part of every workflow helper/criterion | PR and release | Needs consistent enforcement |

### Platform support matrix

| Environment | Acceptance responsibility |
|---|---|
| Ubuntu pull requests | Published executable, stdio, SDK/MSBuild loading, case-sensitive paths, plugins, all deterministic acceptance workflows |
| Windows pull requests | Same public workflows plus Windows file replacement, path casing, linked-target regression and the existing native Workspace durability suite |
| macOS release/manual | Run the deterministic acceptance suite, Workspace integration and a curated external-repository scenario subset as best-effort evidence once public v1 release-candidate preparation begins |
| Native Linux release | External repositories, destructive recovery and performance baseline |
| Native Windows release | External repositories, Windows durable replacement/conflict/recovery and performance baseline |
| WSL release/manual | `/mnt/<drive>` warning and comparison of Windows-mounted versus native-Linux storage where relevant |

## Target published execution-family set

The expanded suite is complete only when these production paths each have at least one published representative:

| Production path | Required representative |
|---|---|
| Server lifecycle/status | Startup, catalogue, status and shutdown |
| Workspace lifecycle | Open, list, status, reload and close |
| Bundled query | Bounded semantic or structural query |
| External query | Packaged query with private dependency |
| Code Action query | Discovery with token metadata |
| Bundled mutation | Rename or formatting proposal through preview/commit |
| External mutation | Packaged mutation through preview/rollback |
| Token-based Code Action mutation | Existing raw-string stage/rollback |
| Dedicated/replay Code Action mutation | One deterministic refactoring tool |
| Transaction lifecycle | Start, preview, history, rollback and commit |
| Recovery/status | Blocked recovery plus clean restart after commit |
| Cancellation/concurrency | Deterministic shared lease, cancellation, retry and exclusive acquisition |

This is execution-architecture coverage, not one acceptance test per tool. Individual handlers, result branches and Code Action providers remain owned by their unit, contract and audit suites.

## Dependency-ordered implementation batches

### Batch 1 — Acceptance infrastructure and release artifact contract

**Implementation status:** Complete on 2026-07-23. The next manually initiated acceptance run supplies runtime evidence for the newly discovered cases.

- Keep the acceptance assembly free of production references.
- Centralise only repeated public envelope, Workspace identity and transaction selectors; do not import the scenario runner or hide scenario assertions behind a general harness.
- Add support for known request IDs and MCP cancellation notifications.
- Add deterministic external query and mutation fixture packages, including readiness without timing sleeps.
- Copy the small solution, solution-hierarchy, mixed-solution and multi-target/linked assets into isolated roots.
- Make terminal Host, transaction, recovery and scenario-root cleanup consistent.
- Assert that CI is exercising the exact Release publish output and record its product version.

This batch changes infrastructure but should add at least one focused protocol/configuration acceptance case so its new seams are exercised immediately.

Implemented evidence:

- the acceptance project retains only external package references and build-only fixture references;
- public success-data, Workspace identity, Workspace selector and snapshot projection are centralised without importing production contracts;
- the process fixture can send a tool request with a known request ID and publish the matching MCP cancellation notification;
- deterministic external query and mutation packages use readiness and release files rather than sleeps;
- small project, solution hierarchy, mixed solution and multi-target linked-document assets are copied into isolated scenario roots;
- process-backed and direct-stdio cases share bounded scenario-root cleanup;
- Host-path configuration requires an absolute path, CI supplies the exact Release publish output and server status asserts a non-empty product version; and
- a focused known-request-ID protocol case exercises deterministic readiness, while test discovery and the CI minimum are updated to twelve cases.

### Batch 2 — Distribution, discovery and plugin boundary

**Implementation status:** Complete on 2026-07-23. Runtime evidence will be supplied by the next manually initiated acceptance run and the Ubuntu/Windows pull-request matrix.

- configuration precedence and sensitive-value omission;
- default versus full output-schema publication;
- catalogue stability across Workspace state;
- bounded default application, zero bound and deterministic prefix;
- valid external query and mutation packages;
- invalid/colliding/throwing package isolation and sanitised diagnostics; and
- plugin discovery only after restart.

This batch precedes broader workflows because it establishes the published package fixtures and common result assertions used later.

Implemented evidence:

- published configuration precedence now covers environment values, repeated command-line scalars and omission of state/plugin roots from full public status;
- default and `Full` output-schema modes sample server, bundled query/mutation, Code Action and external query/mutation tools;
- catalogue names, descriptions, annotations and input schemas are compared before open, during a transaction and after close;
- a bounded semantic query proves omitted-default execution, zero limits, `hasMore`, stable ordering and prefix equivalence;
- valid query and mutation packages are loaded alongside invalid and throwing packages without losing unaffected tools;
- duplicate plugin IDs disable both packages deterministically, and throwing configuration diagnostics omit the exception message; and
- installing another package leaves the live catalogue unchanged until process restart.

### Batch 3 — Workspace compatibility and selectors

**Implementation status:** Complete on 2026-07-23. Runtime evidence will be supplied by the next manually initiated acceptance run and the Ubuntu/Windows pull-request matrix.

- `.csproj`, `.sln` and `.slnx`;
- mixed supported/unsupported solution diagnostics;
- no-supported or malformed rejection;
- ID, alias and path Workspace selectors;
- implicit selection with one Workspace and required selection with many;
- duplicate open, close with transaction and multi-Workspace close;
- project, document, location, copied-selection and project-qualified symbol representatives;
- ambiguous and stale selector results; and
- external edit, reload, epoch change and refreshed semantics.

Implemented evidence:

- checked-in `.csproj`, `.sln` and `.slnx` inputs exercise the published loader;
- mixed-language/legacy solutions retain supported C# projects with load diagnostics, while malformed SDK input returns a structured load failure;
- Workspace ID, alias, canonical path and single-Workspace implicit routing resolve consistently;
- duplicate open, close with an active transaction, multiple-Workspace selector requirements and closing one of two Workspaces exercise public lifecycle mappings;
- a linked multi-target fixture covers target-framework-qualified projects, project-qualified documents and symbols, span and copied-selection resolution, and ambiguous unqualified documents;
- an external edit rejects a warmed query with `WorkspaceOutOfDate` and `ReloadWorkspace`; reload advances the epoch and exposes refreshed semantics; and
- the old snapshot is rejected after reload with `SnapshotMismatch`.

The acceptance inventory is now 28 discovered cases. The CI minimum is raised to 28 so omission of any Batch 1–3 case fails the acceptance job.

### Batch 4 — Mutation families and transaction state

- bundled mutation pre-commit disk invariant;
- external plugin mutation staging and rollback;
- token and dedicated/replay Code Action mutation paths;
- no-change and rejected mutation invariants;
- Code Action create/replace commit;
- two revisions, staged queries, preview, undo, redo and rollback;
- promoted post-commit query; and
- transaction ownership transfer between Workspaces.

### Batch 5 — Durability and restart

- small multi-file and linked/multi-target physical-target commit;
- deterministic pre-write external conflict and preservation;
- blocked-recovery open rejection;
- restart after completed commit;
- recovery/status, state-directory and copied-root cleanup; and
- Windows-specific duplicate-target regression.

Application-phase conflict, abrupt termination and durable cancellation boundaries remain release-runner responsibilities.

### Batch 6 — Cancellation, concurrency and failure containment

- known-ID protocol cancellation through the published executable;
- shared-lease release after cancellation;
- configured query bound with deterministic `WorkspaceBusy` and `Retry`;
- successful retry and exclusive acquisition;
- cross-Workspace query isolation;
- throwing tool/plugin sanitisation and Host survival; and
- public path-boundary rejection.

This batch comes last because deterministic concurrency uses the external fixtures and request-control support established in Batches 1 and 2.

## Release-only scenario validation and metrics

The release runner retains:

- GuardClauses, Serilog and EF Core scale measurements;
- large bounded-query comparisons and profiling;
- broad rename and Code Action commits;
- application-phase conflict and recovery;
- abrupt termination followed by fresh-Host recovery;
- cancellation on both sides of durable application;
- repository-scale concurrency;
- native Windows/Linux comparisons; and
- WSL path-environment evidence.

Correctness, cleanup, recovery and shutdown failures gate the release. Timing differences are advisory until repeated comparable release runs establish normal variance.

The runner must produce a versioned normalised aggregate containing:

- schema version;
- Host commit and product version;
- scenario-suite content hash;
- target repository IDs and pinned commits;
- operating system, architecture, .NET runtime and processor count;
- command, scenario, parameters, warm-ups and sample counts;
- median and P95 elapsed and Host CPU where available;
- working-set and response-size observations;
- commit, restoration, cancellation and recovery phase timings; and
- correctness and cleanup status.

Release-branch runs upload detailed output and the aggregate as workflow artifacts. The final aggregate and previous-release comparison are attached to the GitHub release. Generated metrics are not committed to `main`. Comparisons must report scenario, runtime or runner drift rather than silently treating unlike runs as regressions.

## Explicit exclusions

- Do not run the external-repository scenario suite on ordinary pull requests.
- Do not assert elapsed-time thresholds in acceptance tests.
- Do not add one acceptance test per query, mutation or Code Action provider.
- Do not use internet access, repository-scale fixtures or sleeps in pull-request acceptance.
- Do not expose production internals or add production test hooks.
- Do not treat trusted in-process plugins as a security sandbox.
- Do not manufacture a delete mutation solely to create acceptance coverage.
- Do not duplicate component-only symlink, every-parser-branch or every-recovery-artifact test without a distinct published boundary.
- Do not commit generated scenario metrics to the source branch.

## Completion criteria

The release acceptance programme is complete when:

- every row marked for pull-request acceptance has a passing native Ubuntu and Windows representative or an explicit, reviewed component-only rationale;
- every production execution family in the target set is exercised through the exact Release-published executable;
- the acceptance project still has no production project reference;
- CI minimum counts match the final inventory and failures retain actionable diagnostics;
- public v1 release-candidate validation runs the deterministic suite and a curated scenario subset on macOS as best-effort evidence;
- release-only correctness scenarios pass on native Windows and Linux, with WSL evidence where applicable;
- terminal cleanup is verified for copied workspaces, transactions, recovery state and Host processes;
- the release-branch convention and workflow are implemented; and
- each release retains a normalised metrics aggregate and comparison with the previous compatible release.
