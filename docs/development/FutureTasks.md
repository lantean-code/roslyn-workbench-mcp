# Future Tasks

Date: 2026-07-23

## Purpose

This document is the single active engineering backlog for work already justified by the release contract, current evidence or an explicit product decision. Historical plans and audits may retain unchecked or deferred text for context, but an item is actionable only when it appears here.

Completed work, superseded checklists, approved defensive coverage gaps and standing guardrails are excluded. Trigger-based ideas are kept separately from scheduled work so they are not mistaken for current commitments.

Priority bands mean:

- **P0 — Release decision or blocker**: resolve before release, or explicitly accept and document the residual risk.
- **P1 — Production confidence**: high-value evidence or hardening that should normally precede release.
- **P2 — Release support and ecosystem**: important for the supported-platform or extension story, but not necessarily a core release blocker.
- **P3 — Engineering efficiency**: valuable improvements that should not delay the product release.
- **Conditional**: do not schedule unless the stated external capability, evidence threshold or product need exists.

Statuses mean:

- **Started**: investigation, infrastructure or partial implementation already exists, but the intended outcome is not complete.
- **Not started**: the task is recorded, but no implementation work is evidenced.
- **Conditional**: the task has a named trigger and is not active until that trigger occurs.

When a task is completed, remove it from this document rather than retaining a completed entry.

## P0 — Release Decisions and Blockers

No open P0 release decisions or blockers remain from the documentation audit.

## P1 — Production Confidence and Performance

### Expand published-Host acceptance coverage

**Status:** Not started

Implement the dependency-ordered release-capability batches covering the exact published artifact, configuration, discovery, public result contracts, valid and failing external packages, supported Workspace open shapes, selector families, every production query and mutation execution path, transaction history, durability, restart, protocol cancellation, concurrency and failure containment.

Keep the acceptance project independent of production references and drive only the Release-published executable through the public MCP protocol. Use small checked-in fixtures, deterministic synchronization and the existing Ubuntu/Windows pull-request matrix. Do not move repository-scale, timing-sensitive or destructive release scenarios into pull-request acceptance.

Source: [Published Host Acceptance Coverage Audit](AcceptanceCoverageAudit-2026-07-23.md)

### Automate release scenario validation and performance history

**Status:** Not started

Add a release-branch and manual-dispatch workflow for the existing external-repository scenario runner after the repository's release-branch naming convention is selected. Produce a versioned normalised metrics aggregate, compare it with the previous GitHub release asset and publish an advisory Markdown regression report.

Upload detailed scenario output as temporary workflow artifacts. Attach the final aggregate and comparison to the GitHub release for durable release-to-release history. Do not commit generated metrics to `main`, and do not introduce hard timing gates until repeated comparable release runs establish normal variance.

Implementation order:

1. Select and document the release-branch naming convention.
2. Define and emit the versioned aggregate and scenario-suite identity.
3. Add compatible previous-release comparison and Markdown reporting.
4. Add manual and release-branch workflow orchestration.
5. Attach the final aggregate and comparison to the GitHub release.

