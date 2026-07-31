# Subsystem review: transactions, durable commit and recovery

## Scope and relationships

This unit covers transaction start/history/preview/rollback, mutation-candidate processing, linked-document merging, commit planning, cross-process locks, atomic writes, durable recovery manifests and startup recovery. It depends on Abstractions plus Workspace session/lease infrastructure and is invoked by Host lifecycle tools, plugin mutation adapters and Code Action mutation adapters.

## Implementation and boundary review

- Mutation candidates are checked for project identity/options/reference changes, non-source document changes, workspace-root containment and linked-document consistency before a new transaction revision is stored.
- Commit validates the expected snapshot and external-input state, acquires the exclusive workspace gate and cross-process file lock, builds the complete plan, persists an owner record plus exact backups/staged artifacts, durably transitions the manifest and revalidates targets immediately before application.
- Cancellation is honoured before `Applying`. File application, manifest finalisation and failure restoration deliberately use non-cancellable tokens once the consistency boundary is crossed.
- `WorkspaceCommitWriter` compares current hashes with planned hashes, uses atomic replacement/create/delete-marker operations and can restore exact backups. `WorkspaceCommitRecoveryService` treats malformed, incomplete or unsafe evidence as conflicts rather than guessing.
- Recovery deserialisation is bounded by file-size limits, validates manifest version/IDs/absolute paths/root containment, checks artifact paths and file permissions, and keeps unrecoverable evidence for explicit resolution.

## Consumers, DI and configuration

All transaction/recovery services are singleton Host services. `MaxTransactionRevisions` and `StateDirectory` flow from startup configuration. Plugin and Code Action layers can only propose/stage Roslyn solutions; they cannot bypass this subsystem for writes.

## Tests and findings

Unit coverage is extensive for planning, locks, conflicts, cancellation boundaries, application failures, restoration and malicious recovery paths. Scenario-runner families cover durable commit, commit cancellation, conflicts and crash recovery, but are release/scenario workflows rather than the default test loop. RWMCP-007 is a lower-priority operational issue: an existing unwritable state directory is not detected until commit persistence, after transaction work has already been staged. The commit fails safely before source application. No transaction atomicity, confidentiality or data-loss finding survived validation.
