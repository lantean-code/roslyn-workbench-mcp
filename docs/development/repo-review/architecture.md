# Current Repository Architecture

Date: 2026-08-13

**Stage:** 1 — Current architecture map

**Status:** Complete

## Evidence boundary and method

This map describes only the current checked-out repository. It was derived from the current solution and project files, production source, test source and fixtures, build configuration, workflow definitions and operational scripts. No Git history, diff, branch, tag, stash, reflog, deleted or renamed artefact, external backup, historical audit, validation report, remediation record or previous review finding was used.

The inventory covers all seven production projects, all fifteen test or test-support projects in the solution, all seven plugin fixture projects, the scenario-runner project and the current workspace asset projects. Architecture discovery establishes review scope and dependency order only; no subsystem has been reviewed for defects at this stage.

## Repository shape

The product is a .NET 10 local console application that publishes an MCP server over standard input and standard output. It loads C# solutions or projects through Roslyn `MSBuildWorkspace`, retains multiple in-process Workspace sessions, exposes host-owned lifecycle and transaction tools, publishes bundled and external plugin tools, and publishes a separate internal Code Action catalogue. Source mutations are proposed against Roslyn `Solution` snapshots and reach the filesystem only through the Workspace transaction, commit and recovery pipeline.

The root solution is `Roslyn.Workbench.Mcp.slnx`. Source and ordinary test projects inherit `net10.0`, nullable reference types, implicit usings and warnings-as-errors from their respective `Directory.Build.props`; the authoring analyser targets `netstandard2.0`. The SDK is pinned to `10.0.100` with `latestFeature` roll-forward. NuGet is the sole configured package source.

## Production project graph

| Project | Role and owned surface | Direct project dependencies |
| --- | --- | --- |
| `Roslyn.Workbench.Mcp.Abstractions` | Minimal public Workspace-facing contracts used by plugin signatures: selectors, snapshot preconditions, identity and result models, validation attributes, resolver interfaces, path services, reference discovery, type hierarchy and project structure/target-framework services. It references only Roslyn Workspaces Common and is not independently packed. | None |
| `Roslyn.Workbench.Mcp.Workspace` | Workspace loading and compatibility filtering; roots and path identity; session state, selection, operation gates and leases; selector resolution; snapshot identity; query caches; change monitoring; transaction revisions and mutation staging; diff, commit, locking and recovery; project, hierarchy and reference services; performance events. | Abstractions |
| `Roslyn.Workbench.Mcp.Plugins.Analyzers` | Compile-time plugin-authoring diagnostics for entry points, handler contracts and lifetime/state rules, cancellation usage, invocation patterns, query-cache keys and tool-name policy. It is built as a Roslyn analyser for inclusion in the plugin authoring package. | None; Roslyn compiler packages only |
| `Roslyn.Workbench.Mcp.Plugins` | Public trusted in-process plugin API and its internal runtime adapters: plugin and tool attributes, entry point, handler/context/result contracts, singleton service configuration, typed registrations, handler validation, request resolution services, Workspace context adaptation and plugin query-cache scoping. It is the packable authoring package and explicitly includes the Abstractions assembly and authoring analyser in the package. | Abstractions and Workspace as private package dependencies; Plugins.Analyzers as a build/analyser dependency |
| `Roslyn.Workbench.Mcp.CodeActions` | Host-internal Code Action system: provider and MEF composition, policy, diagnostics and discovery, nested-action identities, replay references, Fix All preparation, operation evaluation, Workspace-backed query/mutation contexts, staging, typed catalogue and three Code Action tools. It is deliberately separate from Plugins. | Abstractions, Workspace |
| `Roslyn.Workbench.Mcp.Plugins.Core` | Bundled first-party plugin and its public request/result contracts, projections, diagnostic services, 37 inspection queries and two refactorings. It registers through the same plugin authoring API used by third parties. The AsyncFixer analyser binary is copied into output and publish assets for runtime diagnostic activation. | Abstractions, Plugins, Workspace |
| `Roslyn.Workbench.Mcp` | Executable Host, startup composition, configuration, logging, MCP schemas/envelopes/binding, server-owned tools, four plugin/Code Action transport adapters, plugin package discovery/loading/materialisation, status and error-reporting trust boundary. | Abstractions, Workspace, Plugins, Plugins.Core, CodeActions |

