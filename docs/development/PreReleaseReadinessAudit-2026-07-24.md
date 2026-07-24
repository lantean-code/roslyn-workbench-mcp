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
