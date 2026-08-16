# Current repository architecture

## Scope and evidence

This map describes the complete current tracked repository at commit `43e744acffad098507c706ab711de227ae9d8f11`. The worktree was clean before and after inspection.

Evidence was limited to the current solution and project files, tracked source, current normative documentation, configuration, workflows, tests, compiled fixtures and inert test assets. No Git history, prior branches, tags, stashes, reflogs, removed review artefacts, historical audit conclusions or prior findings were consulted.

The repository targets .NET 10. `global.json` requests SDK `10.0.100` with `latestFeature` roll-forward; SDK `10.0.102` was available during inspection. Production and most test/tool projects target `net10.0`; the plugin authoring analyser targets `netstandard2.0`.

## Repository-level runtime model

Roslyn Workbench is a local console Host exposing a Model Context Protocol server over standard input and standard output. It loads trusted C# projects and solutions through `MSBuildWorkspace`, publishes Roslyn inspection and source-mutation tools, and confines supported source writes to a transaction and durable commit pipeline.

The main dependency direction is:

```text
Roslyn.Workbench.Mcp.Abstractions
                ^
                |
Roslyn.Workbench.Mcp.Workspace
        ^               ^
        |               |
Roslyn.Workbench.Mcp.Plugins    Roslyn.Workbench.Mcp.CodeActions
        ^               ^
        |               |
Roslyn.Workbench.Mcp.Plugins.Core
                \       /
                 \     /
              Roslyn.Workbench.Mcp
```

`Roslyn.Workbench.Mcp.Plugins.Analyzers` is a separate compile-time analyser lane. It has no production project reference, is included in the plugin authoring package, and shares the tool-name policy source with `Roslyn.Workbench.Mcp.Plugins`.

The solution contains 30 build projects: 7 production projects; 15 test, test-support or lock-fixture projects; 7 compiled plugin fixture projects; and 1 manual scenario-runner tool. Checked-in `.csproj`, `.sln` and `.slnx` files under `test/TestAssets` are inert test data, not solution projects.

## Production projects and references

| Project | Output and references | Responsibility |
| --- | --- | --- |
| `Roslyn.Workbench.Mcp.Abstractions` | `net10.0` library; no project references; depends only on Roslyn Workspaces Common | Minimal public Workspace-facing contract assembly: workspace, project, document, location, span, symbol and scope selectors; snapshot preconditions; bounded results, diagnostics, warnings and change summaries; resolver, path, hierarchy, project-structure, target-framework and reference-discovery service contracts; validation attributes. It is not independently packed, but its assembly is inserted into the plugin authoring package. |
| `Roslyn.Workbench.Mcp.Workspace` | `net10.0` library; references Abstractions | Workspace loading, compatibility filtering, path identity, selection, session state, operation gates and leases, snapshot resolution, project/reference/hierarchy services, query caches, external-input certification and monitoring, transaction history and staging, filesystem containment, cross-process coordination, durable commit, recovery and reload/close semantics. |
| `Roslyn.Workbench.Mcp.Plugins` | `net10.0` packable library; references Abstractions and Workspace with `PrivateAssets=all`; build-only reference to Plugins.Analyzers | Supported third-party authoring surface plus Host-side preparation and execution adaptation. Owns plugin attributes, startup configuration, typed handler contracts, plugin-owned singleton registration, query/mutation contexts, result and mutation-proposal contracts, read-only analysis services, query-cache API, registration materialisation and Workspace result mapping. It does not depend on CodeActions or the MCP SDK. |
| `Roslyn.Workbench.Mcp.Plugins.Core` | `net10.0` library; references Abstractions, Workspace and Plugins | Bundled first-party plugin. Registers the ordinary inspection/navigation/analysis tools and the `rename-symbol` and `format-document` mutation tools through the same plugin configuration and materialisation path as external plugins. Ships the AsyncFixer analyser assembly beside the Host for `analyze-async`. |
| `Roslyn.Workbench.Mcp.CodeActions` | `net10.0` library; references Abstractions and Workspace | Separate internal Code Action system. Owns its internal catalogue, request/result contracts, Roslyn MEF provider composition, diagnostic collection, policy, discovery, nested-action projection, opaque replay references, Fix All preparation, replay/evaluation and mutation-proposal production. It does not depend on Plugins or the MCP SDK. |
| `Roslyn.Workbench.Mcp.Plugins.Analyzers` | `netstandard2.0` analyser library; no project references | NuGet-delivered plugin-authoring diagnostics `RWMCP001`–`RWMCP023`: workspace mutation restrictions, invocation-snapshot use, synchronous configuration, handler shape and lifetime, public transport contracts, state/thread-safety warnings, cancellation, bounded query results, metadata/API versioning, cache-key/value rules, tool-name compatibility and protocol-exception restrictions. |
| `Roslyn.Workbench.Mcp` | `net10.0` executable; references Abstractions, Workspace, Plugins, Plugins.Core and CodeActions | Executable bootstrap and composition root; MCP transport, request binding, schemas, envelopes and adapters; server-owned tools; plugin discovery/loading; startup and shutdown; MSBuild registration; status; unexpected-error capture and explicit external-report workflow. This is the only production project referencing `ModelContextProtocol`, Sentry and `Microsoft.Build.Locator`. |

