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
| `PRR-F005` | Resolved | Workspace path identity was compared with `StringComparison.Ordinal` during duplicate-open checks, path selection and pending-recovery matching. Native Windows and the default Windows-mounted WSL filesystem are case-insensitive, so a casing variant could fail selection, open a duplicate session or bypass the Workspace-level recovery preflight. | Duplicate-open, path-selector and pending-recovery checks now use `IWorkspacePathComparison` for filesystem paths while generated IDs and caller aliases remain ordinal. Unit evidence covers both case-sensitive and case-insensitive policies across all three paths. |
| `PRR-F006` | Resolved | `WorkspaceResolver` derived its relative-path base from the loaded solution or project directory instead of the published `WorkspaceIdentity.WorkspaceRoot`. When an explicit root was above the loaded path, emitted document and project references were not Workspace-relative and documented Workspace-relative selectors did not resolve consistently. | Resolution and projection now use `WorkspaceIdentity.WorkspaceRoot`, with filesystem-aware path comparison. Unit and component evidence places the Workspace root above the solution and project directories and proves project and document references round-trip through selectors. |
| `PRR-F007` | Resolved | External-change state transitions updated the in-memory Workspace session but did not update the advisory cross-instance status record. Another Host could therefore continue to observe `Ready` or `TransactionActive` after the owning Host transitioned to `WorkspaceOutOfDate` or `TransactionConflicted`. | Every external-change transition now queues an advisory status update through a publisher-owned ordered worker. Awaited transaction publications share the same queue, shutdown drains queued updates, and status publication remains non-blocking for mutation decisions. Unit evidence covers queue ordering and lifecycle ownership; two-instance component evidence observes both `WorkspaceOutOfDate` and `TransactionConflicted`. |
| `PRR-F008` | Resolved | External-change detection identified files only by existence, length and last-write time. A same-length edit whose timestamp was preserved or fell within a coarse filesystem timestamp interval was treated as unchanged, allowing queries to continue against a stale Roslyn snapshot despite the documented reload contract. | Each loaded Workspace now owns a recursive filesystem change monitor scoped to its manifest files and input directories, while the existing file metadata polling remains as a fallback. Monitoring starts only after the manifest is known, detects tracked-file changes plus relevant create, delete, rename and watcher-error events, ignores directory timestamp churn, and retains the first trigger for `workspace-status`. Generated trees are identified from each physical project's evaluated MSBuild output and intermediate properties, Roslyn's output paths and the Workspace-owned `.vs` state root instead of excluding every directory named `bin` or `obj`. Exact manifest inputs remain monitored even when they are generated beneath an excluded root. Unsafe evaluated roots that contain a loaded solution or project are rejected, and a project-local `bin`/`obj` fallback is used only when evaluation supplies no output root. Native callbacks hand lightweight notifications to an ordered unbounded channel backed by the maximum 64 KiB watcher buffer; filtering and first-change publication run on one consumer, while request-time validation synchronises with notifications already accepted by managed code. The monitor stops after the first relevant change and is disposed on close, reload, commit replacement and failed loading. Real-filesystem, published-host and repository-scale evidence covers same-length timestamp-preserving edits, custom and central artifact layouts, actionable diagnostics, an 80,000-operation watcher burst and three consecutive native Windows EF Core live builds without overflow or false staleness. |

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

**Status:** In progress. The Host startup, configuration and lifecycle scope and the Workspace lifecycle scope are validated, and their findings are resolved. Tool, transaction and plugin scopes remain.

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

The validation used the release-facing [Getting started](../GettingStarted.md), [Configuration](../Configuration.md) and [Tool discovery and results](../ToolDiscovery.md) documents as the product contract. Existing implementation evidence came from the configuration and status unit tests, Host composition and startup-prerequisite integration tests, and the 41-case published-Host acceptance suite recorded in the [Published Host Acceptance Coverage Audit](AcceptanceCoverageAudit-2026-07-23.md).

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

### Workspace lifecycle and stale-state validation

The validation used [Workspaces and transactions](../WorkspacesAndTransactions.md), the first-workflow guidance in [Getting started](../GettingStarted.md), and the selector contract retained in the development design records. Implementation evidence came from the Workspace lifecycle, loading, selection, resolution, change-detection, coordination and execution-context services; their unit and component integration suites; and the published-Host acceptance evidence recorded in the [Published Host Acceptance Coverage Audit](AcceptanceCoverageAudit-2026-07-23.md).

