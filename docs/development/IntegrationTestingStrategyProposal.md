# Integration Testing Strategy Proposal

Date: 2026-07-17 Status: Implemented on 2026-07-18; final evidence is recorded in `IntegrationTestingStage8Results-2026-07-18.md`

> The audit and rationale in this document were converted into the standalone, staged `IntegrationTestingImplementationPlan.md`. `TestingStrategy.md` now describes the implemented model; this proposal remains the design rationale and historical baseline.

## Purpose

This document audited the former integration and audit test landscape and proposed its replacement execution model. The proposal is implemented; current policy lives in `TestingStrategy.md` and final verification is recorded in `IntegrationTestingStage8Results-2026-07-18.md`.

The objectives are to:

- prove the boundaries that unit tests cannot prove;
- add genuine stdio MCP acceptance coverage;
- remove integration scenarios that duplicate unit branches;
- make resource ownership and cleanup explicit;
- reduce local and CI feedback time without weakening transaction, plugin, Workspace or Code Action evidence;
- keep test logic in normal .NET test projects while using GitHub Actions only as the orchestrator.

## Executive Decision

Keep xUnit as the test authoring and assertion framework. Do not create a standalone integration-test command-line tool and do not encode test behaviour directly in GitHub Actions.

Use two complementary execution styles:

1. **In-process component integration tests** for a real boundary owned by one production area, such as MSBuild loading, filesystem durability, MEF composition or assembly-load-context routing.
2. **Out-of-process acceptance tests, still authored in xUnit**, which launch the published stdio server through the official MCP C# client and exercise handshake, tool discovery and representative workflows over the real transport.

The current project split is directionally sound and should be evolved rather than discarded. The current harness model is not sound enough to preserve: it rebuilds a parallel application composition root, repeatedly creates expensive projects and workspaces, does not consistently dispose them, and calls MCP tool objects directly while describing the tests as MCP integration.

GitHub Actions should build or publish once, invoke these test projects, collect results and distribute operating-system coverage. It should not become a second test framework.

## Answers to the Design Questions

### Should integration tests run from code or from an external tool?

Both process shapes are required, but all tests should remain code-based xUnit tests:

- component integration tests run in the test process;
- acceptance tests use `StdioClientTransport` to start the real server as a child process;
- GitHub Actions invokes the same projects and commands used locally.

This gives normal discovery, filtering, debugging, cancellation, result reporting and assertions while still proving the executable boundary. A shell or bespoke external runner would duplicate those facilities and make local diagnosis harder.

`WebApplicationFactory` is not the right host here because the product is a stdio MCP server rather than an ASP.NET Core HTTP application. Testcontainers is also unnecessary while the system has no database, broker or other containerised dependency: launching the published local process is both more faithful and cheaper.

### Is the current setup good enough?

The classification and owner-aligned project names are useful. The implementation underneath them is inconsistent and should be substantially reshaped.

The most important distinction is:

- retain the physical `Workspace`, `Plugins.Core`, `CodeActions`, Host and audit test areas;
- replace the broad shared runtime, generated mega-fixture and direct MCP invocation harnesses;
- introduce a separately identifiable acceptance project for the real executable protocol.

### Can it be quicker?

Yes. The largest opportunities are structural rather than runner-related:

- stop recreating and reopening the same immutable sample solution for each test;
- replace the 1,183-line programmatic sample generator with checked-in fixture templates copied to unique temporary directories;
- reuse read-only workspaces at class or collection scope;
- keep mutation tests isolated with cheap per-test copies;
- remove unit-branch duplicates from integration suites;
- split large serial test classes into capability classes so safe class-level parallelism can work;
- dispose every workspace, host, process and service provider deterministically;
- build or publish once in CI, then test with `--no-build --no-restore`;
- move repository source-policy scans out of the expensive Code Action compatibility process.

Changing from VSTest to Microsoft.Testing.Platform may provide a smaller additional gain, but it is not the first optimisation.

## Audit Scope and Method

The audit covered:

- every `*.IntegrationTest` and `*.AuditTest` project;
- `Roslyn.Workbench.Mcp.IntegrationTestSupport` and fixture assemblies;
- integration-like tests currently held in normal unit projects;
- category generation in `test/Directory.Build.targets`;
- `.github/workflows/tests.yml`;
- the current `TestingStrategy.md`, completed reorganisation plan and architecture re-audit;
- warm local execution timings and TRX per-test durations;
- current xUnit, .NET 10, MCP C# SDK, MSBuildLocator and GitHub Actions guidance.

