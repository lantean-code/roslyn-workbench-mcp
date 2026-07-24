# Pre-release Readiness Audit

Date: 2026-07-24

## Purpose

This audit is the release gate before artifact-publication work begins. It aligns the supported product contract, release documentation and package-facing material; validates the complete functional surface; examines security and trust boundaries; and identifies final product polish.

The audit covers the current source state. Historical plans and implementation evidence remain development records and are not treated as release promises.

## Preliminary findings

The initial inventory identified these actionable inconsistencies:

- the root README contains a development-only `Next` note about nullable Code Action selections;
- the Plugins package README links to a diagnostics document that is not included in the package and does not use a durable public URL;
- the Workspace support package uses the Plugins authoring guide as its package README even though it is not the author-facing package;
- the Code Action audit workflow still watches the former release-document location instead of `docs/development/RoslynCodeActionsAudit.md`;
- the public platform-support text describes macOS release validation before that validation workflow exists;
- `FutureTasks.md` retained completed acceptance and analyser work despite its active-backlog policy; and
- no final public API compatibility, security or release-polish review has been recorded for the release candidate.

These are inventory findings, not the complete audit result.

## Functionality findings promoted from public documentation

| ID | Status | Finding | Disposition |
| --- | --- | --- | --- |
| `PRR-F001` | Resolved | Location-driven Code Action requests such as `AddAwaitRequest` exposed a nullable `Selection`, while the common staging path rejected a missing selector. | All 20 execution-required Code Action target properties are now required and non-nullable through the published request, resolution and staging paths. Schema contract coverage verifies that each property is required and excludes `null`; request binding also rejects explicit `null` for non-nullable properties. |
| `PRR-F002` | Resolved as unverified best effort | Public planning classified macOS as best effort, but `FileStreamWorkspaceFileLockProvider` rejected commit locking outside Windows and Linux. | The macOS path now uses Apple `flock(2)` through `libSystem.dylib` because .NET 10 marks `FileStream.Lock` and `Unlock` unsupported on macOS. Atomic replacement retains the same-directory `File.Move` design and uses macOS `libSystem` imports for directory `open` and `fsync`. This is a research-backed best guess, not validated support; the existing cross-process lock, atomic-write and durability integration coverage must run on a hosted macOS agent during public release-candidate preparation. |
| `PRR-F003` | Resolved | Plugin search roots were deduplicated with `StringComparer.OrdinalIgnoreCase` on every platform before plugin discovery. Linux and macOS can contain distinct roots whose paths differ only by case, so one configured root could be discarded even though the public configuration contract says command-line and environment roots are combined and deduplicated. | Startup configuration now uses the existing filesystem-aware Workspace path comparison for each root. This preserves case-distinct roots on native Linux and macOS while retaining case-insensitive deduplication on Windows and Windows-mounted WSL paths; resolver evidence covers both comparison modes. |
| `PRR-F004` | Resolved | `--code-action-token-lifetime` accepted every positive `TimeSpan`, but Code Action discovery calculates expiry with `DateTimeOffset.Add`. A sufficiently large accepted duration could exceed `DateTimeOffset.MaxValue` and make discovery throw instead of producing an action token. | The supported lifetime is now bounded to 24 hours, the release configuration publishes that maximum, excessive values use the existing fallback-warning contract, validation uses the same rule, and Code Action coverage proves expiry calculation at the accepted maximum. |

## Batch 1 — Documentation and release-surface alignment

**Status:** Complete

1. Remove development-only notes, implementation instructions and stale future claims from release-facing documents.
2. Reconcile terminology, supported-platform statements, links, examples, CLI options, tool behaviour and transaction guidance with the current implementation.
3. Give every published package an appropriate self-contained README and durable diagnostic links.
4. Repair workflow documentation paths and check all release-document links.
5. Keep active work in `FutureTasks.md`; retain completed evidence only in the relevant audit or results document.
6. Decide whether public repository files such as `SECURITY.md`, `CONTRIBUTING.md`, a code of conduct and support guidance are required for v1.

