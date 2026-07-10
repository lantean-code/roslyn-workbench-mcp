# Test Architecture Re-audit

Date: 2026-07-10

## Purpose

This document records the test architecture after phases 1-6 of `IntegrationTestReorganisationPlan.md` and the subsequent production-boundary refactor. It is the current description of project ownership, execution categories and remaining gaps. Policy and test-layer rules are defined in `TestingStrategy.md`.

This document supersedes the structural conclusions in `test-project-audit-2026-07-07.md`. Tool-level ownership and known partial branches are recorded in `Tool Test Inventory.md`.

## Executive Assessment

The reorganisation and remediation are complete except for the separately deferred Workspace unit-coverage round.

The current structure provides:

- physical separation between unit/contract, integration and audit coverage
- assembly-level category enforcement for integration and audit projects
- a small unit-safe `TestSupport` project and a one-way integration-only support dependency
- capability-focused integration suites instead of a duplicate per-tool integration matrix
- mock-isolated host service and tool tests, with host composition and persistence checks at the integration boundary
- production code with no knowledge of controlled test-provider identities
- deterministic plugin-discovery fixtures in dedicated assemblies
- separate typed Plugin and Code Action catalogues, contexts and Host transport paths
- contract tests colocated with the production assembly that owns each contract family
- independent CI gates for fast, integration and compatibility-audit coverage

The boundary-refactored suite discovers 1,150 tests: 979 unit/contract tests, 78 integration tests and 93 audit tests. All pass in the current workspace.

Two evidence gaps remain open: the 190-test Workspace unit project now covers the principal pure selection, state, operation, execution-context and transaction primitives but has not yet completed proposal-validation, diff and lifecycle orchestration coverage, and the intended production dependency graph is verified manually rather than by an automated architecture check. Neither is masked by integration coverage.

## Current Project Topology