The intended dependency direction is therefore `Abstractions <- Workspace <- {Plugins, CodeActions}`, `Plugins <- Plugins.Core`, and all production components point into the Host composition root. `Plugins.Analyzers` remains a compile-time sibling whose output is packaged by Plugins. CodeActions does not depend on Plugins, Plugins does not depend on CodeActions or the MCP SDK, and external plugins receive only the public authoring/Abstractions surface despite the Host's internal Workspace adapter dependency.

## Executable entry points and composition roots

### Production Host

`src/Roslyn.Workbench.Mcp/Program.cs` is the production entry point. It redirects `Console.Out` to `Console.Error` before constructing the generic Host, which protects MCP stdout from ordinary console output. `Host.CreateApplicationBuilder(args)` flows into `AddRoslynWorkbench(args)`, then the built Host runs until stdio shutdown or cancellation.

`HostStartupComposer` is the pre-DI composition step. It resolves and validates startup options and creates the immutable three-tool Code Action catalogue. `RoslynWorkbenchHostApplicationBuilderExtensions` is the main composition root and adds options, Workspace services, plugin execution services, Code Action services, Host services, server-owned and Code Action MCP tools, hosted startup prerequisites, and the MCP server with stdio transport and the fallback/plugin exception filter.

The hosted startup order is significant:

1. `StartupConfigurationReporter` writes fallback warnings to configured logging.
2. `StartupPrerequisiteLifecycleService` registers MSBuild, creates and secures the state/recovery directories, and performs startup recovery before request handling.
3. `PluginCatalogStartupLifecycleService` loads the bundled core plugin plus external plugin packages, protects server-owned and Code Action tool names, materialises closed generic MCP adapters and publishes one immutable runtime catalogue.

`RoslynWorkbenchMcpServerOptionsConfiguration` supplies server instructions and delegates MCP list/call handling to the published runtime plugin catalogue. Server-owned and Code Action tools are registered directly with the MCP SDK; plugin tools are listed and invoked through `PluginMcpRequestHandler`.

### Test and operational executables

`test/Roslyn.Workbench.Mcp.Workspace.LockFixture/Program.cs` is a small child-process executable used to exercise cross-process commit locking.

`tools/Roslyn.Workbench.Mcp.ScenarioRunner/Program.cs` is an independent console entry point. `ScenarioApplication` parses commands and the checked-in scenario suite, prepares pinned external repositories, starts a published Host, drives MCP operations, optionally attaches EventPipe collectors, validates restoration and process state, and writes JSON/Markdown evidence. It has no project reference to production code and communicates with the Host through the published stdio protocol.

The acceptance-test project is also intentionally distribution-facing: its fixtures publish and launch the Host and communicate via the MCP client rather than taking a direct Host project reference.

## Major runtime subsystems

### Public selectors and snapshot contracts

Abstractions defines `WorkspaceSelector`, project/document/symbol/location/scope selectors, `SnapshotPrecondition`, `WorkspaceIdentity`, `DocumentReference`, `SymbolReference`, bounded result contracts and `RequiredAction`. Requests derive from `WorkspaceBoundRequest`; mutation requests add an expected snapshot requirement through `WorkspaceMutationRequest`. `IWorkspaceResolver` and the selector factory bridge author-facing contracts to Roslyn objects without exposing Host lifecycle or transaction ownership.

Workspace assigns a stable `WorkspaceId`, a new epoch for each successful open/reload instance, an internal snapshot ID, and an optional transaction ID/revision. Resolver, cache and Code Action replay identities are bound to these values so consumers can distinguish the base snapshot from staged transaction revisions and reject stale references.

### Workspace loading, state and queries

