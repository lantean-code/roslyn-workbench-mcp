# Stage, review and persist changes

## Choose the review workflow

Read the mode from `server-status` with `detail: "Full"`:

| Mode | Inspect staged changes | Commit |
| --- | --- | --- |
| `inspection-only` | Transaction tools unavailable | Unavailable |
| `transactional` | `transaction-preview` | `expectedSnapshot`; Host confirmation per commit or session |
| `approval-required` | `transaction-review` | `expectedSnapshot` and returned `receiptId`; Host receipt approval |
| `autonomous-trusted` | `transaction-preview` | `expectedSnapshot` |

`transaction-preview` takes no `expectedSnapshot`; `transaction-review` requires it.

## Complete one transaction

1. Resolve the task through queries, then call `transaction-start`. A Host permits one active transaction across all workspaces.
2. Resolve mutation targets in the transaction's current snapshot, then stage [Code Actions](code-actions.md), [renames or formatting](rename-format.md). Check `data.staged` and retain the returned snapshot before resolving the next target. Queries see staged source; ordinary file reads still see disk. Keep mutations and transaction operations sequential.
3. Inspect the complete changed-document summary. For a detailed diff, supply `document` and `includeDiff: true` to preview/review. This selects one diff, **not a subset to commit**. Review every changed path, including additions and deletions.
4. Commit the reviewed revision or discard it with `transaction-rollback`. Inspect `data.committed` and `data.transaction`: a successful no-change commit can return `committed: false` and leave the transaction active. Roll back an empty transaction when finished.
5. Run the repository's build/tests as needed after persistence. Workbench commit writes source files; it does not create a Git commit.

`transaction-history` moves one staged revision with `direction: "Undo"` or `"Redo"` and the current `expectedSnapshot`; it does not list history. Check `canUndo`/`canRedo`, retain its new snapshot and rediscover subsequent targets. Conflicted transactions must be rolled back before history can change.

## Compiler validation

When enabled, `transaction-validate` compares compiler errors with the transaction-start baseline for changed loaded projects and their loaded transitive dependants. Existing errors may remain. Require both `data.isComplete` and `data.succeeded`; inspect `limitations`, `incompleteReasons` and `data.continuation`.

Review and commit enforce this optional gate automatically; commit validates again. It does not validate analysers, run tests or perform a full build. Fix introduced errors within the transaction. For incomplete validation, roll back, correct loading/input conditions, reload or reopen as status requires, then start again.

## Receipt and persistence failures

Receipts are process-local, one-use and valid for at most 15 minutes while workspace epoch, snapshot and revision remain unchanged. Staging, history moves or expiry require another review. Declined/unavailable approval leaves the receipt and transaction active. Accepted approval consumes the receipt before final validation/persistence; a later failure requires a fresh review.

Keep affected source paths free of concurrent edits until commit returns. After uncertain persistence, inspect `workspace-status` and follow [recovery guidance](recovery.md) before retrying. Source creation, deletion and file renaming do not update project membership: handle explicit project includes separately and reload afterwards. Source-generated documents and `obj` source cannot be mutation targets; inspect warnings for checked-in generated-looking files.
