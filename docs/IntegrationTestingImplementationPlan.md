# Integration Testing Redesign Implementation Plan

Date: 2026-07-17
Status: Ready for implementation
Source audit: `IntegrationTestingStrategyProposal.md`

## Purpose

This is the executable hand-off plan for redesigning the repository's integration, acceptance and compatibility-audit tests. It contains the context, decisions, sequencing and acceptance criteria needed by an agent that has not participated in the preceding architecture and testing work.

The plan replaces the execution model described by the completed `IntegrationTestReorganisationPlan.md`. Until this plan is complete, `TestingStrategy.md` remains the canonical policy for the existing suite and this document governs the redesign work.

## Mandatory Repository Instructions

Before changing anything, the implementing agent must:

- [x] Read the root `AGENTS.md`.
- [x] Read `test/AGENTS.md` before changing tests, fixtures or test projects.
- [x] Read `src/AGENTS.md` before any production-code change.
- [x] Read `TestingStrategy.md` and `IntegrationTestingStrategyProposal.md`.
- [x] Verify that the .NET SDK pinned by `global.json` is installed.
- [x] Preserve CRLF in governed files and run `unix2dos` on every changed governed file.
- [x] Add `--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp` to every `dotnet` command, including formatting.
- [x] Use `TestContext.Current.CancellationToken` for test work unless a scenario deliberately verifies a non-cancellable production phase.
- [x] Avoid git write operations unless the user separately authorises them.

All production changes require explicit user confirmation before implementation during this testing programme. If a stage reveals that production composition, lifetime or API changes would improve the tests, record the proposed change and stop for approval rather than adding a test-only production seam.

## Background and Agreed Direction

Roslyn Workbench is a local stdio MCP server. The production dependency direction is:

```text
Host -> CodeActions -> Workspace
Host -> Plugins.Core -> Plugins -> Workspace
Host -> Plugins -> Workspace
Host -> Workspace
```

Code Actions are internal and are not plugins. Host alone owns MCP transport composition. Workspace integration must continue to prove durable local multi-file transactions, recovery and real filesystem/locking behaviour.

The agreed testing direction is:

1. Keep xUnit as the authoring, assertion, discovery and reporting framework.
2. Keep owner-aligned in-process component integration projects.
3. Add out-of-process xUnit acceptance tests that launch the published Host through the official MCP C# client's `StdioClientTransport`.
4. Use GitHub Actions only to orchestrate build, publish, test and diagnostic collection.
5. Do not create a bespoke integration-test CLI, shell test framework or GitHub-Actions-only workflow.
6. Do not use `WebApplicationFactory`; the product is not an HTTP application.
7. Do not introduce Testcontainers while there is no external database, broker or containerised service to provision.
8. Do not switch test runners as part of the structural redesign. Benchmark Microsoft.Testing.Platform after the suite is stable.

## Objectives

- [ ] Every integration test names a real component or infrastructure boundary that a unit test cannot faithfully prove.
- [ ] Genuine acceptance tests cover the published executable over stdio and JSON-RPC.
- [ ] Integration suites no longer duplicate ordinary unit branches.
- [ ] Test-owned infrastructure does not reproduce the entire production composition root.
- [ ] Every workspace, service provider, MEF container, client and child process has deterministic lifetime management.
- [ ] Every stateful scenario has an isolated workspace and recovery root.
- [ ] Read-only expensive fixtures are reused safely; mutable fixtures remain isolated.
- [ ] CI provides Windows and Linux evidence for platform-sensitive durability and acceptance behaviour.
- [ ] Failures retain actionable TRX, stderr and failed-fixture diagnostics.
- [ ] Normal integration feedback becomes materially faster without sacrificing retained boundary scenarios.

## Non-goals

- Rewriting unit tests or changing their coverage policy.
- Adding a per-tool acceptance matrix.
- Changing MCP request or response contracts.
- Changing production architecture merely to make tests convenient.
- Moving integration tests to a separate repository.
- Containerising the stdio server.
- Introducing NuGet lock files solely to enable caching.
- Switching to Microsoft.Testing.Platform before the structural work is measured.
- Replacing the existing source-policy tests with analysers in this plan.

## Current Measured Baseline

The following warm runs used already-built output with restore disabled in WSL. They are comparison evidence, not portable performance assertions.

| Area | Tests | Test duration | Wall time | Peak resident memory |
| --- | ---: | ---: | ---: | ---: |
| Workspace integration | 62 | 20 s | 21.58 s | 375 MiB |
| Plugins.Core integration | 21 | 4 s | 6.00 s | 385 MiB |
| CodeActions integration | 11 | 14 s | 16.14 s | 496 MiB |
| Host integration | 23 | 4 s | 5.60 s | 355 MiB |
| Code Action audit | 95 | 56 s | 57.68 s | 1,189 MiB |