Important external package boundaries include Roslyn Workspaces/Features and MSBuild, `ModelContextProtocol`, `System.Composition`, `Microsoft.Extensions.Hosting` and dependency injection, `Microsoft.Extensions.Caching.Memory`, DiffPlex, Stateless, System.IO abstractions, `Microsoft.VisualStudio.SolutionPersistence`, NuGet.Versioning, AsyncFixer and Sentry.

## Executable entry points and composition roots

### Main Host

`src/Roslyn.Workbench.Mcp/Program.cs` is the production entry point. It redirects normal `Console.Out` to `Console.Error`, reserving raw stdout for MCP framing; creates a generic Host builder from command-line arguments; calls `AddRoslynWorkbench`; builds the Host; and runs it until stdio termination or Host shutdown.

`HostStartupComposer` resolves and validates startup configuration before DI construction and creates the fixed internal Code Action tool catalogue. `RoslynWorkbenchHostApplicationBuilderExtensions` then registers options, Workspace services, plugin execution services, Code Action services, Host/protocol/error services, MCP tools, hosted lifecycle services and the stdio MCP server.

Startup lifecycle ordering is registration-ordered:

1. `StartupConfigurationReporter` reports fallback warnings.
2. `StartupPrerequisiteLifecycleService` registers MSBuild, secures and probes the state directory, then performs startup commit recovery.
3. `PluginCatalogStartupLifecycleService` loads the bundled plugin and configured external plugins, creates their typed MCP adapters and publishes the immutable runtime catalogue.
4. `WorkspaceShutdownLifecycleService` closes all Workspace sessions during Host shutdown.

`RoslynWorkbenchMcpServerOptionsConfiguration` supplies server instructions and the dynamic plugin request handler. The MCP SDK also consumes the statically registered server-owned and Code Action `McpServerTool` services. The exact combination mechanics between the SDK's registered-tool handler and the Host's dynamic plugin handler are partly package-owned; Host composition and published-process tests claim the resulting catalogue contains all families.

### Workspace lock fixture

`test/Roslyn.Workbench.Mcp.Workspace.LockFixture/Program.cs` is a small test executable. It acquires an operating-system byte-range lock on a supplied file, prints `LOCKED`, waits for stdin, then releases the lock. Workspace integration tests use it to exercise real cross-process commit-lock contention.

### Scenario runner

`tools/Roslyn.Workbench.Mcp.ScenarioRunner/Program.cs` is a manually invoked release-validation executable. It parses the scenario command, loads `scenario-suite.json`, prepares pinned external repositories, launches a published Host through MCP stdio, runs measurements or destructive lifecycle scenarios, validates cleanup, and writes reports.

## Dependency-injection ownership and lifetimes

The executable Host owns the application container. Production registrations are overwhelmingly interface-to-implementation singletons.

### Process-lifetime singletons

