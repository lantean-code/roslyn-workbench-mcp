# Code Actions

Roslyn Workbench exposes Roslyn Code Fixes and refactorings through three Host-owned MCP tools:

- `list-code-actions` discovers applicable actions and returns temporary opaque references;
- `prepare-fix-all` evaluates one supported Fix All scope without staging changes; and
- `stage-code-action` stages either one discovered action or one prepared Fix All action into the active transaction.

MCP `tools/list` is authoritative for their live schemas and metadata. Code Actions are composed once when the Host starts, are reported as a Host component rather than a plugin, and do not add one MCP tool per Roslyn provider.

## Safe staging workflow

Use the normal [workspace and transaction workflow](WorkspacesAndTransactions.md):

1. Open a fully trusted workspace and check `workspace-status`.
2. Start a transaction with `transaction-start`.
3. Call `list-code-actions` against the transaction's current revision.
4. Select one result using its title, precise location and diagnostic context where present.
5. Pass its `actionId` and the current `expectedSnapshot` to `stage-code-action`.
6. Inspect the result and call `transaction-preview`.
7. Commit the final revision with `transaction-commit`, or discard it with `transaction-rollback`.

Successful staging advances the transaction revision. Discover again before selecting another action for that new revision. Do not assume that a title, location or reference from an older revision still identifies the same operation.

## Discover actions

`list-code-actions` accepts:

| Argument | Requirement | Meaning |
| --- | --- | --- |
| `workspace` | Optional when exactly one workspace is loaded | Selects the loaded workspace. |
| `document` | Required | Selects one source document by workspace-relative path or document ID, with an optional project selector for linked or multi-target documents. |
| `range` | Optional | Omitted means complete-document discovery; a positive length means selection discovery; zero length means caret discovery. Positions are zero-based UTF-16 values. |
| `kinds` | Required | `1` discovers Code Fixes, `2` discovers refactorings and `3` discovers both. |
| `diagnosticIds` | Optional | Narrows Code Fix discovery to the supplied diagnostic IDs. |
| `limit` | Optional, default `50` | Bounds the returned action leaves. Zero returns no items. |

For example, this request discovers Code Fixes across one document:

```json
{
  "workspace": {
    "workspaceId": "workspace-id"
  },
  "document": {
    "path": "src/Example.cs"
  },
  "kinds": 1,
  "diagnosticIds": [
    "IDE0003"
  ],
  "limit": 50
}
```

The successful `data.actions` value is a bounded collection with `items`, `hasMore` and an optional `totalCount`. Each item contains:

- `actionId`, an opaque temporary UUID reference;
- `title` and `kind`;
- the precise project-aware `location`, including its document, span, line and column;
- concise `diagnostics` with IDs and messages for a Code Fix; and
- `fixAllScopes` when the selected Code Fix supports Fix All.

Provider identities, CLR types, equivalence keys, replay details and internal policy decisions are deliberately omitted. Use the returned title, location, diagnostics and supported scopes to choose an action.

## Stage one action

`stage-code-action` is the only Code Action mutation tool. It requires an active transaction:

```json
{
  "workspace": {
    "workspaceId": "workspace-id"
  },
  "actionId": "00000000-0000-0000-0000-000000000000",
  "expectedSnapshot": {
    "workspaceId": "workspace-id",
    "workspaceEpoch": 1,
    "transactionRevision": 0
  }
}
```

The Host re-discovers the exact action, validates that it still has one unambiguous match, evaluates its operations, rejects unsupported effects, and stages the resulting source-only candidate through the Workspace transaction pipeline. It never writes source files directly. A successful stage consumes the reference and returns the mutation summary, compact preview and updated transaction revision.

## Prepare and stage Fix All

Only use `prepare-fix-all` when the selected Code Fix lists the requested scope in `fixAllScopes`. Scope values are `0` for document, `1` for project and `2` for solution.

```json
{
  "workspace": {
    "workspaceId": "workspace-id"
  },
  "actionId": "00000000-0000-0000-0000-000000000000",
  "scope": 0,
  "maxChanges": 50,
  "affectedDocumentsLimit": 20,
  "expectedSnapshot": {
    "workspaceId": "workspace-id",
    "workspaceEpoch": 1,
    "transactionRevision": 0
  }
}
```

Preparation is read-only. It revalidates the originating Code Fix, evaluates the selected scope, rejects an operation exceeding `maxChanges`, and returns:

- a new `actionId` representing the prepared Fix All operation;
- the accepted `scope`;
- `affectedDiagnosticCount` when authoritatively available; and
- a bounded `affectedDocuments` collection.

Pass the new prepared `actionId`, not the originating action reference, to `stage-code-action` with the same current snapshot. Preview, history, rollback and commit then work exactly as they do for a single action.

## Reference and snapshot rules

Action references are process-local, bounded, temporary and tied to the exact Workspace snapshot from which they were created. Their lifetime defaults to five minutes and is configured with [`--code-action-reference-lifetime`](Configuration.md). Successful staging consumes a reference; merely listing or preparing does not change transaction state.

If a reference is unknown, expired, evicted, already consumed, or no longer matches the selected snapshot, the Host returns a structured failure such as `ActionExpired`, `SnapshotMismatch` or `ActionAmbiguous` with `next: resolveTargetAgain`. Follow that recovery action: inspect the current Workspace state, then list and select the action again. Never edit an `actionId`, reconstruct one from response metadata, or reuse it against a different Workspace, epoch or transaction revision.

## Availability and exclusions

The Host composes installed Roslyn Code Fix and refactoring providers at startup, then applies an exception policy to omit actions that cannot safely use this workflow. Examples include actions requiring interactive options or external UI, package or reference installation, unsupported project-system changes, or operation shapes outside the source-only transaction contract.

An omitted action is not evidence that the provider is missing: it may be inapplicable at the selected location, filtered by the request, unavailable in the loaded project, or excluded by policy. Use `server-status` with `detail: Full` to inspect Code Action component availability and startup diagnostics. Third-party plugins cannot register Code Actions or use Host Code Action services; they add ordinary query and mutation tools through the separate [plugin API](PluginAuthoring.md).