Source: [Testing Strategy](TestingStrategy.md#release-validation-and-performance-history), [Published Host Acceptance Coverage Audit](AcceptanceCoverageAudit-2026-07-23.md#release-only-scenario-validation-and-metrics)

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

This must precede a release that actively promotes third-party plugin authoring. It does not block a Host release that continues to describe plugins as trusted in-process extensions and states the current runtime guarantees.

Sources: [2026-07-13-mef-plugin-composition.md](superpowers/plans/2026-07-13-mef-plugin-composition.md), [PluginApiSurfaceAudit-2026-07-18.md](PluginApiSurfaceAudit-2026-07-18.md)

### Decide whether macOS should gate pull requests

**Status:** Started

Scheduled macOS Workspace integration and published-Host acceptance coverage is configured. Gather hosted-runner reliability and runtime evidence, then decide whether to promote macOS to a pull-request gate. Record the decision in the release support matrix as supported, best-effort or unverified; the support statement must not be inferred from whether CI gates pull requests.

Sources: [TestArchitectureReaudit-2026-07-18.md](TestArchitectureReaudit-2026-07-18.md#deferred-decisions), [IntegrationTestingStage7Results-2026-07-18.md](IntegrationTestingStage7Results-2026-07-18.md#platform-evidence)

## P3 — Engineering Efficiency

### Decide the NuGet lock-file and caching policy

**Status:** Not started

Decide whether the repository should adopt `packages.lock.json` files. Enable `setup-dotnet` package caching only if the approved dependency policy makes cache keys and restore behaviour reliable. Treat dependency reproducibility as the decision driver; do not adopt lock files solely to enable CI caching.

Sources: [IntegrationTestingImplementationPlan.md](IntegrationTestingImplementationPlan.md#decisions-deliberately-deferred), [IntegrationTestingStage7Results-2026-07-18.md](IntegrationTestingStage7Results-2026-07-18.md#nuget-caching-decision)

## Conditional Backlog

These items are intentionally inactive. Move one into the appropriate priority band only when its stated trigger occurs.

### Migrate to Microsoft.Testing.Platform v2

**Status:** Conditional

**Trigger:** xUnit 4 reaches a stable release with supported Microsoft.Testing.Platform v2 integration.

The Stage 7 evaluation established the migration shape and measured only a modest improvement, so VSTest remains selected. When the trigger occurs, validate executable test modules, filtering, TRX/reporting, coverage, IDE support, minimum-count enforcement and CI commands as one isolated runner change.

Sources: [TestingStrategy.md](TestingStrategy.md#continuous-integration), [IntegrationTestingStage7Results-2026-07-18.md](IntegrationTestingStage7Results-2026-07-18.md#microsofttestingplatform-evaluation)

### Evaluate collection-scoped concurrent Workspace reuse

**Status:** Conditional

**Trigger:** Integration-test feedback time becomes materially limiting after the existing project-level parallelism is measured on the current suite.

Prove thread safety before sharing an expensive read-only Workspace fixture across a test collection. Mutable workspaces and state roots must remain scenario-isolated. This is a test-feedback optimisation and should not delay release.

Source: [IntegrationTestingImplementationPlan.md](IntegrationTestingImplementationPlan.md#decisions-deliberately-deferred)

### Replace source-governance tests with analysers

**Status:** Conditional

**Trigger:** A current source-governance test proves too slow, too late in the feedback cycle or unable to enforce a rule reliably.

Assess whether repository source-policy tests should become compile-time analysers. Keep compatibility audit ownership separate and do not remove an existing governance check until equivalent analyser enforcement exists.

Source: [IntegrationTestingImplementationPlan.md](IntegrationTestingImplementationPlan.md#decisions-deliberately-deferred)

### Remove the acceptance shutdown workaround

**Status:** Conditional

**Trigger:** A stable MCP C# SDK closes redirected stdin before waiting for the child process to exit, or provides an equivalent graceful-shutdown mechanism.

Upgrade the acceptance client, remove the short forced-cleanup fallback and make ordinary fixture disposal prove graceful Host shutdown. Retain the direct stdin-EOF acceptance case as explicit protocol-lifetime coverage.

Source: [IntegrationTestingStage2Results.md](IntegrationTestingStage2Results.md)

### Reconsider mutation families blocked by Roslyn APIs

**Status:** Conditional

**Trigger:** Roslyn exposes a supported non-IDE-only API or diagnostics path for one of the listed families.

The following aspirational families are deliberately not planned against the current public Roslyn surface:

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

**Trigger:** A concrete plugin requires independently composed MEF module assemblies rather than one marked entry assembly plus ordinary dependencies.

Define discovery, identity, isolation and collision requirements before adding module composition.

Source: [2026-07-13-mef-plugin-composition.md](superpowers/plans/2026-07-13-mef-plugin-composition.md)