`WorkspaceLifecycleService` owns open, list, status, reload and close workflows. Absolute `.sln`, `.slnx` or `.csproj` inputs are normalised and constrained to a resolved workspace root. `WorkspaceLoadWorkflow` calls the Host-configured `MSBuildWorkspace`, filters unsupported/non-SDK or non-C# projects where applicable, removes unresolved analyser references, and rejects project or document inputs outside the root. Open and reload use a filesystem certification window to ensure inputs did not change while loading.

`WorkspaceSessionStore` holds an immutable host snapshot containing all open sessions and the single transaction owner. A per-session `WorkspaceOperationGate` admits bounded shared queries and exclusive lifecycle/mutation operations. `WorkspaceSessionAcquirer` combines selector resolution with gate acquisition. `WorkspaceExecutionContextFactory` returns explicit async-disposable query or mutation leases; plugin and Code Action projects wrap those leases in their own context types.

`WorkspaceChangeDetector` builds a manifest of the loaded solution/project files, documents, analyser/config inputs, metadata references, imported build files and relevant directories. A recursive watcher plus metadata polling detects changes while excluding known build artefact roots. A detected change moves the session to out-of-date or conflicted state and invalidates snapshot-bound caches and references.

Workspace query caching is split between host query cache entries and plugin result cache entries. Cache identities include Workspace snapshot identity; plugin cache scopes additionally include plugin ID and tool name. `IWorkspaceSnapshotLifecycleObserver` implementations invalidate plugin cache generations and Code Action references at their applicable Workspace, transaction and snapshot boundaries. Error-reporting consent observes the same mechanism but invalidates only a Workspace ID/epoch grant on close or epoch replacement; ordinary transaction and snapshot changes retain that grant.

### Transactions, commit and recovery

The Host permits one transaction owner across all loaded workspaces. `TransactionService` starts the transaction, exposes preview/history, traverses bounded undo/redo revisions, rolls back and delegates commit. Mutations never write directly: plugin handlers and Code Actions return a candidate `Solution`; Workspace validates snapshot preconditions, processes added/removed/linked documents, stages a new immutable revision, and builds bounded diff and change summaries.

Commit obtains an exclusive Workspace lease and a cross-process lock beneath `<workspace-root>/.vs/roslyn-workbench-mcp/locks/commit.lock`. Planning verifies physical containment, expected source state, file operations and platform file metadata. Before application, `CommitRecoveryStore` persists owner metadata, original-file artefacts and a versioned JSON manifest beneath the configured state directory. `WorkspaceCommitWriter` applies create/replace/delete operations through `AtomicFileWriter`, records durable phase transitions and either completes or restores. The atomic writer uses same-directory temporary files, write-through and flush-to-disk, platform-specific atomic replacement, bounded retry and permission preservation.

Startup recovery scans owner records and manifests, acquires the same workspace lock, completes committed cleanup or restores incomplete application. Malformed, unsafe or externally conflicting evidence is retained as a recovery conflict rather than silently discarded. `WorkspaceInstanceStatusPublisher` separately publishes live-process status JSON under the workspace `.vs` tree using an in-process channel worker and locked file handles, allowing status tools to report other live instances.

### Plugin platform

External plugin search roots come from repeated command-line/environment configuration. Each immediate child is one package; top-level DLLs and nested packages are not candidates. Metadata is inspected without loading first, and a valid package must identify exactly one attributed entry point with a supported API version and valid identity. Package paths and resolved managed/unmanaged dependencies must remain physically contained.

External packages receive a non-collectible `PluginAssemblyLoadContext`. Abstractions, Plugins, `Microsoft.CodeAnalysis*` and `System.Composition*` assemblies are shared from the default context; other dependencies resolve from the package. MEF composes exactly one `IRoslynPlugin`, `Configure` records typed query/mutation handlers and plugin-owned singleton services, and configuration is frozen after startup. Each enabled plugin gets its own validated service provider; handlers and services are singletons retained for catalogue lifetime and must be stateless/thread-safe. Reflection and generic construction occur during startup materialisation, after which request execution uses prebuilt strongly typed registrations and adapters.