The four normal integration projects take approximately 49 seconds sequentially. Direct xUnit executable runs saved only about one second for sampled Workspace and CodeActions projects, so runner startup is not the main bottleneck.

The agent implementing Stage 0 must replace this one-off sample with a reproducible baseline of three runs and record the median.

The completed pre-change measurements are recorded in `IntegrationTestingBaseline-2026-07-17.md`.

## Current Problems to Preserve in the Handoff

### Transport evidence is missing

`test/Roslyn.Workbench.Mcp.IntegrationTest/McpIntegrationTestHost.cs` supplies a mocked `McpServer` and invokes `McpServerTool` objects directly. The Plugin and Code Action harnesses also invoke constructed tools in process. These tests do not prove:

- executable startup and shutdown;
- MCP initialisation or capability negotiation;
- stdio framing and process ownership;
- `tools/list` publication from the running server;
- JSON-RPC serialisation across the transport;
- stderr and exit diagnostics.

Do not remove the current direct tests until equivalent real-transport acceptance coverage passes.

### Shared integration support is a second composition root

`test/Roslyn.Workbench.Mcp.IntegrationTestSupport` is approximately 2,785 lines and references Host, Workspace, Plugins, Plugins.Core and CodeActions. In particular:

- `WorkspaceCoordinatorFactory.cs` manually reconstructs a large production service graph;
- `WorkspaceRuntime.cs` exposes it through a test-owned facade;
- `InspectionSampleFixture.cs` contains 1,183 lines of programmatic source/project generation;
- `CodeActionToolTestHarness.cs` builds and validates a service provider for each invocation;
- `PluginToolTestHarness.cs` constructs tools and deserialises protocol results itself.

Full-system behaviour must move to Host composition or process acceptance. Owner component tests should create only the component and real boundary they are proving.

### Lifetime and isolation are unsafe

- `IWorkspaceRuntime` is not disposable.
- Tests commonly open workspaces without explicitly closing them.
- The default state-directory mapping can point independent runtimes at the same temporary recovery directory.
- Service providers, MEF objects and Roslyn workspaces can remain alive until process teardown.
- Enabling more parallelism before correcting these problems can create interference and flakiness.

### Expensive setup is repeated

Across current integration and audit projects there are dozens of fixture creations, Workspace opens and coordinator constructions. There are no xUnit class, collection or assembly fixtures. Large classes form serial islands because xUnit never runs tests within one class concurrently.

Notable examples:

- `WorkspaceCoordinatorIntegrationTests`: 32 facts;
- `DefaultProjectStructureServiceIntegrationTests`: 13 facts;
- controlled-provider and compatibility theories place all rows in single classes.

### Classification still contains inconsistencies

- Twenty-four genuine Host boundary tests remain in `Roslyn.Workbench.Mcp.Test`; they cover PE metadata, assembly loading, MEF, MSBuild registration and Generic Host lifetime.
- `InternalArgumentNullGuardAuditTests` and `ProductionNullForgivingOperatorAuditTests` are source-governance checks, not Code Action compatibility audits.
- Some cancellation, invalid-request, absent-transaction, snapshot-mismatch and handler-failure integration cases repeat unit coverage.

## Target Test Architecture

```text
Unit and contract tests
    isolated behaviour, public shape and protocol shape

Component integration tests (in-process xUnit)
    Workspace     -> MSBuild, filesystem, locking, recovery and real Roslyn
    Plugins.Core  -> representative real-solution inspection and mutation
    CodeActions   -> MEF catalogue and controlled-provider workflows
    Host          -> DI, options, PE metadata, plugin packages and load contexts

Process acceptance tests (out-of-process xUnit)
    official MCP client -> stdio -> published Host -> fixture workspace

Compatibility audit (separate xUnit project)
    supported built-in Roslyn providers and replay families
```

### Layer constraints

| Layer | Production project references | Mocks | Real filesystem/MSBuild | Child process | Owns branch coverage |
| --- | --- | --- | --- | --- | --- |
| Unit/contract | owning assembly | expected | no, except abstractions/in-memory Roslyn data | no | yes |
| Component integration | owner and required dependencies | only outside the boundary under proof | where relevant | normally no | no |
| Acceptance | none | none | fixture copy | yes | no |
| Audit | CodeActions and controlled audit support | no provider mocks | where required | no | no |

`Roslyn.Workbench.Mcp.AcceptanceTest` must not reference a production project, use `InternalsVisibleTo`, use Moq or invoke a production tool class. It consumes the published executable and public MCP JSON through the official MCP client package.