| Project | Category | Responsibility | Tests |
| --- | --- | --- | ---: |
| `Roslyn.Workbench.Mcp.Plugins.Test` | Unit/Contract | Typed plugin registration, context adaptation, execution results and public surface | 26 |
| `Roslyn.Workbench.Mcp.Workspace.Test` | Unit/Contract | Workspace-owned selectors, validation and execution-boundary primitives | 190 |
| `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Unit | Bundled inspection and normal-refactoring branches | 308 |
| `Roslyn.Workbench.Mcp.CodeActions.Test` | Unit | Code-action services, workflows, catalogues and tools | 238 |
| `Roslyn.Workbench.Mcp.Test` | Unit/Contract | Host services, MCP schemas/envelopes, typed transport adapters and relocated Host contracts | 217 |
| `Roslyn.Workbench.Mcp.Workspace.IntegrationTest` | Integration | MSBuild, workspace lifecycle, selection and transaction persistence | 42 |
| `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` | Integration | Capability-focused real-workspace inspection and mutation | 14 |
| `Roslyn.Workbench.Mcp.CodeActions.IntegrationTest` | Integration | Controlled-provider workflows, tokens, fix-all and representative built-in staging | 10 |
| `Roslyn.Workbench.Mcp.IntegrationTest` | Integration | Host composition, MCP adapter, recovery, lifecycle and plugin discovery acceptance | 12 |
| `Roslyn.Workbench.Mcp.CodeActions.AuditTest` | Audit | Built-in Roslyn provider compatibility and replay wrappers | 93 |

Support and fixture projects are not test assemblies:

| Project | Responsibility |
| --- | --- |
| `Roslyn.Workbench.Mcp.TestSupport` | Moq context graphs, selector factories and in-memory Roslyn document/solution helpers |
| `Roslyn.Workbench.Mcp.IntegrationTestSupport` | MSBuild registration, temporary workspaces, real runtime composition and controlled providers |
| `Roslyn.Workbench.Mcp.HostQueryPluginFixture` | One deterministic query plugin for assembly discovery |
| `Roslyn.Workbench.Mcp.HostMutationPluginFixture` | One deterministic mutation plugin for assembly discovery |
| `Roslyn.Workbench.Mcp.InvalidPluginFixture` | Deliberately invalid, duplicate and throwing plugin shapes for discovery diagnostics |

## Boundary Controls

### Category isolation

`test/Directory.Build.targets` derives the required category from the project suffix, adds an assembly-level xUnit trait and rejects a mismatched `TestCategory`.

The fast-loop filter is:

```text
Category!=Integration&Category!=Audit
```

It selects 682 tests and no test from an `*.IntegrationTest` or `*.AuditTest` assembly.

### Support dependency direction

The targets file rejects references to `Roslyn.Workbench.Mcp.IntegrationTestSupport` from normal test projects and from `Roslyn.Workbench.Mcp.TestSupport`.

```text
Unit/contract tests -> TestSupport -> Plugins -> Workspace
Integration/audit tests -> IntegrationTestSupport -> production projects
IntegrationTestSupport -X-> TestSupport
TestSupport -X-> IntegrationTestSupport
```

Integration support constructs the Workspace, plugin and internal Code Action paths explicitly. It has separate typed MCP harnesses and no silent unavailable fallbacks.

### Reflection policy

Reflection is limited to explicit `Category=Contract` locks for deliberate public plugin surface or protocol metadata. Runtime behaviour and internal execution-context capability boundaries remain normal behavioural tests. Reflection must not invoke implementation code or preserve an internal runtime shape merely because an older test asserted it.

### Production ownership and execution paths

The former Contracts assembly and test project no longer exist. Workspace owns its domain and selection contracts, Plugins.Core owns inspection contracts, CodeActions owns Code Action/refactoring contracts, Plugins owns plugin contracts and Host owns MCP/lifecycle contracts. Their tests now live in the corresponding owning unit project.

Plugin query, plugin mutation, Code Action query and Code Action mutation are separate closed generic Host adapter paths. Plugins and CodeActions each test their typed catalogue and Workspace context adaptation. Host owns the MCP binding, schema, cancellation, exception and publication behaviour for all four paths.

## Integration Organisation

Integration tests are organised around real boundaries and shared capabilities:

- workspace persistence and lifecycle
- workspace projection
- semantic inspection
- cross-project search
- selector and snapshot semantics
- mutation staging and preview
- MCP protocol and host composition
- persisted recovery reporting
- controlled code-action workflows
- built-in provider compatibility audit

The current capability-to-suite map is maintained in `Tool Test Inventory.md`. No normal integration project contains the former exhaustive one-class-per-wrapper matrix.

## Remediation Status

### Resolved: host composition in the unit project

The two tests that build the real generic host and inspect the complete service/MCP graph now live in `HostCompositionIntegrationTests`. The unit class retains only isolated argument-null guards.

### Resolved: recovery-store I/O in the unit project

`ServerStatusToolTests` now mocks `IServerStatusService` and verifies only tool mapping. `ServerStatusServiceTests` mock `IRecoveryStatusReader` and own summary/full service behaviour. `ServerStatusRecoveryIntegrationTests` retains the real persistence-to-MCP boundary.

### Resolved: production classification knew test-provider identities

`CodeActionDescriptorRegistry` accepts internal composable classification overrides and contains only production classification rules. Controlled provider IDs, titles and option-gathering behaviour are owned by `ControlledCodeActionDescriptorClassifier` in `IntegrationTestSupport`.

The controlled providers now use the `Roslyn.Workbench.Mcp.IntegrationTestSupport` namespace; production no longer depends on test namespaces or identities.

### Open: Workspace unit coverage is not evidenced

The complete source disposition, scenario inventory and proposed delivery phases are recorded in `WorkspaceUnitTestInventory.md`.

`Roslyn.Workbench.Mcp.Workspace` contains 113 C# source files after taking ownership of the Workspace contract families. Its unit project currently contains 17 tests across selector validation, `WorkspaceSelectionResult` and execution-lease boundaries; the remaining work is tracked in `WorkspaceUnitTestInventory.md`.

The Workspace integration project has 42 useful boundary tests, but these do not replace focused branch coverage of independently testable services. There is no current post-reorganisation assembly-level coverage report proving the repository's stated line and branch objectives.

Deferred action for the separate Workspace round:

- collect fast-loop coverage by production assembly
- identify Workspace services with meaningful mockable branches
- add behaviour-focused unit tests without moving coordinator, filesystem or MSBuild flows back into the unit project
- document genuinely unreachable defensive branches rather than introducing reflection or test-only production hooks

### Resolved: stale inventory documents

`Tool Test Inventory.md` now maps integration ownership to capability suites rather than deleted per-tool class names. The 2026-07-07 audit has a prominent superseded notice linking to this document.

### Resolved: orphaned integration helpers

Unused context builders, the refactoring request factory, direct query/mutation execution helpers and `ToolMutationExecutionResult` have been deleted. Remaining integration support has active consumers.

### Resolved: invalid plugins shared the integration-support assembly

Invalid, duplicate and throwing discovery fixtures now compile into `Roslyn.Workbench.Mcp.InvalidPluginFixture`. Plugin discovery no longer scans the general integration-support assembly for these scenarios.

### Resolved: unclassified reflection locks

Both identified reflection assertions are now contract tests, and the plugin surface assertion is grouped with the other public-surface locks.

### Resolved: integration filename mismatch

The file is now named `PluginDiscoveryAndMcpToolIntegrationTests.cs`, matching its class.

### Resolved: CI did not represent the execution model

`.github/workflows/tests.yml` provides:

- a unit/contract job using the fast-loop category filter
- four independent integration matrix jobs for Workspace, bundled core, code actions and host/MCP
- an independent code-action compatibility-audit job
- pull-request, main-branch, weekly schedule and manual triggers

The audit runs on every configured trigger. This is deliberately stricter than the minimum dependency-change-only pull-request gate and avoids a path-filter maintenance blind spot.

## Areas Still Lacking

### Medium: Workspace unit coverage is not evidenced

The deferred Workspace unit-coverage evidence remains open and is intentionally handled as a separate programme.

### Resolved: all four Host adapters have focused unit evidence

The obsolete `ToolExecutorTests` class has been removed. Dedicated closed-generic tests now cover Plugin query, Plugin mutation, Code Action query and Code Action mutation transport paths. Each adapter has measured 100% line and branch coverage for typed request binding, acquisition rejection, handler outcomes, publication, malformed input, cancellation, exceptions and lease disposal; both mutation adapters additionally cover separate staging and staging failures.

The Code Action handler bases now implement narrow query and mutation interfaces, matching the interface-based substitution seam used by plugins. Host adapters depend on those interfaces, so their tests do not require subclasses, runtime casts or reflection.

### Documented Host coverage boundaries

Process entry, static MSBuild registration, plugin assembly discovery, persisted recovery I/O and complete DI composition remain integration-test responsibilities. Defensive assembly-version fallbacks and schema-exporter compatibility branches that cannot be produced by loaded assemblies or the current MCP SDK are documented rather than reached through reflection or artificial production hooks.

### Low: architecture dependency enforcement is documented but not automated

The project references have been manually verified against the intended graph, but there is no dedicated build-time architecture test that rejects a future Workspace-to-Plugins/CodeActions reference, a CodeActions-to-Plugins reference or MCP SDK usage outside Host. Prefer an MSBuild/project-reference check over reflection-based assembly shape tests.

Separate tool-level partial branches remain documented in `Tool Test Inventory.md`. They are coverage follow-up items, not project-boundary errors. They should be evaluated with measured coverage and explicit unreachable-branch decisions.

## Verification Evidence

The remediated workspace was verified with:

```bash
dotnet format --include <changed C# files> --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet build --no-restore --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test --no-restore --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

Results:

- build: 0 warnings, 0 errors
- unit/contract projects: 979 passed
- integration projects: 78 passed
- code-action audit: 93 passed
- full suite: 1,150 passed
- changed CRLF-governed files normalised to CRLF