Plugin IDs and tool names are checked against server-owned tools, Code Action tools, bundled tools and other external packages. Invalid or conflicting external plugins are disabled with status diagnostics while valid plugins remain available. Schema preflight validates transport contracts before publication. The runtime catalogue owns load contexts and plugin service-provider disposal and is immutable after startup.

### Bundled plugin tools

`BundledCorePlugin` registers three singleton diagnostic services and 39 tools. The 37 queries are: `get-solution-structure`, `get-project-details`, `get-document-options`, `get-document-outline`, `get-code-context`, `search-symbols`, `resolve-symbol`, `get-symbol-info`, `get-symbol-members`, `get-symbol-attributes`, `go-to-definition`, `find-references`, `find-callers`, `find-callees`, `find-implementations`, `find-overrides`, `find-derived-types`, `get-type-hierarchy`, `find-overloads`, `get-partial-declarations`, `get-symbol-dependencies`, `get-symbol-dependents`, `get-dependency-graph`, `find-dependency-cycles`, `find-unused-symbols`, `find-duplicate-code`, `get-diagnostics`, `analyze-nullability`, `analyze-async`, `analyze-disposables`, `analyze-control-flow`, `analyze-data-flow`, `get-operation-tree`, `get-control-flow-graph`, `get-change-impact`, `get-api-surface` and `get-test-impact`. The two mutations are `rename-symbol` and `format-document`.

Query handlers use the plugin execution services for selector/snapshot validation, Roslyn resolution, compiler/analyser diagnostics, project structure, references, type hierarchy and dependency analysis. Results are projected into bounded wire contracts. Mutation handlers construct candidate solutions and rely on the common Workspace staging boundary.

### Code Actions

Code Actions are Host-published and do not pass through plugin discovery or plugin status. The fixed catalogue contains `list-code-actions`, `prepare-fix-all` and `stage-code-action`.

`MefCodeActionComposition` resolves built-in and configured Roslyn assemblies, creates MEF host services used by `MSBuildWorkspace`, and selects C# refactoring and code-fix exports. Provider selection and policy filter the catalogue; diagnostic services activate built-in analysers and gather matching diagnostics. Discovery flattens nested actions into replay recipes containing provider identity, action path, title/equivalence key, diagnostic identities, document/span and exact Workspace snapshot identity.

References live in a bounded in-memory cache with configured expiry and Workspace/transaction/snapshot indexes. Replay re-resolves the document and diagnostics, rediscovering a unique matching provider action against the same snapshot. Fix All prepares document/project/solution actions through Roslyn `FixAllProvider`. Evaluation accepts exactly one `ApplyChangesOperation` plus a specifically recognised harmless wrapping operation and rejects other operation sets. Successful changed solutions are sent to the shared Workspace staging service.

### Host protocol and server-owned tools

The Host publishes request schemas from the current CLR request contracts, optionally publishes output schemas, binds incoming `JsonElement` argument dictionaries, checks required and enum values, applies data-annotation and recursive object-graph validation, and serialises all ordinary results into structured success/failure envelopes with continuation guidance.

Four closed-generic transport paths keep execution families separate: plugin query, plugin mutation, Code Action query and Code Action mutation. Each acquires its family-specific context lease; mutation adapters pass successful candidates to Workspace staging. `McpServerToolBase` owns binding and structured MCP results. The MCP SDK builds one handler which selects either a direct server-owned/Code Action tool or the fallback plugin handler, then wraps that combined route with the call-tool filter. `UnhandledToolExceptionFilter` owns Workbench cancellation and unexpected-exception classification for that combined route before the SDK's outer protocol handling.

The always-published server-owned tools are `get-error-details`, `server-status`, `workspace-open`, `workspace-list`, `workspace-close`, `workspace-status`, `workspace-reload`, `transaction-start`, `transaction-preview`, `transaction-history`, `transaction-commit` and `transaction-rollback`. `prepare-error-report` and `submit-error-report` are additionally published unless consent mode is `Never`.

### Error reporting and status