## Target Project Disposition

| Project | Action | Final responsibility |
| --- | --- | --- |
| `Roslyn.Workbench.Mcp.Workspace.IntegrationTest` | retain and narrow | MSBuild, filesystem, atomic I/O, resolver boundaries, transaction durability, recovery and inter-process locking |
| `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` | retain and narrow | compact real-solution projection, semantic/search and ordinary mutation evidence |
| `Roslyn.Workbench.Mcp.CodeActions.IntegrationTest` | retain and narrow | MEF catalogue, controlled provider workflow, fix-all and one bundled-provider stage |
| `Roslyn.Workbench.Mcp.IntegrationTest` | retain as Host component integration | DI, options fallback, schema exporter, plugin packages, PE metadata, MEF and load contexts |
| `Roslyn.Workbench.Mcp.AcceptanceTest` | add | published executable, stdio and representative public workflows |
| `Roslyn.Workbench.Mcp.CodeActions.AuditTest` | retain and narrow | built-in provider visibility and replay compatibility only |
| `Roslyn.Workbench.Mcp.IntegrationTestSupport` | replace and narrow | asset materialisation, isolated temporary roots, early MSBuild registration and controlled providers |

## Stage Dependencies

```text
Stage 0: safety and baseline
    -> Stage 1: asset model
    -> Stage 2: acceptance foundation
        -> Stage 3: acceptance workflows
            -> Stage 4: component-suite migration
                -> Stage 5: remove obsolete harnesses
                    -> Stage 6: audit/governance separation
                        -> Stage 7: performance and CI
                            -> Stage 8: final audit and documentation
```

Stages may be split into smaller turns, but their exit criteria must be met in order. In particular, obsolete direct-MCP tests and harnesses cannot be removed before Stage 3 establishes replacement transport evidence.

## Stage 0: Make the Existing Suite Safe and Establish the Baseline

Completion evidence: `IntegrationTestingStage0Evidence-2026-07-17.md`.

### Goal

Correct lifetime, state-isolation and cancellation defects before increasing reuse or concurrency. Produce reproducible evidence against which later improvements can be measured.

### Work

- [x] Inventory every creation of `IWorkspaceRuntime`, `WorkspaceRuntime`, `WorkspaceCoordinatorFactory`, `TestWorkspaceFixture`, `InspectionSampleFixture`, `ServiceProvider`, MEF container and child `Process` in integration/audit projects.
- [x] Give each independent scenario a unique workspace root and recovery/state directory.
- [x] Remove the shared default recovery path from test composition.
- [x] Make state-owning test fixtures implement `IAsyncDisposable`.
- [x] Ensure disposal closes loaded workspaces before deleting fixture directories.
- [x] Dispose all service providers and MEF containers deterministically.
- [x] Ensure existing child processes have bounded shutdown and forced cleanup on failure.
- [x] Replace accidental `CancellationToken.None` usages with `TestContext.Current.CancellationToken`.
- [x] Retain intentional non-cancellable calls only for production phases whose contract ignores caller cancellation, and add a short explanatory comment where intent is otherwise unclear.
- [x] Fail clearly when teardown cannot delete a fixture because a file, workspace or process remains live.
- [x] Capture TRX and per-test duration for three warm runs of each current integration/audit project.
- [x] Record median wall time and peak resident memory in this document or a dated evidence appendix.
- [x] Record the ten slowest test cases in each project.

### Constraints

- Do not enable additional parallelism in this stage.
- Prefer test-only lifetime fixes.
- If the runtime cannot be disposed without changing production ownership, document the required production change and ask for approval.

### Verification

- [x] All affected integration and audit projects pass independently.
- [x] A repeated run leaves no live Host/fixture process.
- [x] A repeated run leaves no locked fixture directory.
- [x] Concurrently-created test runtimes demonstrably use different state directories.
- [x] Changed tests use the xUnit cancellation token except for documented intentional cases.
- [x] Baseline evidence is committed to the plan/documentation.

### Exit criteria

- [x] Every current stateful fixture has explicit ownership and teardown.
- [x] No test runtime silently shares recovery state.
- [x] The measured baseline is reproducible.

## Stage 1: Replace Generated Mega-fixtures with Checked-in Assets

### Goal

Make test input readable, reusable and cheap to materialise while preserving exact project shapes.

### Target asset layout

```text
test/TestAssets/
    Workspaces/
        InspectionSample/
        TransactionSample/
        CodeActionSample/
        CompatibilitySamples/
    PluginPackages/
        ValidQuery/
        ValidMutation/
        Invalid/
```

The exact number of fixture directories should be driven by materially different project shapes, not by individual test names.