The completed non-acceptance validation passed all 795 Workspace fast unit and contract cases, all 71 Workspace integration cases and all 283 Host unit and component cases. The complete published-Host acceptance suite passes all 41 cases on both WSL/Linux and native Windows, including linked multi-target selection and mutation, actionable external-change diagnostics and generated-output changes that must not make the Workspace stale.

| Contract area | Result | Evidence and disposition |
| --- | --- | --- |
| Absolute `.csproj`, `.sln` and `.slnx` loading | Validated | The loader normalises only absolute supported extensions and dispatches projects separately from solutions. Published acceptance opens all three forms. |
| Supported-project filtering and load diagnostics | Validated | Unsupported languages, missing project paths, non-SDK projects and unresolved analyser references are removed with structured diagnostics. Loading rejects when no supported SDK-style C# project remains. Unit, component and published acceptance cover usable mixed input and malformed or unsupported rejection. |
| Workspace-root selection and containment | Validated for lexical paths | Explicit roots must be existing absolute directories containing the loaded path, and every retained project and source document is checked against that root. Symlink and canonical-boundary resistance belongs to Batch 3. |
| Workspace identity and path comparison | Resolved | Duplicate-open, path-selector and pending-recovery checks consistently use the filesystem-aware comparison policy. Case-sensitive and case-insensitive evidence resolves high-severity `PRR-F005`. |
| Workspace-relative project and document selectors | Resolved | ID, alias, canonical-path, implicit, project-qualified and ambiguous selector paths retain their published behaviour, while relative project and document paths now use the selected Workspace root and round-trip when the root is above the loaded path. This resolves medium-severity `PRR-F006`. |
| Multiple Workspace routing and ownership | Validated | Omitting a selector succeeds for one Workspace and rejects for many; Workspace gates remain independent; one global transaction owner is projected by `workspace-list`; closing one Workspace leaves another queryable. |
| Duplicate open, capacity and unsafe close | Validated | Structured errors cover duplicate identity under the effective filesystem casing policy, maximum loaded Workspace capacity, busy acquisition and close while a transaction is active. |
| External source and configuration changes | Resolved | Source additions, changed source, `Directory.Build.props` and editor-config changes transition the session and reject subsequent work with reload guidance. Manifest-scoped filesystem monitoring also detects same-length, timestamp-preserving edits while retaining metadata polling. Generated documents are excluded through evaluated project output/intermediate roots plus the Workspace `.vs` state root, while ordinary source trees merely named `bin` or `obj` remain tracked. Exact generated MSBuild inputs remain authoritative, and `workspace-status` retains the first trigger for diagnosis. Channel-backed event handling with a 64 KiB native buffer remained healthy through an 80,000-operation Windows stress run and three consecutive EF Core live builds. Published acceptance and repository-scale scenarios resolve medium-severity `PRR-F008`. |
| Reload and snapshot invalidation | Validated | Reload is restricted to out-of-date Workspaces without an active or conflicted transaction, creates a new epoch, replaces and disposes the old loaded Workspace, invalidates query-cache state and causes old snapshot preconditions to reject. Published acceptance proves refreshed semantic results. |
| Close and resource cleanup | Validated | Close takes exclusive access, rejects an open transaction, removes and cache-invalidates the selected session, closes its advisory status handle and disposes its loaded Roslyn Workspace. Component and published acceptance cover empty and multi-Workspace aftermath. |
| Cross-instance advisory state | Resolved | Open and status surface unavailable, unreadable and live-instance warnings with the documented query-only guidance. Transaction and external-change transitions publish through one ordered, lifecycle-owned queue; two-instance evidence covers out-of-date and conflicted observations. This resolves low-severity `PRR-F007`, and advisory state remains intentionally non-blocking. |
| WSL-mounted Windows storage warning | Validated | `workspace-open` adds a non-persisted `WorkspaceOnWindowsFileSystemFromWsl` warning using mount-aware path classification. Native WSL/manual release evidence remains the appropriate validation tier. |

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