Unexpected failures which escape from direct or fallback tools are converted into bounded, expiring captured records keyed by correlation ID. Capture includes classified exception/stack data, invocation timing and a bounded view of request/Workspace context. `get-error-details` exposes the local record. `ExternalErrorReportProjector` creates a reduced allow-listed report, and `prepare-error-report` freezes an immutable dispatcher-specific payload and human-reviewable JSON preview in a second bounded expiring store.

Submission is single-flight per handle and stores a receipt for idempotent repeated calls. Consent may be never, always, per server session, per Workspace epoch or requested through MCP elicitation. Workspace close or epoch replacement invalidates Workspace-scoped consent for later submission requests. Without embedded Sentry build configuration, approved payloads go only to structured stderr logging. With an embedded `ROSLYN_WORKBENCH_SENTRY_DSN`, the Host constructs an isolated Sentry client and allow-lists the final event before it is accepted into the SDK's background delivery queue; the receipt represents SDK acceptance rather than remote ingestion.

`ServerStatusService` projects MSBuild registration, Code Action composition, plugin catalogue, recovery and effective configuration status to the server-owned status tool.

## Dependency injection and lifetime ownership

The production DI container registers Workspace, plugin adapters, Code Actions, protocol, status, retention stores and error-reporting services as singletons. The significant owned lifetimes are:

- the generic Host owns hosted services, the MCP server/stdio transport, the default service provider and Sentry client when enabled;
- `WorkspaceSessionStore` owns loaded Roslyn workspaces, input manifests, operation gates and transaction snapshots until explicit close or reload; it has no Host shutdown drain;
- query and mutation contexts are invocation-created lease objects, not DI scopes, and release Workspace gates through `IAsyncDisposable`;
- Workspace and plugin query caches and the Code Action reference cache are singleton state partitioned by snapshot identity and invalidated by lifecycle observers;
- `PluginCatalogSnapshot`/`PluginCatalogState` own external load contexts and one isolated singleton service provider per plugin;
- `WorkspaceInstanceStatusPublisher` owns an unbounded in-process update channel, worker task and per-workspace status file handles; and
- bounded captured-error and prepared-submission stores own expiring in-memory state; no database is used.

Hosted startup prerequisites must complete before the MCP server is considered ready. No request-time DI scope is created, so thread safety, explicit lease disposal and singleton state partitioning are architectural invariants for later review.

## Configuration declaration and consumption

For scalar settings, the last command-line occurrence wins, then the corresponding environment variable, then the code default. Invalid values fall back with a startup warning. Plugin directories are additive across repeated `--plugin-directory` values and `ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY`, then path-deduplicated. Error-reporting consent is intentionally command-line/default controlled; a consent environment variable is ignored with a warning so ambient environment cannot silently grant submission consent.

| Area | Command-line/environment inputs | Primary consumers |
| --- | --- | --- |
| Results and concurrency | `default-max-results`, `max-concurrent-queries` | Workspace options, result limiting and operation gates |
| Transaction history | `max-transaction-revisions` | Workspace transaction revision retention |
| Workspace cache | size limit and sliding expiration | `WorkspaceQueryCacheState`/store/scope |
| Plugin cache | entry limit and sliding expiration | plugin cache state/store and per-tool scopes |
| Code Action references | cache size limit and reference lifetime | reference state/store and emitted expiry |
| Plugin packages | one or more plugin directories | startup package discovery only |
| Protocol schemas | `tool-output-schema-mode` | Host schema factory and plugin schema preflight |
| Durable state | `state-directory`; otherwise XDG state home or platform local application data | state-directory security, recovery store and startup recovery |
| Error records/submissions | capacities, lifetimes and byte limits | bounded retention policies, projection and payload preparation |
| Error consent | `--error-reporting-consent` (`never`, `prompt`, `always`) | tool publication, availability and consent state |
| External dispatcher | build-time `ROSLYN_WORKBENCH_SENTRY_DSN` assembly attribute | startup dispatcher selection and Sentry destination |

`WorkspaceOptions.MaxLoadedWorkspaces` remains a Workspace-owned default of four rather than a startup option. `CodeActionCompositionOptions` supports built-in assembly inclusion and additional assemblies through DI options, but the production startup path currently registers its defaults. Plugin discovery is startup-only and there is no dynamic reload configuration.

