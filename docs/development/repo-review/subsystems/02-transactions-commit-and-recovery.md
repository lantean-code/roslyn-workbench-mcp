# Unit 2 — Transactions, commit and recovery

**Status:** Completed with five candidates. Unit 1 reopened for shared owner, snapshot and recovery-admission semantics.

## Implementation evidence

- Per-Workspace exclusive gates protect transactions while `WorkspaceSessionStore` retains a process-global owner slot. `TransactionService` routes start, history, rollback, preview and commit.
- Revisions retain immutable Roslyn solutions. Undo/redo changes the current revision; append after undo discards redo and allocates a new internal snapshot ID.
- Plugin, bundled and Code Action mutations converge on `MutationStagingService`. Processing rejects unsupported graph/options/reference changes, propagates multi-target added/removed documents, merges linked changes, validates prepared Fix All identity and appends revisions.
- Commit locks the Workspace root, plans create/replace/delete, verifies original bytes, serialises intended representation, persists recovery evidence, revalidates disk, enters `Applying`, applies non-cancellably, validates targets/inputs, promotes a committed snapshot and cleans evidence.
- Recovery is bounded beneath the configured state directory. Unix records require owner-only modes and reject links. Startup clears orphans only under lock, restores non-terminal manifests, retains conflicts/incomplete records and cleans committed evidence.
- Static inspection covered all transaction/recovery DI singletons and projected revision/state configuration. Existing implementation handles static containment, duplicate targets, links, byte drift, modes, capacity, cancellation boundaries, partial rollback, corrupt artefacts and lock contention.

## Trace results and candidates

Single/multi-file and multi-project mutations, history, external edits, durability cancellation, partial recovery, contention and startup states were traced. The review raised concurrent owner admission (`RWMCP3-003`), reusable public snapshot tuples (`RWMCP3-004`), add/delete project-graph persistence (`RWMCP3-005`), malformed recovery-path admission (`RWMCP3-006`) and containment TOCTOU (`RWMCP3-007`).

## Claimed and executable evidence

Workspace unit/integration, Host transaction/startup/status, plugin/Code Action integration, published mutation/recovery acceptance and manual scenario families claim broad coverage. Ownership tests are sequential; link tests are static; add/create tests use default compile globs; malformed-recovery tests do not combine a nonblank invalid OS path with Workspace open.

Focused Workspace unit tests passed 311/311 and focused real component tests passed 35/35. They establish covered paths but not the five candidate scenarios. Acceptance and scenarios were not rerun.

## Limits and follow-up

Unit 3 should assess author guidance for add/delete. Unit 4 must trace stale preconditions. Unit 5 must inspect add/remove Code Actions. Unit 6 must inspect simultaneous protocol starts. Unit 8 must validate crash, platform durability, directory swaps and scenario evidence. Native power-loss guarantees and Windows ACL behaviour remain external limits.

## Exported assumptions

Only one transaction may exist process-wide. Public preconditions must identify one immutable solution. Add/delete must remain correct after reload or be rejected. Invalid recovery must yield structured blocking status. Containment must hold at the filesystem mutation boundary.

**Candidates:** `RWMCP3-003`–`RWMCP3-007`.

**Reopened:** Unit 1 for `RWMCP3-003`, `RWMCP3-004`, `RWMCP3-006`.

**Later units required to revisit:** Units 3–6 and 8.