### Work

- [x] Catalogue every file and option currently generated by `InspectionSampleFixture` and `TestWorkspaceFixture`.
- [x] Group generated inputs into the smallest set of stable fixture templates.
- [x] Add readable checked-in `.sln`, `.slnx`, `.csproj`, source, configuration and imported-build files under `test/TestAssets`.
- [x] Preserve intentionally malformed inputs as explicit assets with descriptive directory names.
- [x] Add a small asset materialiser that copies a template into a unique temporary scenario root.
- [x] Allow token substitution only for values that must vary, such as absolute paths or unique assembly names.
- [x] Keep source code out of large C# string literals.
- [x] Ensure materialisation excludes `bin`, `obj`, `.vs` and prior recovery state.
- [x] Preserve exact encodings and binary fixtures where the scenario asserts bytes.
- [x] Migrate one read-only Workspace scenario as the pattern check.
- [x] Migrate one mutable transaction or Code Action scenario as the isolation pattern check.
- [x] After both patterns are approved, migrate remaining fixture consumers in batches by project.
- [x] Delete superseded generation code only after all consumers have moved.

### Fixture lifetime policy

- [x] Keep read-only template copies isolated for now; class/collection reuse was unnecessary, so no unproven concurrent sharing was introduced.
- [x] Mutation, external-change, recovery and locking scenarios always receive isolated copies.
- [x] No shared fixture may be mutated by cleanup, generated-code output or Workspace operations.
- [x] Every materialised fixture exposes its workspace root, state root and asynchronous cleanup through one owner object.

### Verification

- [x] Asset files are understandable without reading fixture-generation code.
- [x] Existing retained scenarios pass against assets.
- [x] Byte/encoding-sensitive tests still compare exact bytes.
- [x] Running the same project repeatedly produces no modified checked-in asset.
- [x] No asset contains build output or machine-specific paths.

### Exit criteria

- [x] `InspectionSampleFixture` no longer embeds the sample project as large C# strings.
- [x] All active Workspace templates are checked in and copied through one narrow materialiser.

## Stage 2: Add the Out-of-process Acceptance Foundation

### Goal

Create independent evidence that the published executable starts and speaks MCP correctly over stdio.

### Project creation

- [x] Add `test/Roslyn.Workbench.Mcp.AcceptanceTest/Roslyn.Workbench.Mcp.AcceptanceTest.csproj` to the solution.
- [x] Extend `test/Directory.Build.targets` so an `*.AcceptanceTest` project has the assembly-level `Category=Integration` trait and is rejected if it declares a different category.
- [x] Update the category-validation error text to recognise Unit/Contract, IntegrationTest, AcceptanceTest and AuditTest project suffixes accurately.
- [x] Prove that the existing `Category!=Integration&Category!=Audit` fast-loop filter selects no acceptance tests.
- [x] Reference xUnit v3, its runner, the MCP C# client package and the standard assertion package.
- [x] Do not add any production `ProjectReference`.
- [x] Do not reference `Roslyn.Workbench.Mcp.IntegrationTestSupport` if doing so would pull in production references.
- [x] If asset materialisation must be shared, extract a production-independent test-assets support project or link the minimal test-only source.

### Published-host prerequisite

- [x] Define one supported way to pass the published Host executable path to acceptance tests.
- [x] Prefer an environment variable set by the build/test command rather than locating arbitrary `bin` output.
- [x] Fail with an actionable message when the executable is absent.
- [x] Add a documented local publish-and-test command using the required artifacts path.
- [x] Keep Debug and Release paths explicit; do not guess silently.

### Process fixture

- [x] Implement a fixture that creates a unique workspace and state root.
- [x] Launch the published Host with the official `StdioClientTransport`.
- [x] Create and initialise an `McpClient` through the official SDK API.
- [x] Capture stderr without contaminating stdout protocol traffic.
- [x] Bound startup, invocation and shutdown with suitable timeouts.
- [x] Use `TestContext.Current.CancellationToken` for setup and calls.
- [x] Dispose client and transport asynchronously.
- [x] Kill a child process only when graceful teardown fails.
- [x] Include command, exit code and captured stderr in failure diagnostics without exposing secrets.
- [x] Retain the failed fixture root when requested by a diagnostic environment variable; otherwise clean it.

### First pattern tests

- [x] Start the server and complete MCP initialisation.
- [x] Call `tools/list` and assert representative server-owned, Plugin and Code Action tools are published.
- [x] Call `server-status` through MCP and assert its structured JSON contract.
- [x] Assert Plugins.Core is represented as a plugin and CodeActions is not reported as a plugin.
- [x] Dispose the client and prove the child process exits.

### Verification

