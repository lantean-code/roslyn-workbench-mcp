# Integration Test Reorganisation Plan

Date: 2026-07-10

> **Completed and superseded:** This plan records the pre-boundary-refactor baseline and the phases used to reorganise it. Project names, counts and ownership statements below are historical and must not be used as current guidance. Use `TestingStrategy.md` for policy, `TestArchitectureReaudit-2026-07-18.md` for the current topology and findings, and `Tool Test Inventory.md` for current ownership.

## Purpose

This plan reorganises the repository's integration coverage around system boundaries rather than individual tool classes.

The unit-test projects own tool registration, `ExecuteAsync(...)` flow, branch coverage, request variations, collaborator interaction, and Roslyn algorithm behaviour that can be represented with in-memory Roslyn objects. Integration tests exist only to prove behaviour that requires multiple production components or an external runtime boundary.

The intended result is a faster and more diagnostic suite without losing protection for MSBuild loading, workspace resolution, transaction persistence, MCP protocol composition, plugin discovery, or real Roslyn MEF provider compatibility.

## Current Baseline

The baseline after the code-action discovery cleanup includes every project under `test` that is present in the solution:

| Project | Project kind | Current role | Discovered tests | Recent duration |
| --- | --- | --- | --: | --: |
| `Roslyn.Workbench.Mcp.Contracts.Test` | Test project | Contract DTO, schema, selector, validation, and serialisation coverage | 53 | less than 1 second |
| `Roslyn.Workbench.Mcp.Plugins.Test` | Test project | Plugin abstraction, registration, protocol, and execution-plumbing coverage | 22 | less than 1 second |
| `Roslyn.Workbench.Mcp.Workspace.Test` | Test project | Mixed workspace unit, contract, and integration coverage | 123 | approximately 28 seconds |
| `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Test project | Bundled inspection and normal-refactoring unit coverage | 311 | approximately 5 seconds |
| `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` | Integration test project | Bundled inspection and mutation workflows | 66 | approximately 29 seconds |
| `Roslyn.Workbench.Mcp.CodeActions.Test` | Test project | Code-action service and tool unit coverage | 146 | less than 1 second |
| `Roslyn.Workbench.Mcp.CodeActions.IntegrationTest` | Integration and audit test project | Code-action workflows, wrappers, and provider audits | 158 | approximately 1 minute 17 seconds |
| `Roslyn.Workbench.Mcp.Test` | Test project | Host service and server-owned-tool unit coverage | 27 | approximately 1 second |
| `Roslyn.Workbench.Mcp.IntegrationTest` | Integration test project | Host, MCP, plugin, and lifecycle acceptance coverage | 31 | approximately 22 seconds |
| `Roslyn.Workbench.Mcp.TestSupport` | Support project | Shared unit helpers, integration fixtures, runtime harnesses, and audit infrastructure | Not applicable | Not applicable |
| `Roslyn.Workbench.Mcp.HostQueryPluginFixture` | Fixture assembly | Single query-plugin assembly used by plugin discovery integration tests | Not applicable | Not applicable |
| `Roslyn.Workbench.Mcp.HostMutationPluginFixture` | Fixture assembly | Single mutation-plugin assembly used by plugin discovery integration tests | Not applicable | Not applicable |

The code-action project includes 58 supported-provider compatibility probes. These are intentionally audit coverage rather than normal integration coverage.

## Test Responsibilities

### Unit

Unit tests must:

- use xUnit, Moq, and AwesomeAssertions
- mock host and runtime collaborators visibly
- use real in-memory Roslyn objects only as Roslyn-owned data or state
- test tool registration first
- follow `ExecuteAsync(...)` branches in logical runtime order
- own line and branch coverage targets
- run during the normal development loop

### Contract

Contract tests must lock stable public or protocol shape, including:

- request and response schemas
- serialisation behaviour
- tool metadata and annotations
- public API surface
- validation rules

Contract tests must not open workspaces or execute transaction workflows.

### Integration

Integration tests must prove a real component boundary, such as:

- loading an actual project through MSBuild
- resolving selectors against a loaded workspace
- reacting to file-system or imported-build-input changes
- staging, previewing, committing, or reloading a transaction
- composing the host runtime through dependency injection
- discovering plugin assemblies
- binding MCP arguments and serialising structured responses
- executing controlled code-action providers through the real workflow

Integration tests must not exist merely to repeat a tool's unit-test happy path.

### Audit

Audit tests must protect compatibility with an external or version-sensitive catalogue, primarily built-in Roslyn MEF providers.

Audit tests:

- are not branch-coverage tests
- do not run in the normal development loop
- run when Roslyn dependencies change and at scheduled or release gates
- use the production support ledger as the source of supported and impossible classifications
- verify supported providers remain visible and replayable
- do not rediscover providers already classified as impossible under current rules

## Target Project Structure

| Project | Status | Target responsibility |
| --- | --- | --- |
| `Roslyn.Workbench.Mcp.Contracts.Test` | Retain | Unit and contract coverage for contracts and schemas |
| `Roslyn.Workbench.Mcp.Plugins.Test` | Retain | Unit and contract coverage for plugin abstractions and execution plumbing |
| `Roslyn.Workbench.Mcp.Workspace.Test` | Retain and narrow | Pure workspace unit and contract coverage |
| `Roslyn.Workbench.Mcp.Workspace.IntegrationTest` | Add | MSBuild, file-system, workspace lifecycle, resolver, and transaction persistence coverage |
| `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Retain | Pure bundled-tool unit coverage |
| `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` | Retain and consolidate | Capability-focused real-workspace coverage for bundled tools |
| `Roslyn.Workbench.Mcp.CodeActions.Test` | Retain | Pure code-action service and tool unit coverage |
| `Roslyn.Workbench.Mcp.CodeActions.IntegrationTest` | Retain and narrow | Controlled-provider workflow and staging integration coverage |
| `Roslyn.Workbench.Mcp.CodeActions.AuditTest` | Add | Built-in Roslyn provider and replay-wrapper compatibility coverage |
| `Roslyn.Workbench.Mcp.Test` | Retain | Pure host and server-owned-tool unit coverage |
| `Roslyn.Workbench.Mcp.IntegrationTest` | Retain and narrow | Small host/MCP acceptance suite |
| `Roslyn.Workbench.Mcp.TestSupport` | Retain and narrow | Unit-safe mock graphs and Roslyn-owned object factories only |
| `Roslyn.Workbench.Mcp.IntegrationTestSupport` | Add | Integration-only MSBuild registration, temporary project fixtures, runtime composition, and shared harnesses |
| `Roslyn.Workbench.Mcp.HostQueryPluginFixture` | Retain as fixture | Dedicated query-plugin discovery input; not a test assembly |
| `Roslyn.Workbench.Mcp.HostMutationPluginFixture` | Retain as fixture | Dedicated mutation-plugin discovery input; not a test assembly |