Singleton groups include filesystem, path normalisation/comparison/containment and atomic-write services; state-directory, recovery-store, commit-plan, lock, commit-writer and recovery services; Workspace session, selector, loader, lifecycle, transaction, resolver, change-detection and query services; Workspace and plugin cache state; plugin-facing analysis and execution-context services; Code Action composition, discovery, replay, Fix All, staging and reference state; protocol schema, binding, validation, serialisation and adapter factories; plugin discovery, inspection, loading, composition, validation and catalogue services; server status, MSBuild registration and error-reporting services; `TimeProvider.System`; dispatchers; and bounded stores.

Multiple `IWorkspaceSnapshotLifecycleObserver` implementations are singletons. The session store notifies them so plugin query caches, Code Action references and Workspace-scoped reporting consent are invalidated with Workspace lifecycle transitions.

### Workspace-owned lifetimes

Each successful `workspace-open` creates an `MSBuildWorkspace`, effective Roslyn `Solution`, input manifest/change monitor, operation gate and coordination handle. These are retained in a `WorkspaceSessionSnapshot` until close, reload or Host shutdown.

The synchronised process-wide session store owns a bounded loaded-Workspace set, monotonically allocated Workspace epochs and snapshot/transaction identities, the single global transaction-owner slot, and cache/lifecycle notifications. Query leases are shared and bounded by `MaxConcurrentQueries`; lifecycle, transaction and mutation operations acquire exclusive access. Per-invocation contexts and leases are disposed after adapter completion.

### Plugin-owned lifetimes

Each enabled plugin receives a separate validated `ServiceProvider`. Plugin-declared services and handlers are singletons in that provider. Handlers are resolved during startup, retained for the catalogue lifetime, and expected to be stateless or thread-safe.

Plugin providers are disposed in reverse materialisation order. External assemblies use a package-specific, non-collectible `AssemblyLoadContext`. Roslyn, System.Composition, Plugins and Abstractions are shared from the default context; private managed and unmanaged dependencies resolve within, and must remain contained by, the package directory.

### Cache and temporary-state lifetimes

- Workspace query caches are process singletons partitioned by Workspace, effective `Solution` and component identity.
- Plugin query caches add plugin ID and tool name to exact Workspace snapshot identity. Invocation-facing scopes become unusable when the query returns.
- Identical misses are coalesced. Null, disposable, failed and cancelled public plugin-cache values are not retained.
- Code Action references use a bounded absolute-expiry memory cache, defaulting to five minutes, indexed by Workspace, epoch, transaction and snapshot.
- Captured errors and prepared submissions use separate bounded, expiring, process-local stores.
- Reporting consent is process-local; Workspace consent is additionally bound to Workspace ID and epoch.

## Major subsystems

### Workspace loading and lifecycle

`WorkspaceLifecycleService` owns open, list, status, reload, close and shutdown. Opening normalises and validates the path, alias, root and allowlisted MSBuild properties; checks duplicates, capacity and recovery; starts input certification; loads through `MSBuildWorkspace`; filters unsupported or unusable projects and unresolved analyser references; allocates identity and advisory status; builds the evaluated input manifest; verifies read-only inputs and load-time stability; creates the epoch, gate and ready session; and registers it atomically.

The loaded project/solution must be inside the Workspace root. Evaluated source, additional and analyser-config documents may be outside it and remain queryable but read-only. Recursive monitoring handles trusted in-root inputs while fingerprint polling covers external evaluated inputs.

Status refreshes external-change and other-instance state. Reload requires an out-of-date non-transactional session, reuses retained MSBuild properties, re-certifies inputs and replaces the epoch. Close rejects active/conflicted transactions and disposes owned resources.

### Selection, snapshots and execution contexts

Abstractions defines Workspace, project, document, scope, location, selection/span and symbol selectors plus `SnapshotPrecondition`. Workspace selection accepts ID, alias or path and permits omission only when unambiguous. `WorkspaceResolver` resolves against the effective immutable solution for the acquired lease.

The effective solution is the loaded baseline without a transaction or the current staged revision during a transaction. Context acquisition records Workspace identity, epoch, transaction revision and snapshot identity. Snapshot-sensitive stale inputs are rejected instead of being reinterpreted. Plugins and CodeActions wrap the neutral Workspace context independently; only mutation contexts expose staging.