- [x] The project passes locally against a published Host.
- [x] Removing or corrupting the executable produces a diagnostic setup failure rather than a hang.
- [x] A deliberately broken startup captures stderr.
- [x] The project has no production project reference, mocks or internal access.
- [x] No test calls `McpServerTool` directly.

### Exit criteria

- [x] At least one passing test proves initialisation, `tools/list` and `server-status` across real stdio.
- [x] Process teardown is deterministic on Linux and Windows.

## Stage 3: Add Representative Acceptance Workflows

### Goal

Prove the principal public workflows without creating a per-tool end-to-end suite.

### Workflow A: Workspace lifecycle and query

- [x] Start a fresh server and open a copied fixture workspace.
- [x] Query workspace list/status over MCP.
- [x] Invoke one bundled inspection tool against the loaded workspace.
- [x] Validate structured response JSON and a representative semantic result.
- [x] Close the workspace and verify the public status changes.

### Workflow B: Transactional plugin mutation

- [x] Open an isolated fixture.
- [x] Start a transaction.
- [x] Invoke one bundled plugin mutation.
- [x] Preview the staged changes.
- [x] Commit the transaction.
- [x] Verify exact expected bytes on disk.
- [x] Execute a follow-up MCP query to prove the in-memory Workspace session was promoted correctly.

### Workflow C: Code Action mutation

- [x] Load deterministic source that offers a supported Code Action.
- [x] List Code Actions over MCP.
- [x] Stage one action using its supported identity/token flow.
- [x] Preview the proposed change.
- [x] Roll back or commit and verify the public transaction result.
- [x] Assert Code Action tools remain separate from plugin status.

### Workflow D: Startup diagnostics and recovery

- [x] Start with invalid configured options and verify defaults are used.
- [x] Assert the configuration diagnostic is surfaced through the public status/tool path.
- [x] Prepare one supported interrupted-recovery fixture.
- [x] Restart the server and verify recovery status through MCP.
- [x] Confirm the fixture reaches the required restored or blocked state on disk.

### External plugin package acceptance

- [x] Publish or assemble one valid external plugin package fixture.
- [x] Start the Host with the package root configured.
- [x] Verify discovery through status and `tools/list`.
- [x] Invoke one external query or mutation over stdio.
- [x] Keep private-dependency version-routing detail in Host component integration rather than multiplying acceptance workflows.

### Replacement mapping

- [x] Map each new acceptance workflow to current direct-MCP tests it supersedes.
- [x] Do not delete the old tests yet; mark the mapping in the implementation notes.

### Exit criteria

- [x] Startup/catalogue, Workspace query, transaction mutation and Code Action paths all pass over stdio.
- [x] At least one restart/recovery path has process-level evidence.
- [x] Existing MCP names and request schemas remain unchanged; the approved uniform `ok`/`data` success envelope is enforced across all response families.

## Stage 4: Narrow and Restructure Component Integration Suites

### General retention rule

Keep a scenario only when the real boundary can fail differently from unit-test collaborators or in-memory Roslyn data. Remove or move it when it merely repeats a production branch already covered by unit tests.

Do not count integration tests toward unit line/branch coverage.

### 4A: Workspace component integration

#### Keep

- [ ] `AtomicFileWriterIntegrationTests`: real create/replace and exact-byte behaviour.
- [ ] `DurableWorkspaceCommitIntegrationTests`: multi-file success, restoration, divergence, restart recovery, directory cleanup and inter-process lock release.
- [ ] Representative SDK project, solution-format and imported-build-input loading.
- [ ] Real filesystem/import changes in change detection.
- [ ] Real open/close and multiple-workspace behaviour.
- [ ] Real external change during a transaction.
- [ ] Real source/metadata, cross-project, ambiguity and documentation-ID resolution.

#### Consolidate or remove

- [ ] Coordinator rejection, status and cancellation branches already covered by unit tests.
- [ ] Pure comparison branches in change detection.
- [ ] Resolver snapshot/status failures already covered by unit tests.
- [ ] Direct Plugin/MCP harness usage that does not add a Workspace boundary.

#### Restructure

- [ ] Split `WorkspaceCoordinatorIntegrationTests` by lifecycle, external-change and transaction capability.
- [ ] Keep mutation/recovery classes isolated.
- [ ] Share only proven immutable read-only fixtures.

### 4B: Plugins.Core component integration

#### Keep

- [ ] One solution/project/document projection flow.
- [ ] Diagnostics plus representative operation/control-flow semantics.
- [ ] Representative cross-project references/implementations.
- [ ] One dependency graph/search family flow.
- [ ] Real selector ambiguity/cross-project behaviour.
- [ ] Representative rename plus formatting/sort staging.
- [ ] Solution/project format and imported-property structure cases.