## Projects Retained Without Integration Responsibilities

The reorganisation must explicitly protect the normal test projects while heavier coverage is moved or consolidated.

### Contracts test project

`Roslyn.Workbench.Mcp.Contracts.Test` remains the owner of:

- contract validation
- schema generation
- serialisation and result-envelope shape
- selector DTO behaviour
- public contract surface locks

It must not reference workspace, host, integration-support, or temporary-project infrastructure.

### Plugin test project

`Roslyn.Workbench.Mcp.Plugins.Test` remains the owner of:

- plugin registry behaviour
- tool metadata and protocol construction
- request binding and execution failure normalisation that can be isolated
- plugin public-surface contract tests

Real plugin-directory loading and fixture assembly discovery remain in `Roslyn.Workbench.Mcp.IntegrationTest`.

### Bundled-core test project

`Roslyn.Workbench.Mcp.Plugins.Core.Test` remains the owner of every bundled inspection and normal-refactoring tool's registration and `ExecuteAsync(...)` unit coverage. Removing duplicate integration tests must not move branch responsibility out of this project.

### Code-action test project

`Roslyn.Workbench.Mcp.CodeActions.Test` remains the owner of independently mockable code-action services, workflows, catalogues, and tools. It must use controlled mocks or Roslyn-owned in-memory data rather than built-in MEF discovery.

