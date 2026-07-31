# Future Tasks

Date: 2026-07-30

## Purpose

This document is the single active engineering backlog for work already justified by the release contract, current evidence or an explicit product decision. Historical plans and audits may retain unchecked or deferred text for context, but an item is actionable only when it appears here.

Completed work, superseded checklists, approved defensive coverage gaps and standing guardrails are excluded. Trigger-based ideas are kept separately from scheduled work so they are not mistaken for current commitments.

Active tasks are ordered by delivery sequence rather than severity. Complete each phase before the next unless the tasks are explicitly safe to run in parallel:

1. **Foundation** — resolve known reproducibility defects and stabilise the public v1 extension boundary.
2. **User-approved diagnostics** — make early v1 failures locally diagnosable and externally reportable only under explicit consent.
3. **Release automation** — automate the evidence required for a release candidate.
4. **Release candidate preparation** — build and validate the versioned artifacts without publishing them.
5. **Final release gate** — complete the readiness review against the prepared candidate.
6. **Publication** — publish only the candidate that passed the final gate.

Conditional tasks remain inactive until their stated external capability, evidence threshold or product need exists.

Statuses mean:

- **Started**: investigation, infrastructure or partial implementation already exists, but the intended outcome is not complete.
- **Not started**: the task is recorded, but no implementation work is evidenced.
- **Conditional**: the task has a named trigger and is not active until that trigger occurs.

When a task is completed, remove it from this document rather than retaining a completed entry.

## Phase 1 — Foundation

### Make external-repository preparation clean and repeatable

**Status:** Not started

Make scenario-runner repository preparation preserve the pinned tracked state across repeated native Windows and Linux/WSL runs without requiring manual cache repair. The Batch 7 Windows EF Core run exposed a mismatch: repository preparation could remove the pinned commit's three zero-byte tracked sentinel files, causing the next cache reuse to fail its deliberate tracked-cleanliness guard even though no source mutation occurred.

Retain the guard against silently concealing real source mutations, failed durable restoration or recovery defects. Do not introduce a blanket reset or clean. Instead, identify preparation side effects at the preparation boundary, fail with the exact affected paths, and either isolate generated preparation output from the checkout or support explicitly declared, pinned-content restoration for known repository-owned preparation effects. Add repeat-preparation and cache-reuse coverage for EF Core on Windows and representative Linux/WSL coverage.