### Transactions, commit and recovery

Only one Workspace may own the Host transaction slot. `TransactionService` owns start, preview, undo, redo and rollback. A transaction retains a baseline solution and bounded revision history. Staging validates snapshot, graph, changed documents, linked-document consistency and containment before appending a revision.

`TransactionCommitService` is the sole public source-write path. It validates transaction and inputs; obtains the Workspace-root lock; builds and validates a plan; reads and hashes original bytes; serialises intended bytes while preserving representation; persists owner, artefact and manifest recovery state; revalidates disk; enters `Applying`; performs atomic create/replace/delete operations; validates applied state and input certification; promotes a committed Workspace snapshot; and removes recovery evidence. Cancellation before application restores the staged transaction. Application and recovery-terminal work is non-cancellable after the durable boundary.

Recovery is stored below the configured state directory in per-commit owner/manifest/backup/staged records. Startup recovery completes before MCP initialisation. Invalid or unfinished state remains visible through `server-status` and blocks affected Workspace opening. Cross-process locking and advisory instance status live beneath `<workspaceRoot>/.vs/roslyn-workbench-mcp`.

### Plugin platform

Plugins are trusted in-process assemblies discovered once at startup. Host enumerates immediate package directories, enforces containment, inspects top-level metadata before execution, requires one marked entry assembly, validates identity/version/API/entry point, loads through a package context, composes one `IRoslynPlugin`, freezes configuration, validates handlers/metadata/names/schemas/dependencies, resolves collisions, creates an isolated singleton provider, resolves closed generic handlers and materialises typed MCP adapters.

Reserved names include server-owned and Code Action tools. Bundled tools load first. Query handlers receive a snapshot-bound query context with resolver, analysis and cache services. Mutation handlers return `MutationCandidate`; Host detects illicit Workspace mutation and stages accepted candidates through Workspace. Reflection and generic construction occur at startup; execution uses prebuilt closed adapters.

### Bundled ordinary tools

`BundledCorePlugin` registers 39 tools spanning solution/project/document structure, symbol metadata and navigation, reference/call/type relationships, dependency/cycle/unused/duplicate analysis, diagnostics, nullability/async/disposable analysis, flow/operation graphs, change/API/test impact, and `rename-symbol`/`format-document` mutation proposals. Internal CLR contracts become published JSON/schema contracts. Collections use deterministic bounded projections. `analyze-async` supplies AsyncFixer plus CS4014 without requiring target-project installation.

### Code Actions

CodeActions is separate from plugins and registers `list-code-actions`, `prepare-fix-all` and `stage-code-action`. Startup MEF composition reads C# fix/refactoring providers; composition failure disables Code Actions without blocking Host startup.

Listing validates the originating snapshot before interpreting UTF-16 coordinates, discovers and filters actions, flattens nested leaves, bounds output and stores opaque snapshot-bound recipes. Fix All preparation replays an originating fix, creates/evaluates Fix All, normalises the Workspace candidate, bounds impact, computes candidate identity and stores a prepared recipe. Staging replays normal or prepared references and returns a candidate; the Host mutation adapter performs final staging and consumes references after success or changed prepared output.

### Host protocol

Server-owned tools cover server status; Workspace lifecycle; transaction lifecycle; error details; and conditionally report preparation/submission. Reporting tools are omitted when consent is `never`, though names remain reserved.

Four closed generic adapters serve plugin query, plugin mutation, Code Action query and Code Action mutation. `McpServerToolBase<TRequest>` binds JSON, checks required properties, enums, annotations and nested validation, then dispatches. Query adapters acquire shared contexts; mutation adapters acquire exclusive transaction contexts and stage proposals.

Structured success is `{ "ok": true, "data": ... }`; structured failure is `{ "ok": false, "error": ..., "continuation": ... }`. Output schemas are optional but response-contract admission is not. The top-level filter preserves cancellation and deliberate protocol exceptions; other failures are captured under a correlation ID and returned generically.

### Error reporting

