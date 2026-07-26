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

### Complete the pre-release readiness review

**Status:** Started

Align the release documentation and package-facing material, remove development-only content from public surfaces, validate the complete supported functionality, perform a security and trust-boundary audit, and finish the product polish needed before release packaging begins.

Implement the dependency-ordered batches defined by the [Pre-release Readiness Audit](PreReleaseReadinessAudit-2026-07-24.md):

1. release documentation, backlog and package-facing documentation alignment — complete;
2. supported-functionality and public-contract audit — complete;
3. security, trust-boundary and dependency audit — complete; `PRR-F020` physical Workspace containment, `PRR-F021` trusted-workspace guidance, `PRR-F022` private recovery storage and `PRR-F024` bounded reference and recovery input processing are resolved; and
4. final product polish and release-readiness validation.

Do not begin artifact publication until the audit has no unresolved release-blocking findings. Development records may remain under `docs/development`, but release-facing documentation and package content must describe only supported behaviour.

### Prepare and publish the v1 release artifacts

**Status:** Not started

Implement tag-driven release publication using GitVersion. A release tag must produce one consistent version across the Host assemblies, .NET tool package, standalone executable archives and Plugins NuGet package.

Publish and retain these immutable artifacts:

- the MCP server as a .NET tool package;
- standalone executable archives for every supported runtime identifier;
- the third-party Plugins library as the author-facing NuGet package, containing the Plugins and Abstractions assemblies plus the authoring analyser;
- symbol packages where applicable;
- checksums for downloadable standalone artifacts; and
- release notes identifying the source tag and commit.

Release reproducibility is based on the tagged source, pinned .NET SDK, centrally managed exact direct dependency versions and retention of the artifacts produced by the release workflow. Do not adopt `packages.lock.json` files solely for release publication or GitHub Actions caching. Leave NuGet package caching disabled unless restore performance later becomes a measured problem that justifies revisiting the policy.

GitHub and publication preparation own `PRR-F023`. Before public publishing, pin every reusable GitHub Action to a reviewed full commit SHA, configure an automated update path such as Dependabot, retain minimal workflow permissions, and use environment-protected OIDC or trusted publishing rather than long-lived publishing credentials.

Before publishing:

1. Restore, build, test, pack and publish from the release tag in one controlled workflow.
2. Confirm every artifact carries the GitVersion-derived release version.
3. Install the generated .NET tool package from an isolated local package source and run a published-Host acceptance smoke test.
4. Run each standalone executable on its target operating system without relying on repository build output.
5. Inspect the Plugins package and generated `.nuspec`; verify that it contains the Plugins and Abstractions assemblies and analyser without publishing Workspace as an authoring dependency, then deliberately approve its direct dependency ranges.
6. Install the Plugins package into a clean external sample plugin with no project references to this repository, then build and exercise that plugin against the packaged Host.
7. Publish only the exact artifacts that passed validation, and retain them without rebuilding the same version.

The Plugins package validation is the consumer-compatibility boundary. A repository lock file would constrain the dependency graph used to build the package, but it would not force downstream plugin projects to restore that graph.

## P1 — Production Confidence and Performance

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

### Resolve unavailable Code Action families after the availability reassessment

**Status:** Started

The 2026-07-26 audit found that the previous “blocked by Roslyn APIs” classification combined ready replay providers, mixed providers, public-API implementation opportunities, high-complexity custom refactorings and intentional product exclusions. The runtime-backed inventory guard then found and classified 151 additional composed C# code-fix providers that the source-based audit had not assessed.

Complete the follow-up work in dependency order:

1. Complete — add a provider-inventory check over the actual composed C# runtime providers, including language-neutral Core providers. The pinned composition currently contains 81 refactoring providers and 169 code-fix providers.
2. Complete — classify the 151 newly inventoried providers as 47 compiler-backed replay candidates, 94 requiring built-in diagnostic support, eight covered by existing tools and two excluded project-setting mutations. No additional option-backed providers were found.
3. Started — eight local compiler-backed code fixes are now validated, published as dedicated tools and covered through real transaction staging and preview. Validate the remaining 39 compiler-backed candidates and the six known ordinary refactoring candidates, then make promotion decisions per proven fixture.
4. Decide whether to add bounded built-in analyser activation and diagnostic mapping for the 94 IDE-diagnostic providers.
5. Add production action-level capability classification, then validate the safe leaves of `GenerateConstructorFromMembers`, `GenerateEqualsAndGetHashCodeFromMembers` and `GenerateType`.
6. Design the high-value public-API simplification workflow before considering custom generation and solution-wide semantic refactorings.

Keep project/package mutation, editor rename tracking and Copilot-backed providers excluded. Keep move-to-namespace, pull-member-up, move-static-members, extraction, change-signature and convert-to-async outside the pre-v1 critical path unless product priority changes.

Source: [RoslynCodeActionAvailabilityAudit-2026-07-26.md](RoslynCodeActionAvailabilityAudit-2026-07-26.md)

### Support additional MEF plugin module assemblies

**Status:** Conditional

**Trigger:** A concrete plugin requires independently composed MEF module assemblies rather than one marked entry assembly plus ordinary dependencies.

Define discovery, identity, isolation and collision requirements before adding module composition.

Source: [2026-07-13-mef-plugin-composition.md](superpowers/plans/2026-07-13-mef-plugin-composition.md)