### Host test project

`Roslyn.Workbench.Mcp.Test` remains the owner of host services and server-owned tools that can be isolated with Moq. MCP adapter acceptance, plugin discovery, and full workspace lifecycle flows remain in `Roslyn.Workbench.Mcp.IntegrationTest`.

### Plugin fixture assemblies

`Roslyn.Workbench.Mcp.HostQueryPluginFixture` and `Roslyn.Workbench.Mcp.HostMutationPluginFixture` remain deliberately small production-shaped fixture assemblies. They must:

- contain one deterministic plugin each
- contain no xUnit tests or test runner packages
- be referenced only by integration tests that validate plugin discovery
- remain separate until plugin discovery can be tested with an equally faithful but simpler boundary

## Retention Rules

An existing integration test should be retained when removing it would leave one of these risks untested:

- MSBuild cannot load a supported project shape.
- A real workspace selector resolves differently from an in-memory unit shape.
- project references, folders, metadata references, or imported build inputs are projected incorrectly.
- transaction staging, history, preview, commit, reload, conflict, or encoding behaviour fails across components.
- MCP binding, schema publication, structured output, or error mapping fails through the real adapter.
- plugin assemblies are not discovered, filtered, or registered correctly.
- a supported Roslyn MEF provider disappears or no longer offers/replays its expected action.

An existing integration test should be removed or consolidated when:

- it only proves that one tool's happy path succeeds
- the same tool and source shape are already executed through a broader integration workflow
- its assertions repeat unit-test branch assertions
- it directly constructs the tool and then exercises no real boundary beyond Roslyn objects already used by unit tests
- the same provider is probed by dedicated, replay, bundled, and MCP suites
- it asserts that a historical discovery backlog is empty

## Phase 1: Establish Structural Gates

1. Add `Roslyn.Workbench.Mcp.Workspace.IntegrationTest` to the solution.
2. Add `Roslyn.Workbench.Mcp.CodeActions.AuditTest` to the solution.
3. Add `Roslyn.Workbench.Mcp.IntegrationTestSupport` and move integration-only infrastructure out of `Roslyn.Workbench.Mcp.TestSupport`.
4. Ensure integration and audit projects apply assembly-level category traits where appropriate so individual classes cannot accidentally leak into the fast loop.
5. Add an automated contract check that integration projects contain no unclassified tests and audit projects contain only audit tests.

Success criteria:

- `Category!=Integration&Category!=Audit` selects no tests from integration or audit projects.
- Project names and class suffixes communicate their test level accurately.
- No production-code changes are required for classification alone.

## Phase 2: Split Workspace Coverage

Move real-boundary coverage from `Roslyn.Workbench.Mcp.Workspace.Test` into `Roslyn.Workbench.Mcp.Workspace.IntegrationTest`.

Initial move candidates:

- `WorkspaceProjectCompatibilityInspectorIntegrationTests`
- integration methods from `WorkspaceCoordinatorTests`
- `WorkspaceInputManifestBuilderTests`
- real-workspace methods from `WorkspaceResolverTests`
- any `WorkspaceDiffBuilderTests` scenarios that genuinely require a real workspace or file system

Before moving `WorkspaceCoordinatorTests`, extract its reflection and public-surface checks into a dedicated contract class that remains in `Roslyn.Workbench.Mcp.Workspace.Test`.

Reclassify any test that only needs an in-memory Roslyn solution as a unit test rather than moving it automatically. In particular, reassess document-diff and selector scenarios before treating them as integration coverage.

Retain integration coverage for:

- SDK and non-SDK project detection
- evaluated imports and `.editorconfig` inputs
- workspace out-of-date and reload transitions
- multiple workspace selection
- real selector ambiguity
- transaction staging, history, preview, rollback, and commit
- external changes during transactions
- encoding-preserving commits
- malformed project diagnostics and recovery

