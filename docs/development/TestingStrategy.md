# Testing Strategy

Date: 2026-07-18

## Purpose

This is the canonical testing strategy for the current production architecture. It replaces the execution model assumed by `IntegrationTestReorganisationPlan.md`, which is retained only as a record of the completed reorganisation.

The strategy keeps fast behavioural tests close to the assembly that owns the behaviour, uses integration tests only for real component or infrastructure boundaries, and reserves audit tests for version-sensitive Roslyn compatibility governance.

## Production Boundaries Under Test

The production dependency direction is:

```text
Host -> CodeActions -> Workspace -> Abstractions
Host -> Plugins.Core -> Plugins -> Workspace -> Abstractions
Host -> Plugins -> Workspace -> Abstractions
Host -> Workspace
```

The test architecture must protect these additional rules:

- Abstractions has no dependency on a production implementation assembly and uses only the minimal Roslyn Workspaces package required by its public signatures.
- Workspace has no dependency on Plugins, CodeActions or MCP transport.
- CodeActions is an internal tool system and has no dependency on Plugins or the MCP SDK.
- Plugins is the public third-party extension system and has no dependency on CodeActions or the MCP SDK.
- Host alone binds MCP requests, constructs MCP tools and publishes protocol results.
- Implementation contracts live with their owning production assembly. Abstractions is the deliberately narrow shared third-party authoring boundary rather than a general shared Contracts project.
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
| CodeActions | Provider composition, exception policy, typed registration/visitor dispatch, context adaptation, discovery, replay references, Fix All, staging and result mapping |
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

Audit tests govern the composed built-in Roslyn provider inventory and replay families. They are compatibility checks, not source-governance checks, unit branch coverage or general integration tests. Source-governance checks live in the fast architecture suite. Compatibility audits run outside the default development loop.

## Execution-Path Coverage

The refactor created four distinct Host transport paths. Each path needs focused Host unit coverage plus representative integration composition:

| Path | Owning handler/context tests | Host adapter tests | Integration evidence |
| --- | --- | --- | --- |
| Plugin query | Plugins and Plugins.Core | request binding, acquisition, handler result/failure, cancellation, exception and publication | plugin discovery and representative MCP invocation |
| Plugin mutation | Plugins and Plugins.Core | proposal, no-change/failure, separate staging, staged result, cancellation and exception | real mutation staging through MCP |
| Code Action query | CodeActions | request binding, Code Action context acquisition, result/failure, cancellation, exception and publication | controlled-provider discovery and Fix All preparation plus Host composition |
| Code Action mutation | CodeActions | proposal, no-change/failure, Host transaction staging, staged result, cancellation and exception | controlled-provider single/prepared staging and representative built-in staging |

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
- duplicate internal Code Action names fail startup tool composition
- plugins colliding with reserved Code Action or existing plugin names are disabled with diagnostics
- external packages with duplicate plugin IDs or shared tool names are all disabled deterministically
- Plugins.Core follows the same MEF configuration and materialisation path while remaining in the default load context
- Host composes all four adapter families
- `server-status` excludes CodeActions from plugin status

Do not add reflection-only tests for internal interface shape. If a least-privilege boundary cannot be demonstrated through compilation, assignability, public behaviour or project references, improve the production seam before adding a brittle shape lock.

## Project and Category Layout

The current project map, final test counts and architecture findings are maintained in `TestArchitectureReaudit-2026-07-18.md`. The earlier `TestArchitectureReaudit-2026-07-10.md` is historical evidence. Tool and capability ownership is maintained in `Tool Test Inventory.md`; detailed unit-coverage dispositions remain in the owning inventories.

The suite has five Unit/Contract projects, four component-integration projects, one published-Host acceptance project and one compatibility-audit project. Acceptance has no production project reference or internal access: it launches the published executable, communicates through the official MCP client over stdio and copies checked-in assets into an isolated scenario root. Component integration constructs only the owner boundary under test and does not reproduce Host transport composition.

Category policy:

- normal `*.Test` assemblies contain Unit and Contract tests
- `*.IntegrationTest` assemblies are categorised Integration at assembly level
- `*.AuditTest` assemblies are categorised Audit at assembly level
- test-support and fixture projects are not test assemblies

## Coverage Policy

New or materially changed unit-testable implementation requires 100% line and branch coverage unless an exact unreachable defensive branch is approved and recorded in the active inventory. Repository-wide or assembly-wide percentages do not replace class-level evidence, and integration execution does not count as proof of unit isolation.

Coverage reports must be used to find gaps after tests are written; production code must not gain test-only hooks, broader runtime interfaces or artificial reflection paths merely to increase a percentage.

