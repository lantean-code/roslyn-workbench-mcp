# Future Tasks

Date: 2026-07-20

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

No open P0 release decisions or blockers remain from the documentation audit.

## P1 — Production Confidence and Performance

### Triage and remediate the solution-wide .NET analyzer baseline

**Status:** Started

The complete `latest-all` baseline began with 2,205 findings across 379 files and 22 projects. The current inventory contains 546 findings across 178 files; eighteen diagnostic families are resolved. Remediate production async and performance findings in cohesive batches, then establish any broader test policy deliberately. Do not apply API-design or concrete-type suggestions mechanically where they would weaken public contracts, plugin discovery or architectural boundaries.

Complete the high-priority production batches before recording performance baselines because analyzer-driven changes can affect allocations, cancellation and execution timing.

Source: [Analyzer Inventory.md](Analyzer%20Inventory.md)

### Establish and execute a tool performance-tuning programme

**Status:** Framework implemented; baseline pending

Use the permanent manual runner under `tools/Roslyn.Workbench.Mcp.Performance` to establish the current implementation baseline across query, mutation and Code Action families. Its pinned small, medium and realistically large GitHub repositories are cached beneath the gitignored `artifacts/performance` directory; scenario definitions and measurement guidance remain in the repository. Record end-to-end latency, Host CPU, runtime counters, peak memory and result size, then use traces or heap captures to investigate measured hot paths rather than applying speculative changes.

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

Source: [Core Tool Performance Audit — 2026-07-19](CoreToolPerformanceAudit-2026-07-19.md)

### Evaluate snapshot-scoped cross-invocation query caching

**Status:** Conditional

If performance measurements show meaningful repeated Roslyn discovery across successive tool invocations, design a Workspace-owned query cache that is separate from request-local lookup caches. Start with operations such as reference discovery that currently obtain a complete lightweight result before applying a response bound, allowing a later request with a larger bound to reuse the same discovery safely.

Use a dedicated size-limited cache rather than the Host's general-purpose cache. Keys must include workspace and snapshot identity plus the canonical operation target and semantic options. Cached values must record whether discovery is complete or only covers a known limit; a larger request may reuse an entry only when the cached result is complete or already covers that request. Do not include presentation-only response bounds in keys when a complete ordered discovery result is cached.

Define explicit entry sizing, expiration and invalidation for workspace close, reload, commit and snapshot advancement. Do not cache cancelled or failed operations, and do not allow cached Roslyn objects or projected results to outlive the snapshot against which they were produced. Introduce supported operations individually with measurements for hit rate, retained memory and latency rather than making every tool automatically cacheable.

Source: [Core Tool Performance Audit — 2026-07-19](CoreToolPerformanceAudit-2026-07-19.md)

## P2 — Release Support and Plugin Ecosystem

### Add a plugin-authoring analyser

**Status:** Not started

Create an analyser for handler contract and lifetime rules that are useful during plugin development but cannot be proven safely by runtime structural heuristics. Generic constraints remain authoritative for family membership and construction eligibility; runtime validation remains authoritative for objectively detectable package and handler errors.

Include diagnostics that protect the immutable session-snapshot model without restricting legitimate Roslyn queries:

- report direct calls to `Workspace.TryApplyChanges` as errors because they bypass mutation validation, staging, revision history and commit handling;
- report use of `Workspace.CurrentSolution` when it replaces the request's `CurrentSolution`, because the live Workspace may have advanced beyond the session snapshot;
- continue to permit normal reads and immutable transformations through the request's `CurrentSolution`; and
- permit access to its associated `Workspace` when a Roslyn query API genuinely requires it rather than prohibiting the property itself.

Ship the analyser with the plugin-authoring package so diagnostics run in the IDE and at build time. Treat it as an engineering guardrail rather than a security boundary: plugins remain trusted in-process code and deliberate suppression, reflection or indirection cannot be prevented by an analyser.

Pair the authoring diagnostics with Host-side detection of unexpected live Workspace changes. First establish whether the existing snapshot and external-change guards already cover an uncoordinated `TryApplyChanges`; add focused coverage or invalidation behaviour where necessary so an affected session becomes stale or conflicted instead of continuing against divergent state. Detection is containment after a change, not permission for plugins to mutate the live Workspace.

This should precede a release that actively promotes third-party plugin authoring, but it need not block a Host release that clearly labels the plugin authoring experience and runtime validation guarantees.

Sources: [2026-07-13-mef-plugin-composition.md](superpowers/plans/2026-07-13-mef-plugin-composition.md), [PluginApiSurfaceAudit-2026-07-18.md](PluginApiSurfaceAudit-2026-07-18.md)

### Minimise the public surface of built-in tool contracts

**Status:** Not started

Audit the request and response DTOs for built-in tools and internalise types that are public only as an implementation convenience. Start with Code Action contracts: these tools are published through the Host-owned catalogue rather than the third-party plugin pipeline, so their request types should not need to form part of the supported public API.

Bundled `Plugins.Core` tools currently pass through the same contract resolver as third-party plugins, and that resolver requires public request and response types. Preserve the public-contract requirement for external plugins. Before internalising bundled-plugin DTOs, separate trusted bundled-tool contract validation and metadata publication from external-plugin validation, or introduce an equally explicit bundled-only path that cannot weaken validation of third-party contracts.

Keep the emitted MCP schemas and request binding behaviour unchanged, cover the intended exported type set with API-surface tests, and include response DTOs in the audit so the visibility policy remains consistent. This is public API hygiene rather than a security boundary and should not delay the core Host release.

Source: [PluginApiSurfaceAudit-2026-07-18.md](PluginApiSurfaceAudit-2026-07-18.md)

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