The timing measurements are diagnostic samples from the current WSL environment, not portable performance guarantees.

## Current Landscape

### Projects and measured cost

Warm runs used already-built outputs with restore disabled.

| Area | Tests | Test-run duration | Wall time | Peak resident memory | Current principal boundary |
| --- | --: | --: | --: | --: | --- |
| Workspace integration | 62 | 20 s | 21.58 s | 375 MiB | MSBuild, filesystem, transactions, resolver |
| Plugins.Core integration | 21 | 4 s | 6.00 s | 385 MiB | real Roslyn inspection and mutation |
| CodeActions integration | 11 | 14 s | 16.14 s | 496 MiB | controlled and built-in providers |
| Host integration | 23 | 4 s | 5.60 s | 355 MiB | composition, plugin discovery, direct tool invocation |
| Code Action audit | 95 | 56 s | 57.68 s | 1,189 MiB | built-in compatibility, replay and source-policy scans |

The four normal integration projects take approximately 49 seconds sequentially when warm. In the current CI matrix their critical path is approximately the Workspace run, plus repeated runner setup and restore. The audit is both the slowest and most memory-intensive single process.

A comparison using the xUnit executable directly reduced Workspace wall time from 21.58 to 20.35 seconds and CodeActions from 16.14 to 15.19 seconds. Runner startup accounts for roughly one second in those samples; it is not the main cause of slowness.

### What is already good

- Integration and audit categories are assigned at assembly level.
- Test projects are named and divided by production owner.
- Durable commit, recovery, lock, MSBuild, real Roslyn and MEF scenarios exercise legitimate external boundaries.
- The audit is physically separate from the normal integration suite.
- CI has independent unit, integration and audit gates.
- Purpose-built plugin fixture assemblies faithfully exercise package discovery better than mocks would.

These parts should be retained.

## Findings

### High: Host MCP integration does not cross the MCP transport

`McpIntegrationTestHost` supplies a mocked `McpServer` and invokes constructed `McpServerTool` instances directly. `PluginToolTestHarness` and `CodeActionToolTestHarness` also construct or call individual tools in process.

This is useful component testing, but it does not prove:

- executable startup;
- stdio ownership and lifetime;
- MCP initialisation and capability negotiation;
- `tools/list` publication through the server;
- JSON-RPC framing and serialisation across the transport;
- process termination and stderr diagnostics.

Many failure branches in these classes are now already covered more precisely by Host adapter unit tests. The retained protocol evidence must be replaced by genuine process acceptance tests before the direct harness tests are removed.

### High: Integration support is a second composition root

`Roslyn.Workbench.Mcp.IntegrationTestSupport` contains approximately 2,785 lines and references Host, Workspace, Plugins, Plugins.Core and CodeActions. `WorkspaceCoordinatorFactory` manually constructs a large production service graph and `WorkspaceRuntime` re-exposes it through a test-owned facade.

This creates two risks:

- production dependency changes can be missed by the test composition;
- a green integration test may validate the test graph rather than Host composition.

Full-system workflows should use the real Host in acceptance tests. Owner-level component tests should construct only the component and real boundary collaborators they are intended to prove, not a second full application.

### High: Resource ownership and persistent state are unsafe

`IWorkspaceRuntime` does not implement `IDisposable` or `IAsyncDisposable`, even though it owns loaded workspaces and services with lifetimes. Test cases commonly create a runtime and open a workspace without an explicit close or deterministic teardown.

The default state-directory mapping in `WorkspaceCoordinatorFactory` also points independent runtimes at the same temporary recovery directory. Parallel execution can therefore create cross-test state interference. Loaded workspaces, service providers and MEF objects may remain alive until garbage collection, contributing to runtime and peak memory.

No additional parallelism should be enabled until every stateful fixture has a unique root and deterministic asynchronous cleanup.

### High: Fixture setup is repeated and oversized

`InspectionSampleFixture` is 1,183 lines of programmatic file generation. Across the normal integration projects, fixtures and runtimes are created and workspaces opened dozens of times. `CodeActionToolTestHarness` constructs and validates a new service provider for each invocation.