Unexpected errors are retained locally in a bounded expiring store. `get-error-details` exposes local diagnostics. External reporting separately projects an allowlisted sanitised report, creates immutable provider bytes and preview JSON, stores an opaque expiring handle, then applies consent and sends exactly that payload. Consent modes are `never`, `prompt` and `always`; prompt uses MCP elicitation. Submission is idempotent after receipt and serialised per handle. Embedded Sentry configuration uses explicit capture and SDK HTTPS transport; otherwise approved JSON is written to stderr without network submission.

## Public, package and cross-project contracts

### Supported package surface

`Roslyn.Workbench.Mcp.Plugins` is the supported authoring package and contains Plugins.dll, matching Abstractions.dll, Plugins.Analyzers under `analyzers/dotnet/cs`, and the authoring guide. Its public surface includes plugin/tool attributes and API versions; plugin/service configuration; typed query/mutation handlers; query/mutation/execution contexts; `IQueryResponse`; query-cache contracts; execution outcomes/errors; mutation candidates; analysis services and result models; and Abstractions selectors, results, validation and Workspace contracts.

### Internal cross-project seams

Workspace exposes internals to Host, Plugins, Plugins.Core and CodeActions. Plugins exposes preparation, typed registration and adaptation internals to Host and Plugins.Core. CodeActions exposes its catalogue, contexts and typed registrations to Host. Host owns MCP DTOs, schemas, envelopes and transport. Plugins.Core owns bundled CLR contracts whose JSON shapes Host publishes. Tests receive targeted internal access. Project references, visibility and architecture tests therefore enforce much of the boundary.

## External boundaries

### Filesystem

Host reads Workspace/MSBuild inputs; evaluated documents; plugin metadata and dependencies; transaction source; recovery records; and coordination state. It writes coordination records, durable recovery data and committed source changes. Path normalisation, physical containment and reparse checks are explicit. Mutation targets must remain within both Workspace root and loaded project boundaries.

### Process and executable-code trust

Workspace opening evaluates trusted MSBuild logic. Diagnostic and Code Action operations can execute project analysers. Plugins execute in-process with Host permissions. These boundaries are intentionally unsandboxed.

### Messaging

There is no broker or database. Internal asynchronous messaging uses channels for watcher and instance-status events. External messaging is MCP JSON-RPC over stdio, including cancellation and elicitation.

### Network and persistence

Normal Host has no listener. Designed outbound transport is explicit Sentry submission; trusted build logic, analysers and plugins can independently use process/network APIs. Scenario preparation uses Git and repository restore traffic. Durable application state is filesystem recovery. Workspaces, caches, references, errors, reports, consent and catalogue state are process-local.

## Configuration declaration and consumption

`StartupOptionsResolver` parses command-line and environment values. Command-line scalars override environment values; repeated scalars use the last value. Plugin directories combine both sources and are path-deduplicated. Invalid values fall back with explicit warnings.

| Concern | Default | Consumers |
| --- | ---: | --- |
| Plugin directories | none | Startup plugin discovery |
| Default maximum results | `100` | Plugin execution compatibility baseline |
| Code Action reference lifetime | 5 minutes | Reference creation |
| Workspace query cache capacity | `10000` units | Workspace cache state |
| Plugin query cache capacity | `10000` entries | Plugin cache state |
| Code Action reference capacity | `75000` units | Reference cache |
| Workspace/plugin cache idle expiry | 1 hour | Cache state |
| Maximum transaction revisions | `20` | Transaction history |
| Maximum concurrent queries | `2` | Workspace gates |
| Output schema mode | `Omit` | Protocol factories |
| State directory | per-user application state | Security and recovery |
| Reporting consent | `prompt` | Tool publication and submission |
| Captured error capacity/lifetime/size | `100`, 1 hour, 64 KiB | Local error store |
| Prepared submission capacity/lifetime/size | `50`, 30 minutes, 64 KiB | Prepared-report store |

Workspace options also set `MaxLoadedWorkspaces = 4`, not exposed through startup resolution. Workspace-open separately accepts allowlisted `artifactsPath`, `configuration`, `platform`, `targetFramework` and `runtimeIdentifier`, retained across reload. `ROSLYN_WORKBENCH_SENTRY_DSN` is a build-time input embedded as an assembly attribute, not a runtime option.

