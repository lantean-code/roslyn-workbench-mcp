# Reuse targets and snapshots

## From discovery to the next request

`resolve-symbol.data.selector` is ready for a subsequent `symbol` argument. Other queries commonly return a `SymbolReference`: use its `location.selector` with the sibling `location.snapshot`.

```javascript
const location = foundSymbol.location;
call("get-symbol-info", {
  workspace: { workspaceId: location.snapshot.workspaceId },
  symbol: { location: location.selector },
  expectedSnapshot: location.snapshot
});
```

A symbol selector uses either `location` or `documentationCommentId`, with optional project qualification. A result object is not itself a selector; there is no `symbolId` or `symbolKey` request field. Documentation IDs suit declared symbols; locals need a source location. Linked documents and multiple target frameworks may require `project` qualification even when the path or documentation ID matches.

When no returned location exists, copy source into a text selection and use surrounding context to distinguish repeated text. For example, replacing these values with the observed path and source:

```json
{
  "location": {
    "selection": {
      "document": { "path": "src/OrderService.cs" },
      "selectedText": "CalculateTotal",
      "contextBefore": "public decimal ",
      "contextAfter": "("
    }
  }
}
```

Numeric `span.range` offsets are zero-based UTF-16 units; displayed line/column coordinates are one-based.

## Preserve the source view

Copy the complete returned snapshot into `expectedSnapshot`: `workspaceId`, `workspaceEpoch`, `snapshotId` and nullable `transactionRevision`.

Reload, staging and history moves can invalidate targets. Re-resolve the target and snapshot together; replacing only the snapshot leaves stale coordinates or action handles. Undo can revisit a revision number without restoring an old snapshot identity. Failed calls supply no replacement snapshot.

## Interpret bounds

`hasMore: true` means a collection is truncated. Narrow scope/filter or raise that collection's limit; reruns start from the beginning, with no pagination cursor. Nested collections and graph depth have independent bounds, so a complete outer list does not establish complete call sites, child nodes or traversed relationships.
