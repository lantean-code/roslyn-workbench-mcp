# Test Project Audit

Date: 2026-07-07

> **Superseded:** This document describes the pre-reorganisation test layout and is retained for historical context only. Use `TestArchitectureReaudit-2026-07-10.md` for the current project topology, boundary rules, coverage map and outstanding work.

Implementation status note, 2026-07-08:

The restructuring described in this document has now been applied directly to the repository.

Completed structural changes:

1. Added explicit test taxonomy guidance to `test/AGENTS.md` for `Unit`, `Contract`, `Integration`, and `Audit`.
2. Split host workflow coverage into `test/Roslyn.Workbench.Mcp.IntegrationTest`.
3. Split bundled-core workflow coverage into `test/Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest`.
4. Removed the host unit project's direct references to other test projects.
5. Moved the reusable `TestWorkspaceFixture` into `Roslyn.Workbench.Mcp.TestSupport`.
6. Added dedicated one-plugin fixture assemblies, now located under `test/TestFixtures/Plugins`, so plugin-discovery integration tests load clean assemblies that match the loader contract.
7. Added explicit xUnit category traits to the moved and reclassified tests.
8. Added targeted `InternalsVisibleTo` entries for the new integration assemblies where internal access was required.

Remaining refactor-required items:

1. `Roslyn.Workbench.Mcp.Workspace.Test` still contains a mix of unit, contract, and integration coverage in one assembly because several high-value tests still rely on direct internal coverage and real coordinator/workspace seams.
2. `Roslyn.Workbench.Mcp.Plugins.Core.Test` still has a small number of mixed files where isolated unit coverage and real-workspace coverage coexist; method-level traits now identify the heavier paths, but a cleaner split would require further production-side seam work.
3. Audit-style coverage for built-in code action governance remains intentionally separate by trait rather than by a dedicated assembly for now.

## Purpose

This document audits the current test landscape under `./test`, classifies what the suite is actually doing today, and sets out a concrete plan to move the repository towards repo-compliant unit testing with xUnit + Moq while preserving the integration coverage that is genuinely useful.

## Executive Summary

The repository uses the correct libraries for tests: xUnit v3, Moq, and AwesomeAssertions are referenced across the test projects. Naming is generally consistent with the repository rules. The main problem is not framework choice. The main problem is test taxonomy.

The suite currently mixes four different concerns inside the same set of test projects:

1. Behaviour-focused unit tests.
2. Contract and API-surface lock tests.
3. In-process component and workflow tests that drive real coordinators, workspaces, transactions, code actions, and temporary file-system fixtures.
4. Design-validation and rollout-audit tests that act more like internal product governance than unit tests.

This means the repository has a meaningful amount of useful coverage, but a large part of it is not unit testing in the strict sense required by the repository guidance. The biggest issues are:

1. Heavy tests are not clearly separated from fast unit tests.
2. Temporary file-system and workspace-driven tests dominate the higher-value projects.
3. A noticeable slice of the suite validates public shape through reflection and JSON payload inspection rather than behaviour through seams.
4. `Roslyn.Workbench.Mcp.TestSupport` has become a test infrastructure layer of its own, which encourages more in-process workflow testing rather than smaller collaborator-based unit tests.
5. There is no explicit category/trait strategy for excluding heavier tests from the default local developer loop.

The suite is not worthless. It does catch real regressions. It is better described as a blended contract/component/integration suite with some unit tests, not as a disciplined unit-test suite.

## Inventory

Current test projects:

| Project | Test count | Predominant character |
| --- | ---: | --- |
| `Roslyn.Workbench.Mcp.Contracts.Test` | 53 | Mostly contract validation and schema/surface tests |
| `Roslyn.Workbench.Mcp.Plugins.Test` | 22 | Mostly small unit tests plus surface-shape tests |
| `Roslyn.Workbench.Mcp.Plugins.Core.Test` | 181 | Mixed, but heavily dominated by fixture-driven workflow/component tests |
| `Roslyn.Workbench.Mcp.Test` | 49 | Host/component tests with some true units |
| `Roslyn.Workbench.Mcp.Workspace.Test` | 58 | Mixed, but largely workspace/file-system/in-process integration tests |
| Total | 363 | Mixed suite, not a pure unit-test portfolio |