Build uses centrally managed direct package versions, nullable latest C#, and production warnings as errors. Test targets assign Integration/Audit categories and restrict support-layer consumption. Main CI runs Unit/Contract on Ubuntu, four component integrations on Ubuntu, acceptance on Ubuntu and Windows, and Workspace integration on Windows. A separate workflow runs Code Action compatibility audit. `scenario-suite.json` governs the manual external-repository runner.

## Representative cross-project flows

### Startup and publication

```text
Program -> HostStartupComposer -> option validation and Code Action catalogue
        -> Host DI composition -> startup prerequisites and recovery
        -> plugin discovery/composition/materialisation
        -> MCP SDK stdio server -> server, Code Action and plugin catalogues
```

### Ordinary query

```text
MCP call -> Host binding/validation -> PluginQuery adapter
         -> Plugins execution-context factory -> Workspace shared lease/effective solution
         -> handler + resolver/Roslyn/cache -> result mapping/serialization -> stdio
```

### Ordinary mutation and commit

```text
MCP call -> PluginMutation adapter -> exclusive mutation lease -> handler candidate
         -> containment + Workspace processing/staging -> transaction revision

transaction-commit -> validation/lock -> plan/recovery persistence -> atomic writes
                   -> applied-state validation -> committed Workspace snapshot -> cleanup
```

### Code Action workflow

```text
list-code-actions -> Workspace query lease -> providers/diagnostics/policy
                  -> snapshot-bound opaque reference
prepare-fix-all -> replay -> Fix All evaluation -> normalized bounded identity -> prepared reference
stage-code-action -> exclusive lease -> replay/evaluation -> candidate -> final staging/consumption
```

### External change and reporting

```text
watcher/fingerprint -> change detector -> out-of-date or conflicted session
workspace-reload -> exclusive reload/certification -> new epoch -> disposal/invalidation

unexpected exception -> filter -> bounded local correlation
prepare report -> allowlisted immutable payload -> expiring handle
submit report -> consent -> stderr or Sentry -> receipt
```

## Test, support and fixture projects

### Unit, contract, integration, audit and acceptance projects

