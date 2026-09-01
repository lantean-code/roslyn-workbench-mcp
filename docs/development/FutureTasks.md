# Future Tasks

Date: 2026-09-01

## Purpose

This document is the single active engineering backlog for work already justified by the release contract, current evidence or an explicit product decision. Historical plans and audits may retain unchecked or deferred text for context, but an item is actionable only when it appears here.

Completed work, superseded checklists, approved defensive coverage gaps and standing guardrails are excluded. Trigger-based ideas are kept separately from scheduled work so they are not mistaken for current commitments.

Active tasks are ordered by delivery sequence rather than severity:

1. **First alpha release** — define the pre-release contract, prepare the public repository and community routes, then manually publish and validate release builds through the agreed lightweight release guidance.
2. **Beta preparation** — announce the product and begin wider external feedback when it is ready for beta evaluation.
3. **Stable v1 preparation** — add production discovery, plugin authoring and native installation capabilities intentionally deferred from alpha.

Conditional tasks remain inactive until their stated external capability, evidence threshold or product need exists.

Statuses mean:

- **Started**: investigation, infrastructure or partial implementation already exists, but the intended outcome is not complete.
- **Not started**: the task is recorded, but no implementation work is evidenced.
- **Conditional**: the task has a named trigger and is not active until that trigger occurs.

When a task is completed, remove it from this document rather than retaining a completed entry.

## First Alpha Release

### Prepare, validate and publish the first alpha

**Status:** Not started

Execute the dependency-ordered [`First Alpha Release Worklist`](AlphaReleaseWorklist.md). Its unchecked items are the detailed execution record for this canonical backlog task.

The worklist covers the alpha release shape, GitHub repository and community setup, issue triage, Discussions, security controls, versioning, release production, .NET tool validation, diagnostics and error reporting, and manual release checks.

## Beta Preparation

### Announce and monitor the beta

**Status:** Not started

When the product is ready for wider beta evaluation, publish an approved GitHub Announcement and any deliberately selected additional announcement. Watch incoming Issues, Discussions, private security reports and opt-in error reports, and triage meaningful feedback as it arrives. Do not require a formal observation period, reporting cadence or adoption metric unless experience shows that one would be useful.

Alpha releases remain direct engineering builds validated through manual checks; they do not require a public announcement or monitoring programme.

## Stable v1 Preparation

### Publish to the MCP Registry and discovery catalogues

**Status:** Not started

As part of the road to the production v1 release, publish the NuGet-hosted stdio tool to the official MCP Registry using `io.github.lantean-code/roslyn-workbench-mcp` as its server name. Include the matching ownership marker in the package README and generate versioned `server.json` metadata from the release build using the current stable Registry schema, exact package version, GitHub repository identity, `dnx` runtime hint and stdio transport. Reference the locked SVG icon as a scalable `any` size and provide a 128×128 or 256×256 PNG from `assets/icons` as the universally supported fallback. Include no required arguments or environment variables because the Host starts with defaults. Validate generated metadata rather than editing version fields manually.

Publish only after the production discovery route is intentionally being prepared; an alpha or beta package on NuGet.org does not trigger Registry publication. Use the exact package already published to NuGet.org and do not rebuild or replace it for registration.

Inventory a small number of reputable MCP client catalogues and community directories. Prefer the official Registry as the authoritative machine-readable record and add other listings only when their maintenance, security and update process are acceptable. Avoid broad automated submission and duplicate metadata formats that cannot preserve version and ownership information.

### Publish the plugin-authoring ecosystem and curated repository

**Status:** Not started

Publish and validate the supported third-party plugin-authoring surface before v1, including the Plugins NuGet package, its analyser and authoring documentation. Verify the package through a clean external sample with no repository project references, deliberately approve its dependency boundary and ranges, confirm Workspace is not exposed as an authoring dependency, load the sample into the packaged Host and exercise representative query and mutation tools.

Create the public discovery route for third-party Roslyn Workbench plugins before v1. Define repository ownership, submission and removal rules, source and licence requirements, compatibility metadata, package and release provenance, declared filesystem/process/network behaviour, security reporting, maintainer responsibilities, update and deprecation handling, and the boundary between listing and endorsement before accepting entries. The alpha retains the existing plugin runtime for source consumers but does not publish the authoring package, authoring documentation, a plugin directory or showcase listings.

### Add platform-native installation packages

**Status:** Not started

Retain the .NET tool as the portable installation route while adding managed deployment options before v1. Produce an MSI suitable for corporate Windows environments, publish that approved installer through Chocolatey and WinGet, and add Linux distribution packages and repository installation beginning with Debian/Ubuntu `apt`. Evaluate RPM and other distribution-specific formats from demonstrated user demand rather than claiming unsupported package-manager coverage.

Every installer must consume the same versioned Host output as the corresponding release build, preserve GitVersion identity and diagnostic symbols, support clean installation, upgrade and removal, and avoid introducing a second independently built release binary. Windows executables, shortcuts and MSI Add or Remove Programs metadata should use the multi-resolution `assets/icons/roslyn-workbench-mcp.ico`; other package formats should select the smallest matching approved PNG without rescaling a smaller asset upward. Installer signing, repository trust, update behaviour and enterprise deployment documentation form part of this work.

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