Supporting infrastructure:

| Project | Notes |
| --- | --- |
| `Roslyn.Workbench.Mcp.TestSupport` | 29 helper files; effectively a shared test runtime and fixture library |

High-level structural metrics:

1. No `[Trait(...)]` categorisation was found in the current test suite.
2. No skip-based segregation strategy was found.
3. `Roslyn.Workbench.Mcp.Plugins.Core.Test` has 92 files that reference fixture or harness patterns.
4. `Roslyn.Workbench.Mcp.TestSupport` contains reusable fixtures, builders, harnesses, runtime wrappers, and audit helpers, which is far beyond a small helper library.
5. `Roslyn.Workbench.Mcp.Test.csproj` references other test projects directly, which couples test layers together rather than keeping them isolated.

## Current Structure

### 1. Contract and surface-shape tests

These tests mostly lock DTO validation rules, schema generation, output envelopes, and public surface shape.

Representative examples:

1. `test/Roslyn.Workbench.Mcp.Contracts.Test/Selectors/SelectorValidationTests.cs`
2. `test/Roslyn.Workbench.Mcp.Contracts.Test/Schema/SchemaGenerationTests.cs`
3. `test/Roslyn.Workbench.Mcp.Plugins.Test/PluginSurfaceShapeTests.cs`
4. `test/Roslyn.Workbench.Mcp.Plugins.Test/ToolSchemaFactoryTests.cs`

These are often fast and deterministic, but many of them are not classic unit tests. They are contract locks. That is a valid category, but it should be called that explicitly.

### 2. Small isolated unit tests

There are some genuinely good unit tests in the suite.

Representative examples:

1. `test/Roslyn.Workbench.Mcp.Test/MsBuildRegistrationHostedServiceTests.cs`
2. `test/Roslyn.Workbench.Mcp.Plugins.Test/ToolExecutionFailureResultTests.cs`
3. Parts of `test/Roslyn.Workbench.Mcp.Plugins.Test/ToolExecutorTests.cs`

These are closer to the repository standard:

1. Small system under test.
2. Direct collaborator mocking with Moq.
3. No temporary projects or file-system fixtures.
4. Assertions on behaviour rather than full runtime orchestration.

This style should become the default.

### 3. In-process workflow and component tests

This is the largest and most problematic category.

These tests often:

1. Create a temporary project on disk.
2. Open a real workspace.
3. Start transactions.
4. Register full plugins.
5. Execute tools through runtime wrappers.
6. Assert on structured payloads or previews after orchestrating several layers together.

Representative examples:

1. `test/Roslyn.Workbench.Mcp.Workspace.Test/WorkspaceCoordinatorTests.cs`
2. `test/Roslyn.Workbench.Mcp.Workspace.Test/WorkspaceResolverTests.cs`
3. `test/Roslyn.Workbench.Mcp.Test/CodeActionMcpToolTests.cs`
4. `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/InspectionToolsTests.cs`
5. `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/ReplayRefactoringToolsTests.cs`
6. Most `Get*ToolTests`, `Find*ToolTests`, `Analyze*ToolTests`, and refactoring tool tests in `Roslyn.Workbench.Mcp.Plugins.Core.Test`

These tests are useful, but they are not unit tests. They are closer to component or integration tests.

### 4. Audit-ledger and rollout-governance tests

There is a further category that is not really test pyramid coverage at all. It is internal rollout governance for built-in Roslyn code action families.

Representative examples:

1. `test/Roslyn.Workbench.Mcp.TestSupport/BuiltInCodeActionAuditHarness.cs`
2. `test/Roslyn.Workbench.Mcp.TestSupport/BuiltInCodeActionAuditCases.cs`
3. Theory-driven code action promotion tests in `CodeActionMcpToolTests.cs`
4. Theory-driven replay coverage in `ReplayRefactoringToolsTests.cs`

These have value, but they read more like acceptance/audit scripts for supported built-in provider families than unit tests of repository-owned behaviour.

## Evidence

### Fixture-driven synthetic workspaces

`InspectionSampleFixture` creates a large synthetic project and code corpus on disk and is used broadly by higher-level tests. See `test/Roslyn.Workbench.Mcp.TestSupport/InspectionSampleFixture.cs`, especially the fixture creation and file writes around lines 20-82 and the sample code corpus beginning at line 83.