Coverage uses the native Coverlet extension for Microsoft.Testing.Platform. Run an affected project with `--coverlet --coverlet-output-format cobertura`; use `--results-directory` to place its timestamped report files under an explicit coverage directory.

## Execution Policy

Fast development loop:

```bash
dotnet test --project <affected-non-acceptance-test-project> --filter "Category!=Integration&Category!=Audit"
```

Run the affected integration project after changes to a real boundary. Run the Code Action audit when Roslyn dependencies, provider classification, replay behaviour or Code Action discovery changes. CI runs that audit on a weekly schedule, by manual dispatch and during release preparation; Roslyn dependency updates should invoke the manual workflow rather than making the audit part of ordinary pull-request or push validation. Run the full suite before completion of behaviour-affecting work.

Documentation-only changes do not require restore, build or test execution.

## Continuous Integration

Pull-request CI separates routine fast coverage from explicitly requested or release-boundary validation:

- the fast Ubuntu job runs Unit and Contract tests after one restore and build for changes entering `develop`, `release/*`, `hotfix/*` or `main`;
- applying `validation/full` to a pull request adds the four Ubuntu component-integration jobs and Ubuntu and Windows published-Host acceptance jobs;
- pull requests targeting `main` receive that broader integration and acceptance validation without requiring the label; and
- manually dispatched CI can run the same broader validation when evidence is needed outside a pull request.

macOS is a best-effort release platform, not a pull-request gate. Do not run recurring macOS jobs while the repository is private. When public v1 release-candidate preparation begins, release-branch or manually dispatched validation should run published-Host acceptance, Workspace integration and a curated external-repository scenario subset on macOS. macOS failures inform the support statement and release decision without redefining the authoritative Windows and Linux gates.

Tests run with `--no-build --no-restore` after their job has produced the required outputs. The fast job selects the six Unit/Contract executables with `--test-modules` before applying the defensive category filter because MTP correctly treats a selected module with zero matching tests as an error. Every test job writes structured TRX results, uses Microsoft.Testing.Platform's native minimum expected test count and uploads those results even when testing fails. Roslyn/MSBuild test runs use bounded MTP hang detection without retaining memory dumps. Failed acceptance runs additionally retain and upload the Host's stderr, process details and isolated scenario state.

The Code Action compatibility audit remains a separate workflow because it is slower, version-sensitive coverage rather than part of the normal component-integration path.

## Release validation and performance history

Published-Host acceptance runs on native Ubuntu and Windows when a maintainer requests `validation/full`, when a pull request targets `main`, or as part of a manually dispatched release. It uses small checked-in fixtures and deterministic public MCP workflows. The external-repository scenario runner is a separate release-validation system: run it only from release branches or by explicit manual dispatch, not for ordinary pull requests, pushes or recurring schedules. Once the repository is public, release validation may include a curated macOS scenario subset as best-effort evidence.

Scenario output has two retention levels:

- detailed JSON, summaries, validation files, traces, counters and heap captures are workflow artifacts for diagnosis and short-term release evidence; and
- a versioned normalised metrics aggregate plus its previous-release comparison are durable GitHub release assets.

GitHub Actions artifacts are not the permanent performance history because their retention is bounded by repository or organisation policy. The final aggregate is attached to its release and becomes the baseline downloaded by the next release run. Generated metrics are not committed to `main`.

The aggregate records enough identity to prevent invalid comparisons: Host commit and version, scenario-suite hash, pinned target-repository commits, command and parameters, operating system, architecture, .NET runtime and sample counts. Compare only like-for-like observations. Display runner or scenario drift explicitly.

Performance comparisons are advisory until several releases establish normal variance on comparable runners. Correctness, repository cleanliness, recovery-state cleanup and Host shutdown remain release-gating outcomes. Raw diagnostic captures are retained beyond workflow-artifact expiry only when they support a release decision or investigation.

The detailed acceptance gaps and dependency-ordered implementation batches are recorded in [Published Host Acceptance Coverage Audit](AcceptanceCoverageAudit-2026-07-23.md).

Microsoft.Testing.Platform v2 is the selected solution-wide runner under .NET 10. Every xUnit 4 test project is an executable MTP module; xUnit's MTP filter compatibility preserves the established category expressions, while native MTP extensions provide TRX reporting, minimum-count enforcement, hang handling and Coverlet coverage.

The repository does not use `packages.lock.json`. Release reproducibility comes from the release tag, GitVersion-derived artifact version, pinned SDK, centrally managed exact direct dependency versions and retention of the exact artifacts produced by the release workflow. NuGet package caching remains disabled; restore performance must become a measured problem before either caching or its supporting dependency policy is reconsidered.