This mixes test-data definition with lifecycle mechanics, makes the input difficult to inspect, and repeatedly pays project creation, MSBuild evaluation, compilation and composition costs.

Checked-in fixture templates should define input. A small materialiser should copy a template to a unique scenario directory and substitute only values that genuinely vary.

### Medium: Integration suites repeat unit behaviour

Examples include cancellation, absent transaction, snapshot mismatch, malformed request and handler-exception cases that are now covered in focused unit tests. Some integration classes still assert branches rather than boundaries.

Integration retention should be based on the failure mode that only the real boundary can expose, not on production class or tool names.

### Medium: Test-class shape prevents useful parallelism

xUnit does not run tests from the same class in parallel. Large classes therefore create serial islands:

- `WorkspaceCoordinatorIntegrationTests` contains 32 facts;
- `DefaultProjectStructureServiceIntegrationTests` contains 13 facts;
- controlled-provider and compatibility theories keep all rows in one class.

After isolation and cleanup are fixed, these should be split by independent capability. Tests sharing mutable or process-global state must remain in explicit serial collections.

### Medium: Cancellation does not follow the xUnit v3 pattern

There are 152 `CancellationToken.None` usages across integration and audit code. Most should use `TestContext.Current.CancellationToken` so an aborted or timed-out run can stop expensive Roslyn, MSBuild and filesystem work. Deliberate non-cancellable commit/recovery assertions should remain explicit and documented.

### Medium: Some true integrations are held in the Host unit project

The Host inventory records 24 tests that exercise real PE metadata, assembly loading, plugin composition, MSBuild registration or Generic Host lifecycle boundaries. They are valuable but misclassified. They should move to the Host component integration suite without changing their behaviour.

### Medium: Audit ownership is mixed

The built-in provider and replay checks are genuine version-sensitive Code Action compatibility audits. `InternalArgumentNullGuardAuditTests` and `ProductionNullForgivingOperatorAuditTests` scan repository source policy and are not Code Action compatibility checks. Keeping them in this process adds unrelated work and obscures audit timing.

The policy scans should move to a fast architecture/governance test project or build-time analyser when that separate decision is made. The user has previously chosen to retain the tests for now; this proposal changes their category and location, not their rules.

### Low: Current documentation contains stale topology and counts

`TestArchitectureReaudit-2026-07-10.md` and sections of the completed reorganisation plan describe older counts and resolved states as current evidence. They should remain historical records, but the canonical strategy and inventory must be updated after migration.

## Relevant Platform Guidance

### .NET test execution

