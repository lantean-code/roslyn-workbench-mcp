# Future Tasks

Date: 2026-07-18

## Purpose

This document consolidates genuinely unfinished work recorded across the repository documentation and orders it for progression towards a production release. It excludes completed work, checklist templates, superseded unchecked checklists whose outcomes are present in the repository, and guardrails that do not request a change.

Priority bands mean:

- **P0 — Release decision or blocker**: resolve before release, or explicitly accept and document the residual risk.
- **P1 — Production confidence**: high-value evidence or hardening that should normally precede release.
- **P2 — Release support and ecosystem**: important for the supported-platform or extension story, but not necessarily a core release blocker.
- **P3 — Engineering efficiency**: valuable improvements that should not delay the product release.
- **Conditional**: do not schedule unless the stated external capability or product need exists.

Statuses mean:

- **Started**: investigation, infrastructure or partial implementation already exists, but the intended outcome is not complete.
- **Not started**: the task is recorded, but no implementation work is evidenced.
- **Conditional**: the task depends on an external capability or concrete product requirement.

When a task is completed, remove it from this document rather than retaining a completed entry.

## P0 — Release Decisions and Blockers

### Reconcile the planned tool catalogues with the shipping surface

**Status:** Not started

`RoslynMcpToolDesign.md` and `RoslynMcpToolContracts.md` still describe several tools as deferred even though the bundled plugin or Code Action catalogue now registers them. Audit the published tool list and update the catalogue, contract execution-surface note, counts and implementation matrix together. In particular, verify the nine analysis tools formerly listed as deferred plus `move-type-to-file` and `convert-property`.

Accurate public contracts and capability documentation are required before release; users and agents must not be told that shipping tools are unavailable.