`TestWorkspaceFixture` does the same for workspace lifecycle scenarios, including malformed and ambiguous projects. See `test/Roslyn.Workbench.Mcp.Workspace.Test/TestWorkspaceFixture.cs`, especially lines 33-212.

These helpers are good integration-fixture utilities, but their prevalence is the opposite of a unit-test-first strategy.

### Reflection and surface locks

`WorkspaceCoordinatorTests` starts with reflection-based surface assertions such as `GetInterfaces`, `GetMethod`, and `GetConstructor` checks before moving into behavioural tests. See `test/Roslyn.Workbench.Mcp.Workspace.Test/WorkspaceCoordinatorTests.cs`, lines 16-60.

`PluginSurfaceShapeTests` asserts fully qualified names of public types. See `test/Roslyn.Workbench.Mcp.Plugins.Test/PluginSurfaceShapeTests.cs`, lines 7-18.

`SchemaGenerationTests` validates protocol schema shape by reflecting tool methods and asserting on generated JSON schema fragments. See `test/Roslyn.Workbench.Mcp.Contracts.Test/Schema/SchemaGenerationTests.cs`, lines 9-220.

These tests are not wrong, but they are contract locks, not unit behaviour tests.

### Real runtime orchestration inside tests

`CodeActionMcpToolTests` opens real coordinators, registers plugins, creates snapshot preconditions, and invokes tools through runtime plumbing. See `test/Roslyn.Workbench.Mcp.Test/CodeActionMcpToolTests.cs`, lines 9-220.

`InspectionToolsTests` drives a large cross-section of the bundled plugin tool surface inside one real workspace flow. See `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/InspectionToolsTests.cs`, especially lines 147-253 and the further batches later in the file.

`ReplayRefactoringToolsTests` enumerates built-in tool cases, opens a workspace, starts a transaction, executes mutation tools, and inspects transaction previews. See `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/ReplayRefactoringToolsTests.cs`, lines 10-180.

These are component or integration tests. Treating them as unit tests is inaccurate.

### Shared test runtime layer

`BundledCoreToolTestHarness` and `PluginToolTestHarness` provide orchestration wrappers for coordinators, tools, staging, and JSON payload conversion. See:

1. `test/Roslyn.Workbench.Mcp.TestSupport/BundledCoreToolTestHarness.cs`
2. `test/Roslyn.Workbench.Mcp.TestSupport/PluginToolTestHarness.cs`

This is a signal that the suite has evolved a testing framework for higher-level flows rather than consistently testing collaborators in isolation.

### Cross-project coupling

`Roslyn.Workbench.Mcp.Test.csproj` references `Roslyn.Workbench.Mcp.Plugins.Core.Test` and `Roslyn.Workbench.Mcp.Workspace.Test`, which makes the host test project dependent on other test projects. See `test/Roslyn.Workbench.Mcp.Test/Roslyn.Workbench.Mcp.Test.csproj`, lines 24-29.

This is a smell. Test projects should depend on product code and narrowly-scoped shared test support, not on other test projects.

### Broad internal visibility

Several source projects expose internals to multiple test projects and to `Roslyn.Workbench.Mcp.TestSupport`. See:

1. `src/Roslyn.Workbench.Mcp.Plugins/Roslyn.Workbench.Mcp.Plugins.csproj`, lines 12-37
2. `src/Roslyn.Workbench.Mcp.Plugins.Core/Roslyn.Workbench.Mcp.Plugins.Core.csproj`, lines 13-26
3. `src/Roslyn.Workbench.Mcp.Workspace/Roslyn.Workbench.Mcp.Workspace.csproj`, lines 18-34

Internal visibility is not inherently wrong, but the breadth of exposure supports a white-box, tightly coupled test style.

## What The Current Suite Achieves

The current tests do provide real value.

### It locks contracts and protocol shapes

The contracts and schema tests will catch accidental changes to:

1. Validation rules.
2. Generated MCP schema.
3. Output envelope structure.
4. Public plugin surface naming.

### It provides in-process confidence for high-risk workflows

The workspace and core plugin tests exercise:

1. Workspace open/close/reload lifecycle.
2. Selector resolution.
3. Snapshot preconditions.
4. Tool registration and execution.
5. Transaction preview/staging.
6. Bundled Roslyn code action behaviour.

If these tests pass, there is meaningful evidence that a large slice of the in-process runtime still works.

### It captures behaviour that pure mocks would miss

For Roslyn-backed tools, some behaviours genuinely require real documents, compilations, or workspaces. The suite does cover those paths.

## What The Current Suite Lacks

### 1. A clear test pyramid

The repository does not currently present a clear split between:

1. Fast unit tests.
2. Contract/surface tests.
3. In-process integration tests.
4. Audit or acceptance tests.

Everything effectively lives in one blended layer.

### 2. Default-fast local feedback

Because there is no categorisation strategy, developers cannot easily run just the fast tests during routine work while reserving heavier tests for broader gates.

### 3. True unit seams in key areas

A large share of workspace and plugin-core behaviour is tested by driving the whole coordinator/runtime path rather than by testing smaller services behind interfaces with Moq.

### 4. Behaviour-first tests for business rules

Many tests lock surface shape or complete structured payloads instead of focused behavioural rules. This makes intent less clear and increases incidental coupling.

### 5. Test project boundaries

The current structure encourages coupling between test projects and a growing `TestSupport` runtime layer.

### 6. A consistent classification policy

Some files contain a mix of:

1. Surface/reflection tests.
2. Pure unit tests.
3. Real file-system/workspace tests.

That makes it hard to reason about the cost and purpose of a project.

## Classification By Project

### `Roslyn.Workbench.Mcp.Contracts.Test`

Assessment: strong and mostly healthy, but predominantly contract tests rather than unit tests.

Keep:

1. Validation tests such as `SelectorValidationTests`.
2. DTO/result behaviour tests.

Reclassify conceptually:

1. Schema generation tests as contract tests.

Primary gap:

1. Very little here is problematic, but it should be labelled as contract coverage rather than counted as classic unit coverage.

### `Roslyn.Workbench.Mcp.Plugins.Test`

Assessment: best aligned project overall.

Keep as unit tests:

1. `ToolExecutionFailureResultTests`
2. Much of `ToolExecutorTests`
3. `PluginRegistryTests`
4. `ToolExecutionContextLeaseTests`

Reclassify:

1. `PluginSurfaceShapeTests` as surface/contract tests.
2. `ToolSchemaFactoryTests` as contract tests.

Primary gap:

1. Reduce reflection-only shape locks unless they protect a deliberate compatibility boundary.

### `Roslyn.Workbench.Mcp.Workspace.Test`

Assessment: mostly component/integration tests, not unit tests.

Examples:

1. `WorkspaceCoordinatorTests`
2. `WorkspaceResolverTests`
3. `WorkspaceInputManifestBuilderTests`
4. `WorkspaceDiffBuilderTests`

What to do:

1. Keep a focused subset as integration tests.
2. Break out actual unit tests for state transitions, selectors, manifest building, diff computation, and staging logic behind interfaces where possible.

### `Roslyn.Workbench.Mcp.Plugins.Core.Test`

Assessment: the most overloaded project in the repository.

It currently contains:

1. Tool registration surface checks.
2. Small targeted units.
3. Large bundled inspection workflow tests.
4. Refactoring replay tests.
5. Built-in provider audit coverage.

What to do:

1. Keep small handler/service units here or in a narrower unit project.
2. Move workspace-driven tool execution tests to an integration project.
3. Move provider audit/ledger tests to a dedicated audit or acceptance project.

### `Roslyn.Workbench.Mcp.Test`

Assessment: mixed host tests with some good units and some genuine component/integration tests.

Keep as unit tests:

1. `MsBuildRegistrationHostedServiceTests`
2. Other small service tests that only use Moq and constructor-injected dependencies

Reclassify:

1. `CodeActionMcpToolTests`
2. `InspectionMcpToolTests`
3. `WorkspaceLifecycleToolTests`
4. `WorkspaceStatusToolTests`

These should be treated as host/component tests.

## Recommended Target Taxonomy

The repository should adopt four explicit categories.

### Unit

Definition:

1. No temporary project directories.
2. No real `WorkspaceCoordinator`.
3. No real `InspectionSampleFixture` or `TestWorkspaceFixture`.
4. Collaborators replaced with Moq where normal seams exist.
5. Single behaviour or rule per test.