.NET 10 can select VSTest or Microsoft.Testing.Platform in `global.json`. Microsoft describes MTP as the faster modern path and exposes test-module globbing, module-level parallelism, result directories and a minimum expected test count. Opting in is solution-wide: every selected test project must support MTP. See [dotnet test](https://learn.microsoft.com/dotnet/core/tools/dotnet-test) and [dotnet test with MTP](https://learn.microsoft.com/dotnet/core/tools/dotnet-test-mtp).

The implication for this repository is to benchmark MTP after the structural cleanup, not to couple the redesign to it. The current runner comparison shows that repeated real work dominates startup overhead.

### xUnit fixture lifetime and parallelism

xUnit provides class, collection and assembly fixtures specifically for expensive shared context and deterministic cleanup. xUnit v3 also supports `IAsyncDisposable`. Assembly fixtures do not themselves change parallelisation and therefore must be thread-safe. See [sharing context between tests](https://xunit.net/docs/shared-context) and [running tests in parallel](https://xunit.net/docs/running-tests-in-parallel).

The implication is to share immutable, read-only state at the narrowest useful scope and keep mutation state per test. Global parallelisation should not be disabled merely to hide unsafe fixtures.

### MCP process testing

The official MCP C# SDK provides `StdioClientTransport`, which starts a child process and owns its stdin, stdout and lifecycle. `McpClient.CreateAsync`, tool listing and tool calls provide the supported client path. See the MCP C# SDK [transport guidance](https://csharp.sdk.modelcontextprotocol.io/concepts/transports/transports.html) and [getting-started client example](https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html).

That is the appropriate acceptance boundary for this stdio server. A custom protocol harness is unnecessary.

### CI orchestration

GitHub's .NET guidance uses `setup-dotnet`, normal `dotnet` build/test commands and uploaded test results. The setup action can cache NuGet packages when dependency lock files are present. See [Building and testing .NET](https://docs.github.com/en/actions/tutorials/build-and-test-code/net) and [actions/setup-dotnet](https://github.com/actions/setup-dotnet).

This repository currently has no NuGet lock files, so cache enablement should be a separate package-management decision rather than an integration-test shortcut.

### MSBuild registration

MSBuildLocator must be registered before MSBuild assemblies are loaded. The current module initialisers achieve the required early timing, although the behaviour is hidden. Retain one small, documented early-registration mechanism until a proven alternative can run before all test discovery and fixture construction. See [Find and use a version of MSBuild](https://learn.microsoft.com/visualstudio/msbuild/find-and-use-msbuild-versions).

## Target Test Model

```text
Unit and contract tests
    isolated production behaviour and protocol shape

Component integration tests (in process, xUnit)
    Workspace     -> MSBuild, filesystem, locking and real Roslyn
    Plugins.Core  -> representative real-solution capabilities
    CodeActions   -> MEF catalogue and controlled provider workflows
    Host          -> DI, PE metadata, plugin packages and load contexts

Process acceptance tests (out of process, xUnit)
    official MCP client -> stdio -> published Host -> real fixture workspace

Compatibility audit (separate, xUnit)
    supported built-in Roslyn providers and replay families
```

### Layer rules

| Layer | Production references | Mocks | Filesystem/MSBuild | Process boundary | Branch-coverage owner |
| --- | --- | --- | --- | --- | --- |
| Unit/contract | owning assembly | expected | only in-memory abstractions | no | yes |
| Component integration | owning area and necessary dependencies | only outside the boundary under proof | real where relevant | normally no | no |
| Acceptance | no direct production project reference | no | real fixture copy | yes, official MCP client | no |
| Audit | CodeActions plus controlled audit support | no provider mocks | real workspace as required | no | no |

The acceptance project should consume the published executable and public MCP JSON only. It must not use `InternalsVisibleTo`, Moq, Host service interfaces or direct tool classes. This is what makes it independent evidence rather than another component test.

## Proposed Project Layout

| Project | Disposition | Target responsibility |
| --- | --- | --- |
| `Roslyn.Workbench.Mcp.Workspace.IntegrationTest` | retain and narrow | real MSBuild loading, input change detection, resolution, atomic I/O, commit, recovery and inter-process lock behaviour |
| `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` | retain and narrow | a compact matrix of real-solution projection, semantic search and ordinary mutation capabilities |
| `Roslyn.Workbench.Mcp.CodeActions.IntegrationTest` | retain and narrow | MEF catalogue, controlled provider list/describe/stage/fix-all and one representative bundled action |
| `Roslyn.Workbench.Mcp.IntegrationTest` | retain as Host component integration | Host DI, options fallback, plugin package discovery, PE metadata, MEF, load contexts and SDK schema generation |
| `Roslyn.Workbench.Mcp.AcceptanceTest` | add | published executable, stdio handshake, tool listing and representative query/mutation/Code Action/lifecycle workflows |
| `Roslyn.Workbench.Mcp.CodeActions.AuditTest` | retain and narrow | version-sensitive built-in provider visibility and replay compatibility only |
| `Roslyn.Workbench.Mcp.IntegrationTestSupport` | replace and sharply narrow | fixture-template materialisation, process-safe temporary roots, early MSBuild registration and controlled providers only |

Host component integration may reference production projects. Acceptance must not.

## Scenario Retention and Migration

### Workspace

| Current area | Decision | Required evidence after migration |
| --- | --- | --- |
| `AtomicFileWriterIntegrationTests` | keep | real atomic replace/create behaviour and exact bytes on supported operating systems |
| `DurableWorkspaceCommitIntegrationTests` | keep and split by capability | multi-file success, deterministic restoration, divergence, restart recovery, directory cleanup and real inter-process lock release |
| project compatibility | keep representative cases | SDK project, supported solution formats and evaluated imported build inputs |
| change detector | keep external-boundary cases | real file and imported-input changes; remove pure comparison branches covered by units |
| coordinator | decompose | retain real open/close, multiple workspaces, external change and durable transaction workflows; remove rejection/cancellation branches covered by units |
| resolver | narrow | retain real source/metadata, cross-project, ambiguity and documentation-ID behaviour; remove snapshot/status branches covered by units |

### Plugins.Core

| Current area | Decision | Required evidence after migration |
| --- | --- | --- |
| default project structure | consolidate | retain solution/project format and imported-property cases; move malformed/missing/cancellation branches to units |
| workspace projection | keep compactly | one real solution/project/document projection flow |
| semantic inspection | keep compactly | diagnostics plus representative operation/control-flow behaviour against a real compilation |
| solution search | keep representative families | cross-project references/implementations and one dependency graph; avoid a per-tool matrix |
| selector and snapshot | narrow | real ambiguous and cross-project selector behaviour only |
| mutation pipeline | keep | representative rename plus formatting/sort mutation through neutral staging |

Read-only Plugins.Core scenarios should share one immutable opened fixture at collection scope if the Workspace APIs are safe for concurrent queries. Mutation scenarios use isolated copies.

### CodeActions

| Current area | Decision | Required evidence after migration |
| --- | --- | --- |
| MEF provider composition | keep and share | one composition per fixture lifetime with deterministic disposal |
| controlled provider workflow | consolidate | list/describe/stage and fix-all through real Roslyn services; token tamper/expiry/staleness remain unit tests |
| built-in staging | keep one representative | proof that a supported bundled provider is discoverable and its proposal reaches Workspace staging |

Split independent provider families into separate classes after fixture isolation. Do not run multiple mutation cases concurrently against the same workspace.

### Host

| Current area | Decision | Required evidence after migration |
| --- | --- | --- |
| Host composition | keep | real container validation and complete tool catalogue |
| plugin discovery and MCP tool | retain discovery, remove direct MCP duplication | package enumeration, metadata, MEF and closed-generic materialisation; protocol execution moves to acceptance |
| representative MCP tool | replace | genuine query and Code Action acceptance over stdio |
| workspace lifecycle MCP | replace | genuine lifecycle and transaction acceptance over stdio |
| server-status recovery | split | persistence-to-service remains component integration; JSON publication is covered in acceptance/contract tests |
| SDK schema provider | keep | real SDK exporter compatibility |
| boundary tests in Host unit project | move | 24 PE metadata, ALC, MEF, MSBuild and Generic Host lifecycle cases become component integration tests |

### Audit and governance

- retain supported built-in provider compatibility and replay-family cases;
- move repository source-policy scans to fast governance coverage;
- share an immutable provider composition and template fixture where safe;
- split provider families into independently schedulable classes or CI shards;
- cap audit parallelism based on measured memory rather than allowing unbounded Roslyn compilations.

## Acceptance Suite Design

Add `Roslyn.Workbench.Mcp.AcceptanceTest` with a process fixture that:

1. receives the already-published Host executable path;
2. creates a unique temporary workspace and state root;
3. copies a checked-in fixture template;
4. starts the server through `StdioClientTransport`;
5. creates an `McpClient` and completes initialisation;
6. captures stderr and process exit information for failure diagnostics;
7. disposes the client and transport, terminates a hung child if necessary, and removes temporary state.

Initial workflows should be few and broad:

1. **Startup and catalogue:** handshake, `tools/list`, stable names and schemas, `server-status`, Plugins.Core present, CodeActions absent from plugin status.
2. **Workspace query:** open a fixture, query status, invoke one bundled inspection, close the workspace.
3. **Transactional mutation:** open, start transaction, invoke a plugin mutation, preview, commit, verify exact on-disk bytes, then perform a follow-up query.
4. **Code Action mutation:** list actions from a deterministic input, stage one, preview and roll back or commit.
5. **Startup diagnostics/recovery:** invalid configured options fall back visibly; one prepared/incomplete recovery state is surfaced after restart.

Do not create one acceptance test per MCP tool. Unit and component suites own those details.

External plugin package execution can either be part of startup/catalogue acceptance or a separate workflow using a fixture package directory. Private-dependency version routing remains Host component integration because its primary boundary is `AssemblyLoadContext`, not stdio.

## Fixture and Lifetime Design

### Fixture assets

Replace generated mega-fixtures with directories under a test-assets root, for example:

```text
test/TestAssets/
    Workspaces/
        InspectionSample/
        TransactionSample/
        CodeActionSample/
    PluginPackages/
        ValidQuery/
        ValidMutation/
        Invalid/
```

Projects used only as compiled plugin fixtures may remain projects. Source workspaces should be readable checked-in assets. The materialiser copies assets and performs narrowly defined token substitution; it does not contain source files as large C# strings.

### Lifetime rules

- Every stateful fixture implements `IAsyncDisposable`.
- Every test scenario receives a unique workspace root and state directory.
- Read-only fixtures may be class or collection fixtures.
- Mutable workspaces are never shared between concurrently running tests.
- Every loaded workspace is closed before fixture deletion.
- Service providers and MEF containers are disposed.
- Child processes have bounded startup and shutdown and always expose captured stderr on failure.
- Test operations use `TestContext.Current.CancellationToken` unless the scenario deliberately verifies a non-cancellable protocol phase.

### Parallelism rules

- Keep xUnit assembly parallelism enabled.
- Split large classes by capability only after isolation is complete.
- Use explicit non-parallel collections for shared mutable workspaces and process-global registration.
- Limit Code Action audit concurrency to a measured safe value because current peak memory exceeds 1 GiB.
- Do not serialize the entire repository to compensate for fixture defects.

## Performance Plan

Optimise in this order and remeasure after every phase:

1. deterministic cleanup and unique state directories;
2. checked-in templates instead of repeated generated projects;
3. remove integration scenarios duplicated by unit tests;
4. reuse immutable opened workspaces and composed catalogues at class/collection scope;
5. split serial monoliths for safe parallel execution;
6. build/publish once and use `--no-build --no-restore`;
7. benchmark MTP against VSTest on the resulting suite;
8. consider package caching only with an approved lock-file policy.

Record the median of at least three warm local runs and CI job duration. The initial improvement target is a 40% reduction in normal component-integration wall time from the approximately 49-second sequential baseline, with no loss of retained boundary scenarios. Treat that as an engineering objective, not a brittle per-run test assertion.

The acceptance suite should remain small enough for every pull request. If it grows beyond its purpose, consolidate workflows rather than sharding a per-tool matrix.

## Test Runner Recommendation

Keep the current xUnit v3/VSTest path during fixture and scenario migration.

After the suite is stable:

1. create a short-lived branch or explicit benchmark configuration selecting MTP in `global.json`;
2. verify every test project, category filter, coverage collector, IDE workflow and CI result reporter;
3. compare median clean and warm timings;
4. adopt MTP only if the complete toolchain works and the improvement is material.

MTP's `--test-modules`, module-level parallelism and minimum expected test count are attractive for the final CI shape. A global runner switch should not be mixed into the fixture redesign because it makes failures harder to attribute.

## Proposed CI Shape

### Pull requests

1. **Build:** restore once, build and publish the Host once.
2. **Unit and contract:** run the fast category filter with no build or restore.
3. **Component integration:** run owner projects from built outputs. Start with an Ubuntu matrix; consolidate into fewer jobs only if measured end-to-end CI time improves.
4. **Acceptance:** run on Ubuntu and Windows using the published Host and official MCP client.
5. **Audit:** run when CodeActions, Roslyn dependencies, compatibility data or audit infrastructure changes; retain a scheduled full audit to avoid path-filter blind spots.

### Main and schedule

- run all component and acceptance suites on Ubuntu and Windows;
- add macOS on a schedule until there is evidence that it should become a pull-request gate;
- run the full Code Action audit;
- retain real inter-process lock, atomic file and recovery cases on every supported operating system.

### Diagnostics

- always write and upload TRX or the MTP equivalent;
- upload captured server stderr, diagnostic output and failed fixture state;
- use a hang-detection timeout appropriate to Roslyn/MSBuild rather than an unbounded job;
- report a minimum expected test count so a filtering mistake cannot produce a green empty run;
- keep temporary recovery artifacts only on failure.

CI action-version and NuGet cache updates should be made during workflow implementation using the then-current official versions and an explicit lock-file decision.

## Implementation Plan

### Phase 0: Correctness and baseline

- [x] Give every runtime and fixture a unique workspace and recovery root.
- [x] Make all stateful test fixtures asynchronously disposable and close loaded workspaces deterministically.
- [x] Replace accidental `CancellationToken.None` usages.
- [x] Add failure diagnostics for undeleted roots, live processes and stderr.
- [x] Capture three-run timing and memory baselines by project and slowest test.

This phase should not increase parallelism.

### Phase 1: Introduce real acceptance coverage

- [x] Add `Roslyn.Workbench.Mcp.AcceptanceTest` without production project references.
- [x] Publish the Host as an explicit test prerequisite.
- [x] Implement the stdio process fixture with the official MCP client.
- [x] Add startup/catalogue and workspace-query workflows.
- [x] Add transaction and Code Action workflows.
- [x] Add restart/recovery coverage and robust process diagnostics.

Keep the old direct Host integration cases until equivalent acceptance evidence is green.

### Phase 2: Replace fixture infrastructure

- [x] Extract checked-in workspace and plugin-package assets.
- [x] Replace `InspectionSampleFixture` with a small asset materialiser.
- [x] Remove the test-owned full composition root.
- [x] Make owner integration tests construct only the boundary under proof.
- [x] Use Host composition or process acceptance for full-system workflows.
- [x] Narrow `IntegrationTestSupport` to asset, lifetime, MSBuild and controlled-provider responsibilities.

If removing the parallel composition root reveals a need for a new production DI-registration seam, stop and request approval before changing production code. The preferred first approach is to move full-system evidence to Host/acceptance rather than add a test-driven production abstraction.

### Phase 3: Reclassify and consolidate scenarios

- [x] Apply the retention tables in this document test by test.
- [x] Move the 24 Host boundary tests from the unit project.
- [x] Remove direct MCP harness cases only after acceptance replacements pass.
- [x] Move source-policy scans out of the Code Action compatibility audit.
- [x] Split large serial classes by capability.
- [x] Add explicit serial collections only where state genuinely requires them.

### Phase 4: Optimise execution and CI

- [x] Reuse immutable read-only fixtures and catalogues.
- [x] Build/publish once per workflow and test without build/restore.
- [x] Add Windows acceptance and durability coverage.
- [x] Add result, stderr and failed-fixture artifacts.
- [x] Add hang diagnostics and minimum-test-count protection.
- [x] Benchmark matrix versus consolidated jobs.
- [x] Benchmark MTP and make a separate adoption decision.
- [x] Evaluate NuGet lock files and caching separately.

### Phase 5: Final audit and documentation

- [x] Re-audit every integration case against a named external boundary.
- [x] Verify acceptance has no production references, mocks or internal access.
- [x] Verify every fixture is isolated and deterministically disposed.
- [x] Record final timings, memory and retained scenario counts.
- [x] Update `TestingStrategy.md` as the canonical policy.
- [x] Supersede stale topology/count statements in the prior re-audit while retaining historical plans.
- [x] Update contributor commands and CI documentation.

## Completion Criteria

The redesign is complete when:

- at least one test proves the published executable over real stdio using the official MCP client;
- no test described as MCP acceptance invokes a tool object directly;
- every integration and audit fixture has deterministic cleanup and an isolated state root;
- the shared integration-support project no longer composes the whole product;
- every retained integration test names the real boundary and failure mode it protects;
- branch variants already covered by unit tests are absent from integration unless the external boundary changes their meaning;
- transaction success, restoration, recovery, divergence and inter-process locking have real-filesystem evidence on supported operating systems;
- plugin package discovery and dependency routing retain real assembly evidence;
- Code Action integration retains controlled-provider and representative bundled-provider evidence;
- CI collects actionable results and process diagnostics and cannot silently run zero selected tests;
- normal component-integration wall time is materially lower than the recorded baseline;
- the Code Action audit is compatibility-focused and has a measured memory-safe parallelism policy.

## Implementation Decisions

- VSTest remains selected; MTP v2 is the intended direction after xUnit 4 is stable.
- CI uses a four-owner component matrix.
- Collection-scoped concurrent Workspace reuse remains deferred pending thread-safety evidence.
- NuGet lock files and package caching remain a separate dependency-policy decision.
- macOS acceptance is scheduled but remains non-gating pending reliability evidence.
- No new production composition seam was needed.