Batch 1 establishes the written product contract used by the remaining audits.

The root and release-document indexes now describe the product without an embedded development backlog. Workspace loading, WSL storage guidance, bounded defaults and current platform support are aligned with the implementation. The stale Code Action workflow paths and package-management paths are corrected.

The Plugins package retains the authoring guide as its README and uses the same durable diagnostics URL as the analyser descriptors. The transitive Workspace support package now has a separate README that explains its dependency role without presenting it as the author-facing package. Package integration coverage inspects both generated READMEs.

`SECURITY.md` and `CONTRIBUTING.md` now provide the required private-reporting boundary and contribution entry point. A separate `SUPPORT.md` is unnecessary for v1 because the release documentation and issue guidance already define those routes. A code of conduct is a repository-owner community-governance choice rather than a product security control; select one before actively soliciting public contributions, but it does not block technical release validation.

## Batch 2 — Supported-functionality and contract audit

**Status:** In progress. The Host startup, configuration and lifecycle scope is validated and its two findings are resolved; Workspace, tool, transaction and plugin scopes remain.

Validate the implementation against the written product contract:

- executable startup, command-line validation, configuration precedence, help and version behaviour;
- workspace open, selection, diagnostics, stale-state handling, reload and close behaviour;
- every published query, mutation and Code Action tool, including complete request-property mapping;
- schema descriptions, curated defaults, selectors, bounded collections, known totals and result invariants;
- transaction staging, preview, commit, rollback, conflict, recovery, restart and multi-workspace ownership;
- unsupported project handling and Windows, Linux and WSL path behaviour;
- plugin discovery, authoring, validation, analyser activation, package layout and failure containment; and
- live tool discovery and acceptance coverage against the actual published Host.

Record each finding with its affected contract, severity, evidence and required disposition. Group safe fixes, but isolate changes that affect public contracts, persistence, transactions or compatibility.

### Host startup, configuration and lifecycle validation

The validation used the release-facing [Getting started](../GettingStarted.md), [Configuration](../Configuration.md) and [Tool discovery and results](../ToolDiscovery.md) documents as the product contract. Existing implementation evidence came from the configuration and status unit tests, Host composition and startup-prerequisite integration tests, and the 40-case published-Host acceptance suite recorded in the [Published Host Acceptance Coverage Audit](AcceptanceCoverageAudit-2026-07-23.md).