| Project | Claimed behavioural boundary |
| --- | --- |
| `Roslyn.Workbench.Mcp.Workspace.Test` | Unit/contract coverage for caches, change detection/certification/watchers, options, bounded contracts, coordination, diagnostics, contexts/leases, hierarchy, path/atomic IO, lifecycle/loading, project/TFM resolution, recovery, references, selectors, state machines, transactions, linked documents and validation attributes. |
| `Roslyn.Workbench.Mcp.Workspace.IntegrationTest` | Real filesystem/MSBuild loading, external evaluated inputs, compatibility, unresolved analysers, hierarchy/TFMs, resolution, change detection, external read-only documents, atomic writes, lifecycle, persistence, commit/recovery and inter-process locking. |
| `Roslyn.Workbench.Mcp.Plugins.Test` | Public API, configuration freezing, handler/type/lifetime validation, names, typed materialisation, contexts/results, cache scopes, analysis services, selector rejection and result invariants. |
| `Roslyn.Workbench.Mcp.Plugins.Analyzers.Test` | All authoring analyser diagnostics, descriptors and configuration. |
| `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Every bundled handler, bases, diagnostic services, projections, flow-region resolution, registration and API/architecture constraints. |
| `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` | Real Roslyn semantic inspection, solution search, selector/snapshot semantics, projections, AsyncFixer provisioning and mutation staging. |
| `Roslyn.Workbench.Mcp.CodeActions.Test` | Architecture, composition, analyser activation, diagnostics, discovery/projection, contexts, evaluation, Fix All, policy, references, typed registration, resolution, replay, staging and all three tools. |
| `Roslyn.Workbench.Mcp.CodeActions.IntegrationTest` | Real MEF providers, built-in staging, controlled providers, diagnostics, changing/failing Fix All replay and encoding-sensitive behaviour. |
| `Roslyn.Workbench.Mcp.CodeActions.AuditTest` | Version-sensitive built-in provider inventory, compatibility and replay support. |
| `Roslyn.Workbench.Mcp.Test` | Host architecture/governance; configuration; schemas/binding/envelopes; lifecycle tools; all adapters; plugin loading/collisions; status; MSBuild/lifecycle; local error reporting, consent, dispatch and exception filtering. |
| `Roslyn.Workbench.Mcp.IntegrationTest` | Full Host composition; MSBuild lifecycle; plugin discovery/metadata/MEF/load contexts; packaged analyser; MCP cancellation/binding/schema/publication/errors; attribution; containment; Sentry envelope; recovery status. |
| `Roslyn.Workbench.Mcp.AcceptanceTest` | Published Release Host via official MCP client: distribution, startup/lifetime, catalogue/schema, Workspace lifecycle/selection/compatibility/containment, inspection, transactions/durable mutation, Code Actions, plugins, protocol cancellation/concurrency/failures, recovery and state security. |

Abstractions has no dedicated test project; Workspace, Plugins and Host tests exercise its behavioural and schema contracts.

### Shared support and fixtures

| Project | Purpose |
| --- | --- |
| `Roslyn.Workbench.Mcp.TestSupport` | In-memory Roslyn data, selectors, candidates and visible Moq graphs for unit/contract consumers. |
| `Roslyn.Workbench.Mcp.IntegrationTestSupport` | MSBuild registration, immutable temporary assets, real component Workspaces, bundled plugin catalogues, controlled Code Action providers and integration sessions. |
| `Roslyn.Workbench.Mcp.Workspace.LockFixture` | Real cross-process lock holder. |
| `ConsoleOutputPluginFixture` | Stdout-to-stderr protocol isolation across plugin lifecycle/execution. |
| `HostQueryPluginFixture` | Valid queries, plugin DI, private dependency routing, file-signal blocking, exceptions and cache workloads. |
| `HostMutationPluginFixture` | Valid external mutation, cancellation/concurrency blocking and candidate production. |
| `InvalidPluginFixture` | Duplicate configuration and unsupported API metadata. |
| `InvalidToolNamePluginFixture` | Invalid protocol tool-name shapes. |
| `ThrowingPluginFixture` | Exceptions across startup/configuration/materialisation. |
| `UnsupportedSchemaPluginFixture` | Request/response contracts rejected by schema/serializer admission. |

Fixtures are compiled libraries rather than test assemblies. Acceptance references them as packaged runtime assets without consuming their CLR types.

`test/TestAssets` contains inert package consumers, normal and inspection projects, hierarchy, multi-target/linked documents, legacy/malformed/ambiguous/mixed-language compatibility samples. They are materialised by support code and are not solution participants.

## Operational tool project

`Roslyn.Workbench.Mcp.ScenarioRunner` has no production reference. It uses the public MCP client and diagnostics packages to launch a separately published Host and observe process, stdio, filesystem, EventPipe and result files.

Its scenario families cover bounded query determinism/performance; Code Action workflows; server cancellation and lease release; durable-boundary cancellation; durable create/replace/delete; pre-write and application conflicts; crash/startup recovery; concurrent queries and ownership; reload/live-build/watcher/multi-revision state; diagnostic profiling; and cleanup validation. `scenario-suite.json` pins GuardClauses, Serilog and EF Core. The runner uses OS-local exact-commit caches, isolated NuGet caches, recorded restoration and ignored durable result output. It is manual release evidence with no test project.

## Review-stage uncertainties and evidence limits

- Exact MCP SDK merging of DI-registered and dynamic tools is package-owned. Current Host and published-process tests claim the combined catalogue; package source was not inspected in this stage.
- Arbitrary third-party plugin, analyser and MSBuild behaviour is trusted and open-ended.
- Platform filesystem semantics were mapped from implementation and tests but not executed during this stage.
- Sentry transport below explicit client capture is SDK-owned.
- Scenario definitions describe intended evidence; no scenario or test ran during this read-only mapping stage.
- Test descriptions are structurally derived claims, not fresh pass/fail evidence.
