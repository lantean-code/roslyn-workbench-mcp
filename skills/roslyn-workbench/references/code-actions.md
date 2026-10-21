# Discover and stage Roslyn Code Actions

Use Code Actions for compiler/analyser fixes and provider refactorings. Use [rename/format tools](rename-format.md) for ordinary symbol renaming and formatting.

## Discover and stage

1. For a source edit, start the transaction **before discovering actions**. `list-code-actions` and `prepare-fix-all` also work without a transaction for inspection; neither stages changes.
2. Call `list-code-actions` on the current document and snapshot. Prefer a caret (`range.length: 0`) or selection for targeted refactorings; omit `range` for document-wide diagnostic fixes. Narrow `kinds` and `diagnosticIds` to the task.
3. Select from `data.actions.items` using title, project-aware location, diagnostic evidence and `fixAllScopes`. If `actions.hasMore`, narrow discovery or raise its limit before concluding no suitable action exists.
4. Call `stage-code-action` with the chosen `actionId` and matching `expectedSnapshot`. The Host rediscovers the action and checks its source effects. A changed stage consumes the reference and advances the snapshot: discover again before staging another action. Complete [transaction review and commit](transactions.md).

## Fix All

Choose an ordinary discovered Code Fix advertising the required scope. Refactorings and already-prepared Fix All references cannot be prepared. Use the smallest scope that meets the task: `"Document"`, `"Project"` or `"Solution"` is a string, not a scope-selector object.

```javascript
// W is the workspace; fix is the selected action from listed.data.actions.items.
const prepared = await call("prepare-fix-all", {
  workspace: W, actionId: fix.actionId, scope: "Project",
  maxChanges: 30, affectedDocumentsLimit: 30,
  expectedSnapshot: listed.snapshot
});
// Inspect affectedDocuments, hasMore and totalCount before staging.
await call("stage-code-action", {
  workspace: W, actionId: prepared.data.actionId,
  expectedSnapshot: prepared.snapshot
});
```

Stage the **new prepared action ID**, not the original fix ID. `maxChanges` caps **changed source documents**, not diagnostics or edits; exceeding it rejects the operation. `affectedDocumentsLimit` only limits returned document identities. Preparation evaluates a candidate; staging recomputes its effects and requires a match. Preparation does not issue a transaction-review receipt.

## Missing or stale actions

References are temporary and snapshot-specific; history or reload also requires rediscovery. For `ActionExpired`, `SnapshotMismatch`, `ActionAmbiguous` or `MutationCandidateChanged`, use the returned `continuation.instruction` to rediscover/reselect or reprepare; rediscovery uses `kind: "ReviseRequest"`.

If discovery is empty, check the selection, filters, compilation/diagnostics and Code Actions availability in `server-status`. Actions requiring option dialogs, external UI, package/reference installation or unsupported project-system operations are excluded. Handle package/project changes through the repository workflow, then refresh the workspace as its status requires.
