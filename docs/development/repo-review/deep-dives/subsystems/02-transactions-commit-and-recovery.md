# Deep dive 2 — Transactions, commit and recovery

Date: 2026-07-31

Status: Complete

## Scope and dependency map

The review covered transaction admission and global ownership, immutable revision history, mutation-candidate validation, added-document propagation, linked-document reconciliation, preview/history/rollback, commit planning, physical containment, cross-process locks, durable atomic writes, recovery manifests, startup recovery and the Host, plugin and Code Action consumers of the staging boundary. Registrations in the Host composition root were checked: transaction, planner, writer, recovery, lock, path and staging services are singleton stateless services or singleton owners of synchronised process state, with no captive lifetime defect substantiated.

Dependency direction remains coherent. Host owns transaction MCP tools and startup recovery; Plugins and CodeActions adapt trusted handlers to a Workspace mutation lease; Workspace owns candidate normalisation, transaction state and all filesystem application; Abstractions exposes neutral snapshot and mutation results without exposing persistence implementation. Third-party mutation handlers cannot write through the supported API except by returning a candidate Solution to the Workspace-owned stager.

## Representative traces

### Single-file mutation and revision history

The Host or plugin adapter acquires an exclusive mutation lease, refreshes external-change state and invokes a handler against the active transaction snapshot. `MutationStagingService` validates the returned Solution, propagates supported project-context additions, reconciles linked text, builds a baseline-relative change summary, appends an immutable revision and replaces the session. Undo and redo select retained revision Solutions; staging after undo discards the redo suffix and its snapshot cache partitions. Revision admission is enforced before the stager is exposed. No stale-revision, suffix-retention or ordinary rollback defect survived validation.

### Multi-project linked-document mutation

Text changes for one physical source file are collected from linked document contexts, deduplicated when identical and applied to every linked document. Added files are propagated between Roslyn project contexts representing the same physical project. Deletions receive no equivalent reconciliation: RWMCP-014 shows that removing one linked document can produce a physical delete while another current project still contains that path, after which the inconsistent Solution is promoted as committed. RWMCP-019 records that adjacent non-overlapping text changes are rejected because `TextSpan.IntersectsWith` treats a shared boundary as an intersection even though `SourceText.WithChanges` accepts adjacency.

### Commit planning and application

Commit takes the in-process exclusive lease and a non-blocking cross-process file lock, creates a path-contained plan from baseline to current Solution, captures exact original and intended bytes, persists owner/artifacts/manifest, revalidates hashes, durably marks `Applying`, applies each entry atomically without cancellation, marks `Committed`, promotes the session and removes delete markers and recovery evidence. Every target is revalidated immediately before application and a failed or partial apply restores entries in reverse order. RWMCP-016 shows that replace and restore write a newly created temporary inode with default mode before renaming it over the source, so repository file permissions are not preserved. RWMCP-017 shows that the store's expected size-limit exceptions bypass the commit's structured filesystem-failure path. RWMCP-018 shows that all before/after byte arrays are retained in one plan without an aggregate byte limit before persistence.

Commit promotion also reopens RWMCP-008: after application, `CreateCommittedSession` fingerprints disk separately from the transaction Solution. An external writer in that interval can make the manifest describe external bytes while the promoted Solution retains intended bytes, recreating the same uncertified Solution/manifest pairing identified during load and reload.

### Cancellation, interruption and startup recovery

Cancellation is honoured through validation, planning, persistence and revalidation. Once source application begins, application and restoration intentionally use `CancellationToken.None`; this prevents cancellation from abandoning a partial commit. A cancellation before application restores the still-unapplied plan to a terminal state and removes its evidence. An interrupted `Applying` manifest is restored at startup under the same Workspace lock; conflicts and incomplete recovery remain durable and block affected Workspace admission.

A candidate startup orphan time-of-check/time-of-use race was independently rejected as RWMCP-015. Recovery scans owner-only pre-manifest evidence, but lock acquisition is non-blocking. While a live commit owns the lock, recovery skips deletion rather than waiting with a stale classification; after a crash, no writer can subsequently publish a manifest. Existing cross-process integration coverage proves contention and crash-released reacquisition.

### Cross-process contention and external boundaries

The commit lock is rooted beneath the canonical Workspace, revalidated after directory creation and backed by OS file locking on Windows, Linux and macOS. Contention returns `WorkspaceBusy`; lock setup failures return a structured retryable fault. Recovery artifacts use the configured state directory, owner-only Unix directories/files and physical-containment validation before read or write. Manifest validation checks version, operation shape, absolute target containment, exact artifact/delete-marker paths, duplicate targets, hashes and terminal state. No symlink traversal, ambiguous recovery ownership, unsafe cancellation boundary or unreleased lock survived validation.

## Configuration, contracts and tests

`MaxTransactionRevisions` is declared, validated and consumed consistently. State-directory startup validation precedes recovery. The recovery store bounds individual owners, legacy records, manifests and artifacts, but no aggregate commit-plan budget exists and the per-artifact write limit is not translated by the commit boundary.

Focused evidence passed on the current source: 796 transaction/recovery/IO unit tests in `Roslyn.Workbench.Mcp.Workspace.Test` and 17 durable-commit/atomic-write integration tests in `Roslyn.Workbench.Mcp.Workspace.IntegrationTest`. Coverage is strong for ordinary staging, history, candidate allow-lists, linked replacements, exact byte/encoding preservation, containment, manifest validation, partial-apply restoration, cancellation phases, terminal cleanup, real cross-process contention and owner-only recovery permissions. The passing suites do not contradict the retained findings because their linked fixtures change or remove only one unshared document, their overlap fixture uses genuinely overlapping replacements rather than adjacent spans, their default atomic replacement assertion checks bytes but not the destination mode, and their size tests cover artifact reads rather than end-to-end oversized commit handling or cumulative plan size.

## Findings and limitations

Independent source validation retained RWMCP-014 and RWMCP-016 through RWMCP-019, rejected RWMCP-015, and expanded RWMCP-008 with the post-application manifest-capture path. No P0 issue was substantiated. No production or test code was modified. Acceptance, Code Action audit and external-repository scenarios were not run under repository policy. Roslyn MCP tooling was unavailable, so local source inspection was used for symbol and call-site navigation; Microsoft Learn was used to confirm that `TextSpan.IntersectsWith` includes coincident boundaries while `SourceText.WithChanges` rejects only overlapping changes.