| Contract area | Result | Evidence and disposition |
| --- | --- | --- |
| Published executable startup and stdio transport | Validated | The acceptance fixture launches the exact configured Release-published executable, initialises through the official MCP client and exercises the public catalogue and `server-status`. Host composition registers stdio transport and all server-owned, bundled, Code Action and external-plugin tool families. |
| Protocol and operational stream separation | Validated | Host logging clears inherited providers and routes every configured log level to stderr; MCP stdio transport owns stdin/stdout. Published startup failure diagnostics are captured from stderr without treating them as protocol messages. |
| Graceful process lifetime | Validated | Direct published-process acceptance closes stdin, observes process exit code `0` and retains a bounded forced-cleanup fallback only for the known MCP SDK client-disposal limitation. Normal acceptance disposal verifies process completion and isolated-root cleanup. |
| Startup without a Workspace | Validated | Published protocol and catalogue/status acceptance run before any Workspace is opened, and the live catalogue remains stable before open, during a transaction and after close. |
| Documented options, environment variables and defaults | Validated | Resolver tests cover every documented option in separated and `=` forms, every environment variable, all documented defaults, supported value ranges, schema modes, state-directory syntax and plugin-root collection. The token-lifetime range discrepancy found during validation was resolved under `PRR-F004`. |
| Scalar precedence, repeatable roots and invalid-value fallback | Validated | Unit and published acceptance prove command-line scalar precedence, last-value semantics, combined command-line/environment plugin roots, fallback warnings and effective status projection. Filesystem-aware plugin-root deduplication was resolved under `PRR-F003`. |
| Runtime propagation of configuration | Validated | Host composition tests prove that result, concurrency and transaction limits reach `WorkspaceOptions`, token lifetime reaches `CodeActionExecutionOptions`, and state-directory and schema-mode values reach their owning services. Published acceptance exercises configured single-slot query concurrency and default/full schema publication. |
| Non-sensitive effective status and startup diagnostics | Validated | Full `server-status` acceptance verifies effective non-sensitive values, fallback warnings, actionable MSBuild status and omission of state, plugin and scenario paths. Standard detail omits expanded configuration, plugin and recovery branches. |
| Startup prerequisite ordering and recovery | Validated | Component integration proves MSBuild registration and commit recovery complete before the transport starts. Published restart acceptance reports durable recovery conflict state and blocks affected Workspace opening with the documented recovery action. |
| Startup-fixed plugin catalogue | Validated | Published acceptance covers combined package roots, valid and invalid package isolation, collision handling, sanitised configuration failures and discovery of a newly installed package only after restart. |
| Help, version and unknown-argument behaviour | No published contract | Release documentation does not advertise standalone help or version commands or rejection of unknown arguments. Product and Roslyn versions are observable through `server-status`, and the resolver deliberately ignores unrelated arguments. No release discrepancy is recorded; exact GitVersion-derived artifact identity remains part of release publication validation. |
| Supported platforms | Validated for available evidence | Native Windows and WSL/Linux published acceptance evidence is recorded. macOS implementation is resolved as unverified best effort under `PRR-F002`; hosted macOS validation remains deferred until public release-candidate preparation. |

## Batch 3 — Security and trust-boundary audit

Create an explicit threat model for the local stdio Host and its trusted in-process plugin model. Audit:

- command-line, MCP, JSON and plugin metadata input validation;
- path canonicalisation, root containment, traversal, symlinks and Windows/WSL path translation;
- transaction time-of-check/time-of-use behaviour, atomic replacement, journals and crash recovery;
- state and temporary-directory permissions, cleanup and sensitive source retention;
- process invocation, MSBuild arguments, environment inheritance and command-injection resistance;
- plugin discovery, dependency loading, identity, collisions and the documented absence of adversarial isolation;
- resource-exhaustion controls for large workspaces, compilations, results and concurrent requests;
- cancellation and shutdown behaviour at safe and unsafe interruption points;
- stdout protocol integrity, stderr logging, error disclosure and accidental secret or source leakage;
- NuGet dependencies, known vulnerabilities, generated package contents and analyzer dependency isolation; and
- GitHub Actions permissions, third-party action pinning, artifact handling and future publishing credentials.

Use static analysis and dependency tooling as evidence, not as a substitute for design review. Fix exploitable or boundary-breaking findings before release. Document accepted residual risks in release-facing security guidance.

## Batch 4 — Product polish and final validation

1. Review public API naming, XML documentation, package metadata and compatibility baselines.
2. Review tool, schema, diagnostic, log and error wording for consistency and actionable recovery guidance.
3. Confirm clean install, first-run, help, version, configuration and plugin-author workflows.
4. Enable appropriate package validation and public API compatibility checks where they provide a stable v1 baseline.
5. Run clean restore, build, latest-all analysis, unit, contract, integration and published-Host acceptance validation.
6. Run the supported Windows and Linux paths and record WSL-specific evidence; treat macOS as best effort only when public release infrastructure exists.
7. Re-run the audit inventory and close, defer with rationale, or promote every finding to an explicit release blocker.

## Completion criteria

The readiness phase is complete when:

- release-facing documents contain no development-only notes or unsupported claims;
- the complete supported tool and lifecycle surface has an evidence-backed disposition;
- security boundaries and residual risks are documented accurately;
- no unresolved critical or high-severity functionality or security finding remains;
- package consumers and the published Host pass clean external validation; and
- remaining work is limited to artifact construction and publication.