Examples:

1. Validation logic.
2. Envelope conversion.
3. Registration services.
4. Small tool execution branches.
5. State-machine transitions if expressed behind small abstractions.

### Contract

Definition:

1. Tests that deliberately lock schema, validation, serialisation, or public API surface.
2. Usually fast, but not the same thing as unit tests.

Examples:

1. Schema generation.
2. Public type/surface locks.
3. Contract validator rules.

### Integration

Definition:

1. Uses the real file system, Roslyn workspace, coordinator, plugin registry, transaction flow, or code action runtime.
2. Verifies multi-component behaviour end to end inside the process.

Examples:

1. Workspace lifecycle.
2. Selector resolution against real documents.
3. Tool invocation against a real sample project.
4. Transaction preview/staging flows.

### Audit or Acceptance

Definition:

1. Governance-style tests for built-in provider coverage, provider promotion lists, or supported replay families.
2. Valuable, but not part of the normal unit loop.

Examples:

1. Built-in code action family audit.
2. Replay wrapper coverage over promoted or pending provider families.

## Recommended Project Layout

Recommended end state:

1. Keep `Roslyn.Workbench.Mcp.Contracts.Test` for unit + contract tests in contracts.
2. Keep `Roslyn.Workbench.Mcp.Plugins.Test` for unit + contract tests in plugins.
3. Create `Roslyn.Workbench.Mcp.Workspace.IntegrationTest`.
4. Create `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest`.
5. Create `Roslyn.Workbench.Mcp.IntegrationTest` for host/runtime orchestration.
6. Optionally create `Roslyn.Workbench.Mcp.ProviderAudit.Test` or `Roslyn.Workbench.Mcp.AcceptanceTest` for built-in Roslyn provider audit coverage.

If new projects are too much churn initially, categorise first with traits and then split physically in a second pass.

## Comprehensive Plan To Reach Repo-Compliant Unit Testing

### Phase 1: Define the taxonomy and stop the drift

Actions:

1. Add a short test taxonomy section to `test/AGENTS.md` or a dedicated test strategy document.
2. Define allowed characteristics for `Unit`, `Contract`, `Integration`, and `Audit`.
3. Require new tests that touch the file system, temporary projects, or real coordinators to be marked `Integration` or `Audit`.

Success criteria:

1. No new workspace/file-system orchestration tests land unlabelled.

### Phase 2: Categorise the current suite without rewriting everything

Actions:

1. Add `[Trait("Category", "Integration")]` to workspace/coordinator/runtime tests.
2. Add `[Trait("Category", "Audit")]` to provider audit and replay-ledger tests.
3. Add `[Trait("Category", "Contract")]` to schema and public-surface lock tests where useful.
4. Keep unlabelled or `[Trait("Category", "Unit")]` only for true unit tests.

Initial classification candidates:

1. Mark `WorkspaceCoordinatorTests` and `WorkspaceResolverTests` as `Integration`.
2. Mark `CodeActionMcpToolTests`, `InspectionMcpToolTests`, and `WorkspaceLifecycleToolTests` as `Integration`.
3. Mark most fixture-driven `Get*ToolTests`, `Find*ToolTests`, `Analyze*ToolTests`, and refactoring replay tests in `Roslyn.Workbench.Mcp.Plugins.Core.Test` as `Integration`.
4. Mark built-in code action promotion and replay-ledger tests as `Audit`.
5. Mark schema and public-surface lock tests as `Contract`.

Success criteria:

1. `dotnet test --filter "Category!=Integration&Category!=Audit"` runs the fast default suite.

### Phase 3: Split physical projects along the new boundaries

Actions:

1. Move workspace/file-system/coordinator tests from `Roslyn.Workbench.Mcp.Workspace.Test` into `Roslyn.Workbench.Mcp.Workspace.IntegrationTest`.
2. Move host orchestration tests from `Roslyn.Workbench.Mcp.Test` into `Roslyn.Workbench.Mcp.IntegrationTest`.
3. Move bundled-core real-workspace tool tests from `Roslyn.Workbench.Mcp.Plugins.Core.Test` into `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest`.
4. Move built-in provider audit tests into a dedicated audit project if they remain strategically important.
5. Remove test-project-to-test-project references such as the `Roslyn.Workbench.Mcp.Test` references to `Roslyn.Workbench.Mcp.Plugins.Core.Test` and `Roslyn.Workbench.Mcp.Workspace.Test`.