Source: [Code Action Batch 7 Validation](CodeActionBatch7Validation-2026-07-30.md#external-repository-preparation-follow-up)

### Harden the plugin query-cache boundary

**Status:** Not started

Replace the raw `IToolExecutionServices.QueryCache` contract with Host-created synchronous and asynchronous get-or-create scopes. Bind the public cache to the current query invocation, exact Workspace snapshot, plugin and tool; require dedicated immutable semantic key types; coalesce identical misses; and prevent cross-plugin collisions, escaped-scope use, recursive-factory deadlock, late stores into an invalidated generation and manual Workspace identity mistakes. Package analyser errors for unsafe keys and warnings for unsafe cached values.

Use physically separate, interface-registered state and capacity for Host Workspace query results, plugin query results and replayable Code Action handles. Preserve the Workspace cache's current solution-based invalidation, apply stricter exact-transaction-snapshot invalidation to plugins, retain existing Code Action expiry and lifecycle semantics, and remove concrete-plus-resolving-delegate registrations for shared query and Code Action state.

Default both query caches to one-hour sliding expiration with independent command-line overrides up to 24 hours. Expose independently bounded capacity controls that cannot disable caching or fall below an evidence-backed supported minimum. Add permanent versioned scenario-runner cache metrics, calibrate minimum/default/maximum limits across representative repositories and a cache-using fixture plugin, and correlate logical pressure with retained process memory before locking the v1 defaults.

Implement the design and validation requirements in [Plugin Query Cache Boundary](PluginQueryCacheBoundary-2026-07-30.md) before treating the current cache API as a stable v1 plugin contract.

## Phase 2 — User-Approved Diagnostics

### Implement local error inspection and user-approved external reporting

**Status:** Not started

Retain bounded immutable records for unexpected correlated tool failures, expose detailed local diagnostics to the trusted agent through `get-error-details`, and add `prepare-error-report` plus `submit-error-report` for sanitised external reporting. Preserve normal stderr logging and generic MCP failures; do not retain live exceptions, scrape raw logs, submit automatically or allow local diagnostic content to enter the external submission path.

Use MCP form elicitation for per-report, Workspace and session approval. Support the concise agreed choices, session suppression through “No, and don't ask again”, and explicit command-line `never|prompt|always` policy. Temporary approvals bypass only the prompt: every external report must still be prepared, reviewed and passed to an explicit submission call. Include reporting availability with unexpected errors so agents do not attempt suppressed or unavailable preparation.

Keep captured errors, prepared canonical payloads and consent state process-local, bounded, expiring and capacity-isolated. Implement Sentry first behind an internal provider abstraction, require provider-level idempotency, and validate privacy, exact-byte submission, concurrency, retry, conditional publication, open-world metadata and the trusted-agent boundary before release-candidate preparation.

Implement the complete architecture and validation requirements in [User-Approved Error Reporting Proposal](UserApprovedErrorReportingProposal-2026-07-30.md).

## Phase 3 — Release Automation

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
6. Make the final aggregate and comparison available to the publication workflow for attachment to the GitHub release.

Source: [Testing Strategy](TestingStrategy.md#release-validation-and-performance-history), [Published Host Acceptance Coverage Audit](AcceptanceCoverageAudit-2026-07-23.md#release-only-scenario-validation-and-metrics)

## Phase 4 — Release Candidate Preparation

### Prepare and validate the v1 release artifacts

**Status:** Not started

Implement tag-driven release preparation using GitVersion. A release tag must produce one consistent version across the Host assemblies, .NET tool package, standalone executable archives and Plugins NuGet package.

Prepare and retain these candidate artifacts:

- the MCP server as a .NET tool package;
- standalone executable archives for every supported runtime identifier;
- the third-party Plugins library as the author-facing NuGet package, containing the Plugins and Abstractions assemblies plus the authoring analyser;
- symbol packages where applicable;
- checksums for downloadable standalone artifacts; and
- draft release notes identifying the source tag and commit.

Release reproducibility is based on the tagged source, pinned .NET SDK, centrally managed exact direct dependency versions and retention of the artifacts produced by the release workflow. Do not adopt `packages.lock.json` files solely for release publication or GitHub Actions caching. Leave NuGet package caching disabled unless restore performance later becomes a measured problem that justifies revisiting the policy.

GitHub and publication preparation own `PRR-F023`. Pin every reusable GitHub Action to a reviewed full commit SHA, configure an automated update path such as Dependabot, retain minimal workflow permissions, and configure environment-protected OIDC or trusted publishing rather than long-lived publishing credentials.

Validate the candidate without publishing it:

1. Restore, build, test and pack from the release tag in one controlled workflow.
2. Confirm every artifact carries the GitVersion-derived release version.
3. Install the generated .NET tool package from an isolated local package source and run a published-Host acceptance smoke test.
4. Run each standalone executable on its target operating system without relying on repository build output.
5. Inspect the Plugins package and generated `.nuspec`; verify that it contains the Plugins and Abstractions assemblies and analyser without publishing Workspace as an authoring dependency, then deliberately approve its direct dependency ranges.
6. Install the Plugins package into a clean external sample plugin with no project references to this repository, then build and exercise that plugin against the packaged Host.
7. Retain the exact candidate artifacts and validation evidence for the final release gate.

The Plugins package validation is the consumer-compatibility boundary. A repository lock file would constrain the dependency graph used to build the package, but it would not force downstream plugin projects to restore that graph.

## Phase 5 — Final Release Gate

### Complete the pre-release readiness review

**Status:** Started

Align the release documentation and package-facing material, remove development-only content from public surfaces, validate the complete supported functionality, perform a security and trust-boundary audit, and finish the product polish needed before publication.

Implement the dependency-ordered batches defined by the [Pre-release Readiness Audit](PreReleaseReadinessAudit-2026-07-24.md):

1. release documentation, backlog and package-facing documentation alignment — complete;
2. supported-functionality and public-contract audit — complete;
3. security, trust-boundary and dependency audit — complete; `PRR-F020` physical Workspace containment, `PRR-F021` trusted-workspace guidance, `PRR-F022` private recovery storage and `PRR-F024` bounded reference and recovery input processing are resolved; and
4. final product polish and release-readiness validation against the prepared v1 candidate.

Do not begin artifact publication until the audit has no unresolved release-blocking findings. Development records may remain under `docs/development`, but release-facing documentation and package content must describe only supported behaviour.

## Phase 6 — Publication

### Publish the validated v1 release artifacts

**Status:** Not started

Publish only the exact candidate artifacts that passed Phase 4 validation and the Phase 5 release gate. Do not rebuild or replace artifacts under the same version.

Use the environment-protected OIDC or trusted-publishing configuration prepared in Phase 4 to publish the .NET tool and Plugins packages, standalone archives, symbol packages, checksums and release notes. Attach the final scenario metrics aggregate and comparison report produced by Phase 3 to the GitHub release, and retain all immutable release artifacts with their source tag and commit identity.

## Conditional Backlog

These items are intentionally inactive. Move one into the appropriate delivery phase only when its stated trigger occurs.

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

### Support additional MEF plugin module assemblies

**Status:** Conditional

**Trigger:** A concrete plugin requires independently composed MEF module assemblies rather than one marked entry assembly plus ordinary dependencies.

Define discovery, identity, isolation and collision requirements before adding module composition.

Source: [2026-07-13-mef-plugin-composition.md](superpowers/plans/2026-07-13-mef-plugin-composition.md)