Success criteria:

- `Roslyn.Workbench.Mcp.Workspace.Test` does not create temporary projects or open MSBuild workspaces.
- The new integration project contains all real file-system and persistence scenarios.
- Existing workspace behaviour remains covered without duplicate unit and integration assertions.

## Phase 3: Consolidate Bundled-Core Integration Coverage

Replace per-tool happy-path organisation with capability-focused suites.

Target capability groups:

1. `WorkspaceProjectionIntegrationTests`
   - solution structure
   - project details
   - document options
   - folders, project references, and metadata references
2. `SemanticInspectionIntegrationTests`
   - representative diagnostics, operation, control-flow, and semantic-model execution
3. `SolutionSearchIntegrationTests`
   - cross-project references, callers, implementations, derived types, and dependencies
4. `SelectorAndSnapshotIntegrationTests`
   - ambiguity, stale snapshots, metadata symbols, and bounded results
5. `MutationPipelineIntegrationTests`
   - rename and format-document through plugin registration, binding, resolution, staging, and preview, with organize-imports covered through the Code Action transaction path

For the three normal refactoring tools:

- strengthen the existing batch mutation workflow to assert the resulting preview content and transaction revisions
- remove `FormatDocumentToolIntegrationTests` and `RenameSymbolToolIntegrationTests` once the strengthened workflow covers their unique assertions

For inspection tools:

- preserve special real-workspace shapes, especially multi-project and metadata scenarios
- remove dedicated one-test classes whose only assertion is a successful result already covered by a capability suite
- split the current `InspectionToolsTests` monolith into the target capability groups rather than retaining both the monolith and dedicated classes

Success criteria:

- Every retained test names the boundary or capability it protects.
- No integration class exists solely because a corresponding tool class exists.
- All unique cross-project, MSBuild, selector, and snapshot scenarios remain covered.
- The project executes materially faster than the current approximately 29-second baseline.

## Phase 4: Reduce Host/MCP Acceptance Coverage

Keep the host integration project focused on end-to-end host boundaries:

- plugin assembly discovery and disabled-plugin diagnostics
- MCP metadata and output-schema publication
- argument binding and structured error mapping
- workspace open, list, status, reload, and close workflows
- transaction start, preview, history, rollback, and commit workflows
- one representative inspection query through MCP
- one representative normal mutation through MCP
- one representative code action through MCP

Move metadata-only and public-surface assertions into unit or contract projects. Remove broad per-tool output assertions when the tool's own unit tests and a representative MCP adapter test already cover the two participating components.

Success criteria:

- The host integration project proves protocol and composition boundaries rather than the complete bundled-tool catalogue.
- Code-action provider matrices do not run through the host project.
- Failures identify host composition or protocol regressions rather than individual tool branches.

## Phase 5: Separate Code-Action Integration From Audit

Move built-in provider compatibility work into `Roslyn.Workbench.Mcp.CodeActions.AuditTest`:

- `BuiltInCodeActionCompatibilityTests`
- `ReplayRefactoringToolsTests`
- the supported compatibility data and audit-only fixture infrastructure

Keep in `Roslyn.Workbench.Mcp.CodeActions.IntegrationTest`:

- list and describe workflows using controlled test providers
- action-token creation, tampering, expiry, and stale-snapshot handling
- parameterised-action rejection and description
- stage-code-action, stage-code-fix, and stage-fix-all workflows
- document, project, and solution fix-all scope behaviour
- a small representative set of built-in provider staging tests where controlled providers cannot represent an important Roslyn boundary

Consolidate or remove:

- one-class-per-wrapper happy-path integration tests
- bundled tests that replay the same wrappers as the audit matrix
- duplicate visibility and replayability probes
- registration-surface assertions already owned by unit tests

The production ledger remains authoritative:

- supported entries are protected by the compatibility matrix
- impossible entries and their reasons remain documented in `RoslynCodeActionsAudit.md`
- broad provider rediscovery is a deliberate maintenance activity during Roslyn upgrades, not a recurring test

