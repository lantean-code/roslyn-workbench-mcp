# Testing Strategy

Date: 2026-07-10

## Purpose

This is the canonical testing strategy for the current production architecture. It replaces the execution model assumed by `IntegrationTestReorganisationPlan.md`, which is retained only as a record of the completed reorganisation.

The strategy keeps fast behavioural tests close to the assembly that owns the behaviour, uses integration tests only for real component or infrastructure boundaries, and reserves audit tests for version-sensitive Roslyn compatibility governance.

## Production Boundaries Under Test

The production dependency direction is:

```text
Host -> CodeActions -> Workspace
Host -> Plugins.Core -> Plugins -> Workspace
Host -> Plugins -> Workspace
Host -> Workspace
```

The test architecture must protect these additional rules:

- Workspace has no dependency on Plugins, CodeActions or MCP transport.
- CodeActions is an internal tool system and has no dependency on Plugins or the MCP SDK.
- Plugins is the public third-party extension system and has no dependency on CodeActions or the MCP SDK.
- Host alone binds MCP requests, constructs MCP tools and publishes protocol results.
- Contracts live with their owning production assembly; there is no shared Contracts production or test project.
- Plugin and Code Action registrations preserve their closed generic request and response types through typed visitors.
- Query contexts cannot stage mutations, and mutation handlers do not receive the final Workspace stager.
- Code Action names are reserved before plugin discovery, and Code Actions are not reported as plugins.

## Test Layers

### Unit

Unit tests are the default. They isolate production collaborators with Moq and exercise behaviour through supported entry points. In-memory Roslyn objects may be used as data when syntax, semantic, compilation or solution behaviour cannot be represented faithfully by mocks.

Unit tests must not open a real workspace, use temporary projects, touch the filesystem, build the production host, execute an MCP transport flow or compose a real multi-assembly execution pipeline.

Each owning unit project is responsible for reachable line and branch coverage of its implementation:

| Production owner | Unit-test responsibility |
| --- | --- |
| Workspace | Domain models with behaviour, selection, acquisition results, leases, state, resolver algorithms and staging behaviour that can be isolated |
| Plugins | Public plugin surface, typed registration/visitor dispatch, Workspace context adaptation, plugin services, result and proposal mapping |
| Plugins.Core | Inspection and ordinary mutation handler branches plus owned DTO validation |
| CodeActions | Internal catalogue, typed registration/visitor dispatch, context adaptation, workflows, tokens, result mapping and Code Action handlers |
| Host | Host services, lifecycle tools, Host-owned contracts, schemas, binding, publication and all four MCP adapters |

### Contract

Contract tests lock an intentional externally observable boundary such as request/response JSON, schema, validation, MCP metadata or the supported public plugin API.

They live in the owning unit-test project and use `Category=Contract`. Reflection is permitted only for a deliberate public surface or metadata contract. It must not be used to invoke implementation code, lock internal runtime shapes or substitute for behavioural tests.

### Integration

Integration tests prove behaviour that requires real boundaries, including:

- MSBuild and filesystem-backed workspace loading
- Workspace lifecycle, selection and transaction persistence
- real Roslyn cross-project and semantic behaviour
- plugin package enumeration, PE-metadata discovery, MEF composition and load-context routing
- Host dependency-injection and MCP composition
- MCP binding and result publication across an execution path
- controlled Code Action provider discovery and staging

Integration tests are capability-focused. A production tool does not need a same-named integration test when its unit tests cover its branches and an existing capability test proves the real boundary it depends upon.

### Audit

Audit tests govern the supported built-in Roslyn provider catalogue and replay families. They are compatibility checks, not unit branch coverage or general integration tests. They run outside the default development loop.

## Execution-Path Coverage

The refactor created four distinct Host transport paths. Each path needs focused Host unit coverage plus representative integration composition:

| Path | Owning handler/context tests | Host adapter tests | Integration evidence |
| --- | --- | --- | --- |
| Plugin query | Plugins and Plugins.Core | request binding, acquisition, handler result/failure, cancellation, exception and publication | plugin discovery and representative MCP invocation |
| Plugin mutation | Plugins and Plugins.Core | proposal, no-change/failure, separate staging, staged result, cancellation and exception | real mutation staging through MCP |
| Code Action query | CodeActions | request binding, Code Action context acquisition, result/failure, cancellation, exception and publication | controlled-provider list/describe flow and Host composition |
| Code Action mutation | CodeActions | proposal, no-change/failure, separate staging, staged result, cancellation and exception | controlled-provider staging and representative built-in staging |

The shared Host MCP base is tested once for common argument, cancellation and safe-exception behaviour. Path-specific acquisition, result mapping and staging remain tests of each closed generic adapter.

## Boundary-Regression Coverage

Architecture assertions should be behavioural or project-reference based wherever possible. The required regression set is:

- project references follow the production dependency graph
- only Host references the MCP SDK
- CodeActions does not participate in plugin discovery or expose plugin metadata
- plugin and Code Action query/mutation registrations dispatch to the correct typed visitor overload
- Workspace execution contexts expose only neutral Workspace capabilities
- plugin contexts add only plugin execution services
- Code Action contexts expose only invocation-specific Workspace execution state; stable Code Action services are constructor-injected into their handlers
- staging remains on the mutation lease, not the handler context
- duplicate internal Code Action names fail catalogue construction
- plugins colliding with reserved Code Action or existing plugin names are disabled with diagnostics
- external packages with duplicate plugin IDs or shared tool names are all disabled deterministically
- Plugins.Core follows the same MEF configuration and materialisation path while remaining in the default load context
- Host composes all four adapter families
- `server-status` excludes CodeActions from plugin status

Do not add reflection-only tests for internal interface shape. If a least-privilege boundary cannot be demonstrated through compilation, assignability, public behaviour or project references, improve the production seam before adding a brittle shape lock.

## Project and Category Layout

The current project map, test counts and remaining gaps are maintained in `TestArchitectureReaudit-2026-07-10.md`. Tool and capability ownership is maintained in `Tool Test Inventory.md`. The deferred broad Workspace programme is maintained in `WorkspaceUnitTestInventory.md`.

Category policy:

- normal `*.Test` assemblies contain Unit and Contract tests
- `*.IntegrationTest` assemblies are categorised Integration at assembly level
- `*.AuditTest` assemblies are categorised Audit at assembly level
- test-support and fixture projects are not test assemblies

## Coverage Policy

New or materially changed unit-testable implementation requires 100% line and branch coverage unless an exact unreachable defensive branch is approved and recorded in the active inventory. Repository-wide or assembly-wide percentages do not replace class-level evidence, and integration execution does not count as proof of unit isolation.

Coverage reports must be used to find gaps after tests are written; production code must not gain test-only hooks, broader runtime interfaces or artificial reflection paths merely to increase a percentage.

## Execution Policy

Fast development loop:

```bash
dotnet test --filter "Category!=Integration&Category!=Audit" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

Run the affected integration project after changes to a real boundary. Run the Code Action audit when Roslyn dependencies, provider classification, replay behaviour or Code Action discovery changes. Run the full suite before completion of behaviour-affecting work.

Documentation-only changes do not require restore, build or test execution.
