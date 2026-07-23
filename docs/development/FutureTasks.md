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

### Prepare and publish the v1 release artifacts

**Status:** Not started

Implement tag-driven release publication using GitVersion. A release tag must produce one consistent version across the Host assemblies, .NET tool package, standalone executable archives and Plugins NuGet package.

Publish and retain these immutable artifacts:

- the MCP server as a .NET tool package;
- standalone executable archives for every supported runtime identifier;
- the third-party Plugins library as a NuGet package;
- symbol packages where applicable;
- checksums for downloadable standalone artifacts; and
- release notes identifying the source tag and commit.

Release reproducibility is based on the tagged source, pinned .NET SDK, centrally managed exact direct dependency versions and retention of the artifacts produced by the release workflow. Do not adopt `packages.lock.json` files solely for release publication or GitHub Actions caching. Leave NuGet package caching disabled unless restore performance later becomes a measured problem that justifies revisiting the policy.

Before publishing:

1. Restore, build, test, pack and publish from the release tag in one controlled workflow.
2. Confirm every artifact carries the GitVersion-derived release version.
3. Install the generated .NET tool package from an isolated local package source and run a published-Host acceptance smoke test.
4. Run each standalone executable on its target operating system without relying on repository build output.
5. Inspect the Plugins package's generated `.nuspec` and deliberately approve its direct dependency ranges.
6. Install the Plugins package into a clean external sample plugin with no project references to this repository, then build and exercise that plugin against the packaged Host.
7. Publish only the exact artifacts that passed validation, and retain them without rebuilding the same version.

The Plugins package validation is the consumer-compatibility boundary. A repository lock file would constrain the dependency graph used to build the package, but it would not force downstream plugin projects to restore that graph.

## P1 — Production Confidence and Performance

### Expand published-Host acceptance coverage

**Status:** Started

Implement the dependency-ordered release-capability batches covering the exact published artifact, configuration, discovery, public result contracts, valid and failing external packages, supported Workspace open shapes, selector families, every production query and mutation execution path, transaction history, durability, restart, protocol cancellation, concurrency and failure containment.

Keep the acceptance project independent of production references and drive only the Release-published executable through the public MCP protocol. Use small checked-in fixtures, deterministic synchronization and the existing Ubuntu/Windows pull-request matrix. Do not move repository-scale, timing-sensitive or destructive release scenarios into pull-request acceptance.

Batches 1–3 are implemented. The acceptance boundary now has the shared published-process infrastructure, distribution/configuration and plugin-package coverage, supported Workspace load formats, multi-Workspace lifecycle routing, selector representatives, ambiguity and external-reload semantics. Batches 4–6 remain; the next manually initiated acceptance run and the Ubuntu/Windows pull-request matrix will provide runtime evidence for the newly added Batch 2 and 3 cases.

Source: [Published Host Acceptance Coverage Audit](AcceptanceCoverageAudit-2026-07-23.md)

### Automate release scenario validation and performance history

**Status:** Not started

Add a release-branch and manual-dispatch workflow for the existing external-repository scenario runner after the repository's release-branch naming convention is selected. Produce a versioned normalised metrics aggregate, compare it with the previous GitHub release asset and publish an advisory Markdown regression report.

Upload detailed scenario output as temporary workflow artifacts. Attach the final aggregate and comparison to the GitHub release for durable release-to-release history. Do not commit generated metrics to `main`, and do not introduce hard timing gates until repeated comparable release runs establish normal variance.

The repository is private before v1 release-candidate preparation, so do not run billed recurring macOS automation. When the repository becomes public, add best-effort macOS release/manual validation covering the published Host acceptance suite, Workspace integration and a curated external-repository scenario subset. macOS is not a pull-request gate. The external-repository scenario runner remains release-only on every platform.

Implementation order:

1. Select and document the release-branch naming convention.
2. Define and emit the versioned aggregate and scenario-suite identity.
3. Add compatible previous-release comparison and Markdown reporting.
4. Add manual and release-branch workflow orchestration.
5. Once the repository is public, add best-effort macOS release validation.
6. Attach the final aggregate and comparison to the GitHub release.

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