Success criteria:

- The normal code-action integration project contains no exhaustive built-in-provider matrix.
- The audit project can be invoked independently.
- Every supported compatibility case runs once per audit gate.
- The integration project is substantially faster than the current 158-test, approximately 77-second baseline.

## Phase 6: Shrink Shared Test Support

Keep in `Roslyn.Workbench.Mcp.TestSupport`:

- `QueryContextMockHelper`
- `MutationContextMockHelper`
- `RoslynTestFactory`
- selector factories
- narrow Roslyn document and solution helpers
- simple contract data factories used by unit tests

Move out of unit test support:

- temporary project and directory fixtures
- MSBuild registration helpers used only by integration tests
- workspace coordinator composition
- bundled runtime harnesses
- plugin execution harnesses
- provider audit fixtures and ledgers used only by audit tests

Prefer project-local helpers for scenario-specific wiring, but place all reusable integration-only infrastructure in `Roslyn.Workbench.Mcp.IntegrationTestSupport`. Scenario-specific setup remains in the test class even when low-level fixture creation is shared.

Dependency rules:

- integration and audit projects may reference `Roslyn.Workbench.Mcp.IntegrationTestSupport`
- unit and contract test projects must never reference `Roslyn.Workbench.Mcp.IntegrationTestSupport`
- `Roslyn.Workbench.Mcp.TestSupport` must not reference `Roslyn.Workbench.Mcp.IntegrationTestSupport`
- integration-only helpers must not remain in `Roslyn.Workbench.Mcp.TestSupport` as a convenience for unit tests
- automated project-reference checks must enforce this direction

Success criteria:

- Unit-test projects cannot accidentally consume real coordinator or temporary-workspace harnesses through their normal support reference.
- Shared helpers have one narrow responsibility.
- Integration composition is not presented as a unit-test convenience API.

## Phase 7: CI And Developer Execution

### Local development

Run unit and contract coverage only:

```bash
dotnet test Roslyn.Workbench.Mcp.slnx --filter "Category!=Integration&Category!=Audit" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

### Pull requests

Always run unit and contract tests. Run integration projects as a separate stage, in parallel by project where CI capacity allows it.

Changes should select additional gates as follows:

| Changed area | Additional required gate |
| --- | --- |
| Workspace loading, resolution, transaction, or persistence | Workspace integration |
| Plugin discovery, MCP adapter, host composition, or server-owned tools | Host integration |
| Bundled inspection or normal refactoring behaviour | Bundled-core integration |
| Code-action workflow, token, staging, or controlled-provider behaviour | Code-action integration |
| Roslyn package version, provider ledger, provider IDs, action matching, or replay wrappers | Code-action audit |

### Main, release, and scheduled gates

- Run all integration projects on the main branch and before deployment or release.
- Run code-action audits on Roslyn dependency changes, scheduled compatibility jobs, and release gates.
- Do not use integration or audit coverage percentages as merge criteria.

## Migration Controls

For each consolidation batch:

1. Record the existing tests and assertions being replaced.
2. Identify the unique boundary each retained test protects.
3. Add or strengthen the replacement capability test before deleting duplicate tests.
4. Run the affected unit project.
5. Run the affected integration or audit project.
6. Run the fast-loop command and confirm no integration or audit leakage.
7. Run the full solution before completing the batch.
8. Report test-count and duration changes alongside any removed scenarios.

No production behaviour should be changed solely to reorganise integration tests. If a test boundary exposes a production design problem, document it separately and request approval before refactoring production code.

## Completion Criteria

The reorganisation is complete when:

- unit tests own tool branch coverage and normal developer feedback
- integration tests are organised by real boundary or capability
- audit tests are physically and operationally separate
- no supported behaviour loses its only meaningful test
- impossible code-action families retain documented reasons without recurring discovery probes
- no integration or audit test runs in the fast loop
- unit support contains no real runtime composition helpers
- integration and audit stages can run independently in CI
- the full solution remains green after every migration phase
- measured execution time is lower and failures identify the responsible architectural layer