Sources: [RoslynMcpToolDesign.md](RoslynMcpToolDesign.md#current-execution-surface-note-2026-07-02), [RoslynMcpToolContracts.md](RoslynMcpToolContracts.md#current-execution-surface-note-2026-07-02), [RoslynMcpToolImplementationMatrix.md](RoslynMcpToolImplementationMatrix.md)

### Close or supersede the original whole-project roadmap

**Status:** Started

The original roadmap still leaves Stage 7 (catalogue expansion) and Stage 8 (hardening and release readiness) unchecked. Much of both stages is now implemented through later architecture, unit-coverage, integration, compatibility and acceptance programmes. Re-audit the roadmap against the current repository, link the later evidence, and retain only genuine release-readiness gaps rather than treating its stale stage boxes as an executable checklist.

Closing this provides the explicit release-readiness decision after the architecture and test programmes.

Source: [2026-06-24-whole-project-roadmap-design.md](superpowers/specs/2026-06-24-whole-project-roadmap-design.md#internal-stages)

## P1 — Production Confidence and Performance

### Establish and execute a tool performance-tuning programme

**Status:** Not started

Measure representative MCP tools before optimising them. Build a repeatable benchmark and profiling baseline across query, mutation and Code Action families using small, medium and realistically large checked-in workspaces. Record end-to-end latency, Roslyn execution time, allocations, peak memory and result size, then prioritise measured hot paths rather than applying speculative changes.

The programme should:

- identify the slowest and most allocation-heavy tools and shared helpers;
- distinguish Host/MCP overhead from Workspace loading, Roslyn analysis, result projection and serialisation;
- measure cold and warm execution where caches materially affect behaviour;
- include broad solution search, graph/flow analysis, diagnostics, mutation preview and Code Action discovery/staging;
- assess cancellation responsiveness and bounded-result behaviour on large result sets;
- use BenchmarkDotNet for isolated repeatable hot paths and profiling or trace collection for realistic end-to-end executions;
- preserve result ordering, contracts, snapshot semantics and transaction safety while tuning; and
- record comparative evidence without introducing brittle elapsed-time assertions into functional tests.

Re-run the baseline after material Roslyn, MSBuild or MCP SDK upgrades and retain dated results so regressions can be distinguished from environment variance.

### Reassess remaining partial tool branch coverage

**Status:** Started

The coverage round measured and classified the remaining branches. Run a fresh assembly-level report, add tests for reachable behaviour, and explicitly approve defensive branches that cannot occur through supported Roslyn flows. Do not add reflection or production test hooks solely to reach them.

Current cases:

- reachable `FindUnusedSymbolsTool` accessibility-filter combinations;
- additional `GetApiSurfaceTool` declared-symbol, accessibility and attribute combinations;
- `GetCodeMetricsTool` delegate/nesting paths and its missing-source-location guard;
- defensive missing syntax-root or semantic-model paths in `GetControlFlowGraphTool` and `GetOperationTreeTool`;
- the defensive null arm in `GetDiagnosticsTool.DiagnosticComparer`;
- a non-null, non-C# parse-options case in `GetDocumentOptionsTool`;
- the same-solution no-change path in `RenameSymbolTool`; and
- defensive null handling for `UsingDirectiveSyntax.Name` in `SortUsingsTool`.

Source: [Tool Test Inventory.md](Tool%20Test%20Inventory.md#known-partial-branch-coverage)

## P2 — Release Support and Plugin Ecosystem

### Add a plugin-authoring analyser

**Status:** Not started

Create an analyser for handler contract and lifetime rules that are useful during plugin development but cannot be proven safely by runtime structural heuristics. Generic constraints remain authoritative for family membership and construction eligibility; runtime validation remains authoritative for objectively detectable package and handler errors.

This should precede a release that actively promotes third-party plugin authoring, but it need not block a Host release that clearly labels the plugin authoring experience and runtime validation guarantees.

Source: [2026-07-13-mef-plugin-composition.md](superpowers/plans/2026-07-13-mef-plugin-composition.md)

### Decide whether macOS should gate pull requests

**Status:** Started

Scheduled macOS Workspace integration and published-Host acceptance coverage is configured. Gather hosted-runner reliability and runtime evidence, then decide whether to promote macOS to a pull-request gate. The release support matrix should state whether macOS is supported, best-effort or unverified independently of the gating decision.

Sources: [TestArchitectureReaudit-2026-07-18.md](TestArchitectureReaudit-2026-07-18.md#deferred-decisions), [IntegrationTestingStage7Results-2026-07-18.md](IntegrationTestingStage7Results-2026-07-18.md#platform-evidence)

## P3 — Engineering Efficiency

### Migrate to Microsoft.Testing.Platform v2

**Status:** Started

The Stage 7 evaluation established the migration shape and measured a modest improvement. VSTest remains selected while xUnit 4 is prerelease. Revisit after xUnit 4 is stable, then validate executable test modules, filtering, TRX/reporting, coverage, IDE support, minimum-count enforcement and CI commands as one isolated runner change.

This should not delay the product release because the current VSTest suite is complete and reliable.

Sources: [TestingStrategy.md](TestingStrategy.md#continuous-integration), [IntegrationTestingStage7Results-2026-07-18.md](IntegrationTestingStage7Results-2026-07-18.md#microsofttestingplatform-evaluation)

### Decide the NuGet lock-file and caching policy

**Status:** Not started

Decide whether the repository should adopt `packages.lock.json` files. Enable `setup-dotnet` package caching only if the approved dependency policy makes cache keys and restore behaviour reliable.

Sources: [IntegrationTestingImplementationPlan.md](IntegrationTestingImplementationPlan.md#decisions-deliberately-deferred), [IntegrationTestingStage7Results-2026-07-18.md](IntegrationTestingStage7Results-2026-07-18.md#nuget-caching-decision)

### Evaluate collection-scoped concurrent Workspace reuse

**Status:** Not started

Prove thread safety before sharing an expensive read-only Workspace fixture across a test collection. Mutable workspaces and state roots must remain scenario-isolated. This is a test-feedback optimisation and should not delay release.

Source: [IntegrationTestingImplementationPlan.md](IntegrationTestingImplementationPlan.md#decisions-deliberately-deferred)

### Replace source-governance tests with analysers

**Status:** Not started

Assess whether repository source-policy tests should become compile-time analysers. Keep compatibility audit ownership separate and do not remove an existing governance check until equivalent analyser enforcement exists.

Source: [IntegrationTestingImplementationPlan.md](IntegrationTestingImplementationPlan.md#decisions-deliberately-deferred)

## Conditional Capability Backlog

### Reconsider mutation families blocked by Roslyn APIs

**Status:** Conditional

The following aspirational families are deliberately not planned against the current public Roslyn surface. Reconsider an item only when Roslyn exposes a supported non-IDE-only API or diagnostics path that can satisfy its documented contract:

- `move-type-to-namespace`;
- `convert-to-async`;
- `convert-to-pattern-matching`;
- `generate-constructor`;
- `generate-tostring`;
- `extract-interface`;
- `extract-base-class`;
- `change-signature`;
- `generate-equals-hashcode`;
- `generate-overrides`; and
- `implement-interface`.

Sources: [RoslynMcpToolDesign.md](RoslynMcpToolDesign.md#current-execution-surface-note-2026-07-02), [RoslynMcpToolContracts.md](RoslynMcpToolContracts.md#current-execution-surface-note-2026-07-02), [RoslynMcpToolImplementationMatrix.md](RoslynMcpToolImplementationMatrix.md)

### Support additional MEF plugin module assemblies

**Status:** Conditional

External packages currently have one marked entry assembly and ordinary dependency assemblies. Add separately composed MEF module assemblies only when a concrete plugin-module use case establishes the discovery, identity, isolation and collision requirements.

Source: [2026-07-13-mef-plugin-composition.md](superpowers/plans/2026-07-13-mef-plugin-composition.md)