#### Consolidate or remove

- [ ] Malformed/missing/cancellation branches covered by service unit tests.
- [ ] A separate integration test for every query tool.
- [ ] Snapshot and selector response branches that do not depend on real Workspace behaviour.

#### Restructure

- [ ] Split the 13-case default-project-structure class into retained capability cases or consolidate equivalent inputs.
- [ ] Use a collection-scoped read-only fixture only after concurrent query safety is demonstrated.
- [ ] Keep mutation tests on isolated fixture copies.

### 4C: CodeActions component integration

#### Keep

- [ ] Real MEF provider catalogue composition.
- [ ] Controlled-provider list/describe/stage workflow.
- [ ] Controlled fix-all across a real solution.
- [ ] One representative supported bundled action reaching Workspace staging.

#### Consolidate or remove

- [ ] Token tampering, expiry and staleness branches covered by unit tests.
- [ ] Duplicate provider scenarios already governed by the compatibility audit.
- [ ] Direct MCP publication assertions moved to acceptance.

#### Restructure

- [ ] Compose an immutable provider catalogue once at the narrowest safe fixture scope.
- [ ] Split independent provider families into separate classes.
- [ ] Never run mutations concurrently against the same Workspace.

### 4D: Host component integration

#### Keep

- [ ] Complete DI/container validation and tool-catalogue composition.
- [ ] Options fallback composition.
- [ ] Plugin package enumeration and metadata validation.
- [ ] MEF composition and generic materialisation.
- [ ] Assembly-load-context shared/private dependency routing.
- [ ] Real MCP SDK schema exporter compatibility.
- [ ] Recovery persistence-to-service mapping.

#### Move from Host unit tests

- [ ] `HostToolCompositionTests`: two complete composition cases.
- [ ] `MefPluginComposerTests`: three real composition cases.
- [ ] `PluginCatalogBootstrapTests`: one real bootstrap case.
- [ ] `PluginAssemblyLoadContextTests`: six load-context cases.
- [ ] `PluginAssemblyMetadataReaderTests`: nine real PE metadata cases.
- [ ] `MsBuildRegistrationServiceTests`: two registration cases.
- [ ] The Generic Host lifecycle-ordering case identified in `HostUnitTestInventory.md`.

Reconfirm the exact count before moving; the current inventory records 24 boundary tests.

#### Replace or split

- [ ] `RepresentativeMcpToolIntegrationTests`: replace protocol assertions with acceptance workflows.
- [ ] `WorkspaceLifecycleMcpIntegrationTests`: replace protocol workflow with stdio acceptance.
- [ ] `PluginDiscoveryAndMcpToolIntegrationTests`: retain package discovery, remove direct protocol duplication after acceptance.
- [ ] `ServerStatusRecoveryIntegrationTests`: retain persistence/service boundary; leave JSON shape to contract and acceptance tests.

### Verification for every substage

- [ ] Run the affected unit project to prove removed integration branches remain covered.
- [ ] Run the affected integration project.
- [ ] Record before/after scenario count and wall time.
- [ ] Explain every deleted or consolidated scenario in the stage notes.

### Exit criteria

- [ ] Every retained component test has a named real boundary.
- [ ] No component project relies on a full test-owned application composition root.
- [ ] The 24 Host boundary cases are correctly classified.

## Stage 5: Remove Obsolete Harnesses and Narrow Shared Support

### Goal

Remove the parallel application and protocol implementation only after replacement evidence is green.

### Work

- [ ] Confirm all mappings from old direct-MCP tests to Stage 3 acceptance workflows.
- [ ] Remove `McpIntegrationTestHost` when it has no unique boundary consumer.
- [ ] Remove direct protocol responsibilities from `PluginToolTestHarness`.
- [ ] Remove direct protocol responsibilities from `CodeActionToolTestHarness`.
- [ ] Remove `WorkspaceCoordinatorFactory` after owner tests no longer need the full parallel graph.
- [ ] Remove `WorkspaceRuntime` and `IWorkspaceRuntime` if no narrow component use remains.
- [ ] Remove per-invocation service-provider construction.
- [ ] Remove orphaned integration-support factories and DTO adapters.
- [ ] Keep only:
  - production-independent asset materialisation;
  - unique temporary-root ownership;
  - documented early MSBuild registration;
  - controlled Code Action providers/classification;
  - small compiled plugin fixture projects where real assemblies are required.
- [ ] Recheck project references so normal unit support cannot reference integration support and acceptance cannot reference production.

### Production-change gate

If tests still require a large production graph after full workflows have moved to Host/acceptance:

- [ ] Document why component-level construction is insufficient.
- [ ] Propose the smallest production DI/composition seam.
- [ ] Explain its production value independently of testing.
- [ ] Obtain explicit user approval before implementing it.

### Exit criteria

- [ ] Integration support no longer duplicates Host composition.
- [ ] No test-owned class manually implements MCP transport publication.
- [ ] Shared support has a small, coherent responsibility set.

## Stage 6: Separate Compatibility Audit from Governance Checks

### Goal

Make the expensive Code Action audit exclusively about version-sensitive Roslyn compatibility.

### Work

- [ ] Keep built-in provider visibility/replay compatibility in `Roslyn.Workbench.Mcp.CodeActions.AuditTest`.
- [ ] Keep the supported/impossible provider ledger as the audit source of truth.
- [ ] Move `InternalArgumentNullGuardAuditTests` to fast architecture/governance coverage.
- [ ] Move `ProductionNullForgivingOperatorAuditTests` to the same fast ownership area.
- [ ] Do not change the source rules or replace the tests with analysers without a separate decision.
- [ ] Share immutable audit catalogue/setup where safe.
- [ ] Split provider or replay families into independently schedulable classes.
- [ ] Set a measured concurrency limit that prevents the current greater-than-1-GiB peak from multiplying uncontrollably.
- [ ] Verify supported-provider cases remain deterministic under the chosen grouping.

### CI policy

- [ ] Run the complete audit on schedule and main.
- [ ] On pull requests, run it when CodeActions, Roslyn dependencies, provider classification, replay logic or audit infrastructure changes.
- [ ] Keep a scheduled full run so path filtering cannot permanently hide drift.

### Exit criteria

- [ ] The audit project contains compatibility checks only.
- [ ] Source-governance checks remain in the fast loop.
- [ ] Audit runtime and memory are recorded after restructuring.

## Stage 7: Optimise Execution and CI

### Local execution optimisation order

- [ ] Re-measure after isolation and deterministic disposal.
- [ ] Re-measure after asset migration.
- [ ] Re-measure after scenario consolidation.
- [ ] Re-measure after immutable fixture reuse.
- [ ] Split serial monoliths and enable only safe class-level parallelism.
- [ ] Keep explicit serial collections for process-global or mutable shared state.
- [ ] Avoid disabling parallelism for an entire assembly unless measured evidence requires it.

### CI build/test flow

- [ ] Restore once per job.
- [ ] Build once per job or consume a verified build artifact.
- [ ] Publish the Host once for acceptance tests.
- [ ] Run test projects with `--no-build --no-restore` where outputs are already available.
- [ ] Compare an owner matrix with a consolidated component job using total CI duration, queue/startup overhead and failure isolation.
- [ ] Preserve owner-specific reporting even if execution is consolidated.

### Operating-system coverage

- [ ] Run acceptance on Ubuntu and Windows for pull requests.
- [ ] Run atomic file, multi-file transaction, recovery and inter-process lock scenarios on Ubuntu and Windows.
- [ ] Add macOS on schedule initially.
- [ ] Promote macOS to a pull-request gate only after runtime and reliability evidence supports it.

### Failure diagnostics

- [ ] Write TRX or equivalent structured results for every project.
- [ ] Upload results even when a test step fails.
- [ ] Upload server stderr and process details for failed acceptance tests.
- [ ] Upload failed fixture/recovery state where it is useful and free of secrets.
- [ ] Add hang detection appropriate to Roslyn/MSBuild operations.
- [ ] Add a minimum expected test count so filters cannot yield a green empty run.

### Microsoft.Testing.Platform evaluation

Perform this only after structural timings stabilise.

- [ ] Create an isolated MTP evaluation change using the .NET 10 `global.json` runner setting.
- [ ] Verify every xUnit project and runner package supports it.
- [ ] Verify category filtering, coverage, IDE execution and result reporting.
- [ ] Evaluate `--test-modules`, module parallelism and `--minimum-expected-tests`.
- [ ] Compare median clean and warm timings with VSTest.
- [ ] Adopt MTP only when compatibility is complete and the gain is material.
- [ ] Keep the runner decision separate in history so regressions are attributable.

### NuGet caching decision

- [ ] Do not enable `setup-dotnet` package caching without lock files.
- [ ] Evaluate lock-file policy as a separate repository dependency-management decision.
- [ ] Enable caching only if the approved lock-file policy makes it reliable.

### Performance target

- [ ] Reduce median normal component-integration wall time by at least 40% from the approximately 49-second sequential warm baseline, or document why retained real-boundary cost makes a lower improvement the correct trade-off.
- [ ] Keep acceptance small enough to run on every pull request.
- [ ] Do not enforce timing as a brittle test assertion.