## External and trust boundaries

| Boundary | Current implementation |
| --- | --- |
| MCP process protocol | JSON-RPC/MCP over stdin/stdout via `ModelContextProtocol`; ordinary logging and redirected console output go to stderr. |
| Workspace/build system | `Microsoft.Build.Locator` and Roslyn `MSBuildWorkspace`; opening trusted projects may execute MSBuild evaluation/build logic and load analyser references. |
| Filesystem reads | Solutions/projects, source/additional/analyser-config documents, imported build files, references, plugin package DLLs/deps files and state/recovery records. |
| Filesystem writes | Transaction commits to source files; recovery/owner/manifests under the state directory; commit locks and live-instance status beneath workspace `.vs`; atomic temporary/backup/delete-marker files during durable commit. |
| Cross-process coordination | Exclusive file lock for commit and locked live-instance status files; there is no external message broker. |
| In-process messaging | `Channel<WorkspaceInstanceStatusUpdate>` serialises advisory instance-status writes. |
| Dynamic code/extensions | Trusted external plugin assemblies and dependencies, Roslyn analyser assemblies, MEF Code Action providers and bundled AsyncFixer. Plugins are not sandboxed. |
| Network | Production network dispatch exists only through an embedded Sentry provider. The scenario runner also invokes Git and repository preparation commands against pinned external repositories and package feeds. |
| Persistence | JSON recovery evidence plus original-file byte artefacts; all other server session, cache, catalogue, error and consent state is in memory. No database is present. |
| Child processes/diagnostics | Acceptance and scenario infrastructure launches the Host; scenario preparation runs external commands and Git; profiling attaches EventPipe/diagnostic tools and samples process metrics. |

## Test and verification projects