Success criteria:

1. Unit projects depend only on product projects plus minimal shared helper code.
2. Integration projects own the heavy fixtures.

### Phase 4: Shrink `Roslyn.Workbench.Mcp.TestSupport`

Actions:

1. Keep only genuinely shared low-level helpers.
2. Move heavy runtime orchestration helpers into integration-only projects.
3. Separate simple builders from full runtime harnesses.
4. Avoid adding new fixture types that create whole sample projects unless the test is explicitly integration-level.

Success criteria:

1. `TestSupport` stops behaving like a second application runtime.

### Phase 5: Add missing true unit coverage around production seams

This is the main engineering task.

Focus areas:

1. Workspace state transition services.
2. Selector and snapshot validation rules that can be tested without opening a workspace.
3. Tool registration and execution branching logic.
4. Host bootstrap services and service registration.
5. Error/result mapping helpers.
6. Manifest and diff builders where logic can be exercised from in-memory inputs.

Approach:

1. Prefer constructor-injected collaborators and Moq.
2. Test one behaviour per method.
3. Keep request/response values small and explicit.
4. Avoid widening production contracts purely for tests.
5. If a class is only testable by driving the entire coordinator, treat that as a refactoring signal.

Success criteria:

1. The majority of behavioural coverage for repository-owned logic comes from fast Moq-based tests, not from opening temporary projects.

### Phase 6: Be explicit about what should remain integration coverage

Do not force all of these into unit tests:

1. Real workspace open/reload flows.
2. Roslyn selector resolution against real documents.
3. Real code action discovery and replay against compilations.
4. Transaction preview and multi-document mutation flows.
5. End-to-end tool execution through protocol payloads where the repository explicitly owns the orchestration contract.

Keep them, but label and gate them correctly.

### Phase 7: Introduce a sensible execution policy

Recommended local default:

1. Unit + Contract only.

Recommended PR gate:

1. Unit + Contract always.
2. Integration for touched areas or for all changes if the CI budget allows it.

Recommended scheduled or pre-release gate:

1. Full Integration.
2. Audit/Acceptance provider tests.

Example command strategy:

1. Local fast loop: `dotnet test --filter "Category!=Integration&Category!=Audit" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`
2. Full CI: `dotnet test --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`

## Concrete First Moves

If this work starts immediately, the highest-value first moves are:

1. Add test categories and classify the existing suite without changing logic.
2. Split `Roslyn.Workbench.Mcp.Test` so host unit tests stop sitting beside integration flows.
3. Split `Roslyn.Workbench.Mcp.Workspace.Test` into unit and integration projects.
4. Split `Roslyn.Workbench.Mcp.Plugins.Core.Test` into small unit tests versus real-workspace tool execution tests.
5. Keep and expand good unit examples such as:
   `MsBuildRegistrationHostedServiceTests`
   `ToolExecutionFailureResultTests`
   `SelectorValidationTests`
   small Moq-driven portions of `ToolExecutorTests`

## Bottom Line

The repository already has a lot of useful tests, but it does not currently have a clean unit-test architecture. The suite should be described as:

1. Strong in contract locking.
2. Strong in in-process workflow coverage.
3. Weak in taxonomy and separation.
4. Underweight in true seam-driven unit tests for several important areas.

The right fix is not to delete the heavier tests. The right fix is to:

1. Name them honestly.
2. Move them to the right layer.
3. Stop counting them as unit tests.
4. Rebuild the fast default suite around Moq-driven collaborator tests in line with the repository guidance.

## External References

The recommendations above align with current Microsoft guidance that:

1. Unit tests should isolate small units of behaviour.
2. If behaviour can be covered by either a unit test or an integration test, choose the unit test.
3. Unit and integration tests should be separated so developers can control what runs.

References:

1. https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices
2. https://learn.microsoft.com/dotnet/core/testing/unit-testing-csharp-with-xunit
3. https://learn.microsoft.com/aspnet/core/test/integration-tests?view=aspnetcore-10.0