### Exit criteria

- [ ] CI and local commands execute the same test projects and test logic.
- [ ] Results and process failures are diagnosable from artifacts.
- [ ] Platform-sensitive behaviour has Windows and Linux evidence.
- [ ] Final timings are materially improved and documented.

## Stage 8: Final Re-audit and Documentation

### Architecture audit

- [ ] Verify every integration class states the real boundary it protects.
- [ ] Verify no acceptance project references production code, mocks or internals.
- [ ] Verify Host alone owns production MCP transport composition.
- [ ] Verify component tests do not reconstruct the entire Host.
- [ ] Verify every fixture and child process is deterministically disposed.
- [ ] Verify every scenario has isolated mutable state.
- [ ] Verify no removed integration scenario left an uncovered unit branch.
- [ ] Verify transaction success, rollback/restoration, recovery, divergence and locking retain real-filesystem evidence.
- [ ] Verify plugin discovery and private dependency routing retain real assembly evidence.
- [ ] Verify controlled and bundled Code Action evidence remains.
- [ ] Verify existing MCP names, schemas and JSON contracts remain unchanged.

### Documentation

- [ ] Update `TestingStrategy.md` with the final component/acceptance model.
- [ ] Mark `IntegrationTestingStrategyProposal.md` implemented and link to final evidence.
- [ ] Mark this implementation plan complete stage by stage.
- [ ] Update `TestArchitectureReaudit-2026-07-10.md` or add a dated replacement re-audit rather than silently editing historical evidence.
- [ ] Update `HostUnitTestInventory.md` after moving boundary tests.
- [ ] Update tool/capability inventories for retained integration and acceptance ownership.
- [ ] Update contributor commands and `.github/workflows/tests.yml` documentation.
- [ ] Record final project counts, scenario counts, median timings and peak memory.

### Full verification

- [ ] Format only changed C# files:

```bash
dotnet format --include <changed-files> --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

- [ ] Restore:

```bash
dotnet restore --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

- [ ] Build:

```bash
dotnet build --no-restore --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

- [ ] Run unit and contract tests:

```bash
dotnet test --no-build --no-restore --filter "Category!=Integration&Category!=Audit" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

- [ ] Run every component integration project explicitly.
- [ ] Publish the Host and run the acceptance project explicitly.
- [ ] Run the Code Action audit explicitly.
- [ ] Run the full suite.
- [ ] Verify governed changed files are CRLF and not mixed.
- [ ] Inspect final test-project references and categories.

### Completion criteria

- [ ] The published executable is tested through real stdio with the official MCP client.
- [ ] No test described as acceptance invokes a tool object directly.
- [ ] Integration support does not reproduce Host composition.
- [ ] Test assets are checked in, readable and copied into isolated scenario roots.
- [ ] All stateful resources are deterministically disposed.
- [ ] Every retained component test proves a named external or multi-component boundary.
- [ ] Ordinary branch variants remain unit-test responsibilities.
- [ ] Compatibility audit and source governance have separate ownership.
- [ ] CI cannot silently pass with zero selected tests.
- [ ] CI preserves diagnostics for failed processes and fixtures.
- [ ] Final timing and memory evidence is recorded.
- [ ] Canonical documentation describes the resulting system rather than the historical suite.

## Decisions Deliberately Deferred

The implementing agent must not silently resolve these during an unrelated stage:

- [ ] VSTest versus Microsoft.Testing.Platform: decide after the structural benchmark.
- [ ] One integration CI job versus a project matrix: decide from end-to-end CI measurements.
- [ ] Collection-scoped concurrent Workspace reuse: prove thread safety first.
- [ ] NuGet lock files and caching: make a separate dependency-policy decision.
- [ ] macOS pull-request gating: decide after scheduled evidence.
- [ ] New production composition seams: require explicit approval.
- [ ] Replacing source-policy tests with analysers: outside this plan.

## Stage Progress Summary

| Stage | Description | Status |
| --- | --- | --- |
| 0 | Existing-suite safety and reproducible baseline | Complete |
| 1 | Checked-in fixture assets | Not started |
| 2 | Out-of-process acceptance foundation | Not started |
| 3 | Representative acceptance workflows | Not started |
| 4 | Component integration migration | Not started |
| 5 | Obsolete harness removal and support narrowing | Not started |
| 6 | Compatibility/governance separation | Not started |
| 7 | Performance, runner evaluation and CI | Not started |
| 8 | Final re-audit and canonical documentation | Not started |

Update this table and the detailed checkboxes as work is completed. A stage is complete only when its exit criteria and verification items are satisfied, not merely when its code changes have been written.