| Project | Direct project dependencies | Claimed boundary or behaviour |
| --- | --- | --- |
| `Roslyn.Workbench.Mcp.Workspace.Test` | Workspace, Plugins, TestSupport | Unit/contract coverage for Workspace configuration, paths, selection/resolution, loading helpers, state/gates/leases, change detection, caches, transactions, commit/recovery, project/reference/hierarchy services and validation attributes. |
| `Roslyn.Workbench.Mcp.Workspace.IntegrationTest` | Workspace, Plugins, CodeActions, IntegrationTestSupport, LockFixture | Real filesystem/MSBuild Workspace lifecycle, compatibility, resolution, external changes, atomic I/O, durable transactions/recovery and cross-process locking. |
| `Roslyn.Workbench.Mcp.Plugins.Analyzers.Test` | Plugins.Analyzers, Plugins | Authoring analyser descriptors and compile-time behaviour for entry points, handlers, state/lifetimes, invocation, cancellation and query cache use. |
| `Roslyn.Workbench.Mcp.Plugins.Test` | Abstractions, Plugins, TestSupport | Public API/architecture contracts, configuration/validation/materialisation, execution contexts, cache scopes, request/diagnostic/dependency services and Workspace mapping. |
| `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Plugins.Core, TestSupport | Bundled public API and registration plus focused unit coverage for every bundled query/mutation tool, projections, diagnostics and shared handlers. |
| `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` | Plugins.Core, IntegrationTestSupport | Real component Workspace traces for selector/snapshot semantics, semantic inspection/search, analyser activation, projections and mutation staging. |
| `Roslyn.Workbench.Mcp.CodeActions.Test` | CodeActions, TestSupport | Unit/contract coverage for composition, policy, discovery, identities/references, replay/Fix All, operation evaluation, contexts, staging, catalogue and all three tools. |
| `Roslyn.Workbench.Mcp.CodeActions.IntegrationTest` | CodeActions, IntegrationTestSupport | Real MEF/built-in and controlled-provider composition, diagnostic sources, discovery and staging. |
| `Roslyn.Workbench.Mcp.CodeActions.AuditTest` | CodeActions, IntegrationTestSupport | Audit-category inventory, direct built-in provider offer discovery and operation compatibility. The current harness names replay compatibility but does not exercise stored-reference resolution or staging. |
| `Roslyn.Workbench.Mcp.Test` | Host, TestSupport | Host unit/contract coverage for configuration, composition architecture, plugin loading, protocol schemas/binding/envelopes, adapters, lifecycle tools, status and the complete local/external error-reporting state machine. |
| `Roslyn.Workbench.Mcp.IntegrationTest` | Host, IntegrationTestSupport, selected plugin fixtures | Host composition, MSBuild prerequisites, plugin package/load-context/MEF boundaries, MCP cancellation/schema/adapter behaviour, Sentry envelope projection, Workspace containment and recovery status. |
| `Roslyn.Workbench.Mcp.AcceptanceTest` | Plugin fixtures as build-only assets; no Host project reference | Published-distribution and real-process stdio MCP coverage for startup/shutdown, catalogue/schema, Workspace lifecycle/selection/reload/containment, transactions/durability/recovery, Code Actions and external plugin success/failure isolation. |
| `Roslyn.Workbench.Mcp.TestSupport` | Abstractions, Plugins, Workspace | Shared in-memory Roslyn data and visible Moq graph construction for unit tests. |
| `Roslyn.Workbench.Mcp.IntegrationTestSupport` | Host, Workspace, Plugins, Plugins.Core, CodeActions | Shared temporary assets, real Workspace/component sessions, MSBuild registration and controlled Code Action providers for integration/audit projects. |
| `Roslyn.Workbench.Mcp.Workspace.LockFixture` | None | Executable lock holder used by Workspace integration tests. |

The plugin fixture projects are all non-test package inputs built for integration or acceptance tests:

| Fixture project | Purpose |
| --- | --- |
| `Roslyn.Workbench.Mcp.ConsoleOutputPluginFixture` | Plugin that writes to the console, used to verify stdout protocol containment. |
| `Roslyn.Workbench.Mcp.HostQueryPluginFixture` | Valid dependency-bearing query plugin with cache-calibration controls. |
| `Roslyn.Workbench.Mcp.HostMutationPluginFixture` | Valid mutation plugin used to drive Host staging and transaction paths. |
| `Roslyn.Workbench.Mcp.InvalidPluginFixture` | Duplicate tool, unsupported API and valid-control entry points for package rejection/isolation. |
| `Roslyn.Workbench.Mcp.InvalidToolNamePluginFixture` | Invalid tool-name policy case. |
| `Roslyn.Workbench.Mcp.ThrowingPluginFixture` | Plugin whose configuration throws. |
| `Roslyn.Workbench.Mcp.UnsupportedSchemaPluginFixture` | Tool whose response cannot be published under the supported transport schema. |

The current project-based test assets are also part of review scope:

| Asset project(s) | Purpose |
| --- | --- |
| `test/TestAssets/PluginAnalyzerPackageConsumer/ExternalPlugin.csproj` | Builds solely against the packed `Roslyn.Workbench.Mcp.Plugins` surface to validate package and analyser composition. |
| `CompatibilitySamples/AmbiguousProjectGraph/ProjectOne/Sample.csproj` and `ProjectTwo/Sample.csproj` | Duplicate project names, a project reference and a physically shared linked document. |
| `CompatibilitySamples/LegacyNet472/Legacy.csproj` | Unsupported legacy non-SDK C# project. |
| `CompatibilitySamples/MalformedSdkProject/Broken.csproj` | Malformed SDK project load failure. |
| `CompatibilitySamples/MixedSolution/Legacy/Legacy.csproj`, `Supported/Supported.csproj` and `VisualBasic/VisualBasic.vbproj` | Mixed legacy, supported SDK C# and non-C# solution compatibility. |
| `InspectionSample/Base/Sample.csproj` | Primary semantic, formatting, diagnostic and Code Action component/acceptance fixture. |
| `InspectionSample/Profiles/CSharp4/Sample.csproj`, `CSharp73/Sample.csproj` and `NullableDisabled/Sample.csproj` | Language-version and nullable variants. |
| `InspectionSample/Profiles/ProgramMainToTopLevelCodeFix/Sample.csproj`, `ProgramMainToTopLevelRefactoring/Sample.csproj`, `TopLevelToProgramMainCodeFix/Sample.csproj` and `TopLevelToProgramMainRefactoring/Sample.csproj` | Code Fix/refactoring profile variants for program/top-level transformations. |
| `MultiTargetLinked/Linked/Linked.csproj` and `MultiTargetLinked/MultiTarget/MultiTarget.csproj` | Linked source shared across a single-target project and a multi-target project. |
| `SdkProject/Sample.csproj` | Minimal supported standalone SDK project. |
| `SolutionHierarchy/App/App.csproj` and `SolutionHierarchy/Lib/Lib.csproj` | `.sln`/`.slnx` folders and direct project-reference hierarchy. |

Non-project asset profiles supply editorconfig variants for auto-properties, block-scoped namespaces and defaults. Together these assets cover SDK projects, `.sln`/`.slnx`, solution folders/project references, ambiguous and linked documents, multi-targeting, legacy and malformed projects, mixed C#/VB solutions, language-version/nullable/editorconfig variants and Code Action inputs.

The Scenario Runner is operational evidence rather than an xUnit test project. Its checked-in suite covers pinned small (`GuardClauses`), medium (`Serilog`) and large (`EF Core`) repositories; bounded query variants; Code Action discovery/Fix All/staging; plugin cache pressure/coalescing; durable create/replace/delete commits; cancellation boundaries; conflicts and forced-crash recovery; external-change/reload, live-build and watcher stress; multi-revision state; concurrency; EventPipe traces/counters/gcdumps; process/memory metrics; restoration and leaked-state checks.

## CI and execution gates

`.github/workflows/tests.yml` restores and builds the whole solution, runs unit/contract tests with integration/audit categories excluded, runs four integration projects independently, and runs the published Host acceptance suite on Linux and Windows; fixed minimum TRX counts prevent empty or silently reduced suites. Windows acceptance also reruns Workspace durability integration coverage. `.github/workflows/code-action-audit.yml` runs the Code Action audit on relevant changes, pushes, a weekly schedule and manual dispatch.

`test/Directory.Build.targets` enforces category/project-name consistency and prevents ordinary unit projects from consuming integration support. The platform acceptance wrappers run the complete acceptance project. Scenario wrappers restore diagnostic tools, publish isolated Release Host/runner/plugin outputs, clear the Sentry DSN, use OS-local temporary/cache locations and invoke the independent runner.

## Representative end-to-end paths for later review

- Startup: `Program` -> option resolution/validation -> Host DI -> MSBuild registration -> state-directory initialisation/recovery -> Code Action composition -> bundled/external plugin discovery and materialisation -> MCP stdio readiness.
- Workspace query: MCP call -> binding/validation -> plugin query adapter -> Workspace shared lease and external-change check -> snapshot-bound plugin context/cache -> bundled or external handler -> Roslyn services -> bounded result mapping -> structured MCP envelope.
- Plugin mutation: MCP call -> plugin mutation adapter -> exclusive transaction-bound Workspace context -> handler candidate `Solution` -> mutation validation/link reconciliation -> new transaction revision -> preview/history -> commit planning/lock/recovery persistence -> atomic filesystem application -> session reload/close semantics.
- Code Action: MCP call -> Code Action adapter -> query context -> provider/diagnostic discovery -> expiring replay reference -> replay/Fix All resolution against exact snapshot -> operation evaluation -> Workspace mutation staging -> normal commit boundary.
- Unexpected tool error report: direct or fallback tool exception -> combined call-tool filter -> bounded captured record -> local details -> allow-listed projection and immutable preview -> consent state/elicitation -> single-flight dispatcher -> stderr write or Sentry queue-acceptance receipt.
- Operational scenario: wrapper publish -> external repository preparation -> stdio Host launch -> Workspace open and scenario calls -> optional EventPipe/process instrumentation -> transaction/recovery/restoration checks -> normal stdin shutdown -> JSON/Markdown validation outputs.

These paths define the direct producers, consumers and external boundaries that each dependency-ordered review unit must follow. They are navigation targets, not conclusions about correctness.
