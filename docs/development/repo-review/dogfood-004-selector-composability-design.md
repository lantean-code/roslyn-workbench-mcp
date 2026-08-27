# DOGFOOD-004 — Canonical selectors for resolved locations

## Purpose

DOGFOOD-004 makes a source location returned by one tool straightforward to reuse as another tool's input without weakening selector validation or redesigning the existing input contracts.

## Repeated dogfood evidence

The published DOGFOOD-002 build exposes complete, readable `LocationSelector`, `DocumentSelector`, `ProjectSelector` and `SnapshotPrecondition` declarations. Repeating the original workflows showed that the remaining friction is contract composition rather than schema projection:

1. `get-symbol-members` with the documentation-comment ID for `CommitRecoveryStore` returned `SymbolAmbiguous` without candidates, so a separate `search-symbols` call was required.
2. `search-symbols` returned the production symbol with a `ResolvedLocation` containing `document`, a flat `span`, line, column and snapshot.
3. Passing that complete `location` unchanged as `symbol.location` failed because `LocationSelector` expects `span.document` and `span.range`, not top-level `document` and flat `span` members.
4. Manually moving the returned document, project and span fields still failed because `DocumentReference` correctly contains both path and document ID while `DocumentSelector` correctly permits exactly one.
5. Retaining only document ID and project ID, nesting them under `span.document`, renaming the returned span to `range` and copying the returned snapshot into `expectedSnapshot` succeeded.

The corrected schemas made every required transformation visible, but the caller still had to understand two deliberately different contracts and recreate a conversion already implemented inside the server.

## Discovery

`ResolvedLocation` is a public result model containing an optional `DocumentReference`, optional `TextSpanRange`, one-based line and column, and a required `SnapshotPrecondition`. `LocationSelector` is a public input model with exactly one of a document-bound `TextSpanSelector` or a copied `TextSelectionSelector`. Their different shapes are intentional: a result reports all useful identities and display coordinates, while an input requires one unambiguous resolution strategy.

`WorkspaceSelectorFactory.CreateLocationSelector` already implements the canonical conversion. It requires a resolved document and span, prefers document ID over path, retains project ID when available and builds the correctly nested span selector. `CreateSymbolSelector` composes the same conversion. The dedicated `resolve-symbol` tool uses this service to return a canonical symbol selector, but ordinary `ResolvedLocation` values returned by symbol search, references, diagnostics, definitions and other inspection tools do not expose that projection.

The documentation-comment ambiguity path is separate. `WorkspaceResolver.ResolveSymbolByDocumentationCommentIdAsync` reduces resolution to `Resolved`, `Ambiguous` or `NotFound`; `SelectorResolveResult<T>` and `SelectorRejectionFactory` do not retain candidates. Adding bounded candidates would therefore change the generic resolver and rejection contracts across project, document, location and symbol resolution. `search-symbols` already provides a bounded, filterable discovery operation and returns the identities needed to recover.

Microsoft's documentation for `DocumentationCommentId.GetFirstSymbolForDeclarationId` confirms that it returns the first matching declaration symbol from a supplied `Compilation`, with undefined ordering. The current resolver deliberately evaluates the identifier across loaded project compilations; DOGFOOD-004 does not change those resolution semantics.

## Operating-model assessment

- **Actor:** an authorised agent performing ordinary read-only inspection against a loaded trusted Workspace.
- **Action:** use a source location returned by `search-symbols` or another query as the target of a later location- or symbol-based query.
- **Plausibility:** this is the normal recovery path after documentation-comment ambiguity and a common way to compose inspection tools.
- **Existing control:** input validation prevents ambiguous document identity, and snapshot validation prevents a returned span from being silently reinterpreted against a newer Workspace state. The existing selector factory knows how to preserve both controls, but its result is not published with ordinary resolved locations.
- **Impact:** callers receive avoidable `InvalidRequest` responses or must manually restructure fields and discard one returned document identity. The operation eventually succeeds, but only with server-specific conversion knowledge.
- **Decision:** publish the existing canonical conversion with each convertible `ResolvedLocation`; retain the current validated input contracts and separate snapshot precondition.

## Proposed contract

Add a span-only `CanonicalLocationSelector` result contract and expose it through an optional `Selector` property on `ResolvedLocation`:

```csharp
/// <summary>
/// Represents the canonical span-based selector for a resolved source location.
/// </summary>
public sealed record CanonicalLocationSelector
{
    public required TextSpanSelector Span { get; init; }
}

public CanonicalLocationSelector? Selector { get; init; }
```

Do not type this output property as the complete `LocationSelector`. Its copied-selection branch contains `SelectedText`, `ContextBefore` and `ContextAfter`; using that input union as an output type would advertise source-text context throughout nested inspection output schemas even though the canonical projection is always span-based. `CanonicalLocationSelector` publishes exactly the span branch and its JSON remains structurally valid `LocationSelector` input.

For a normal source location, the JSON result will retain its existing diagnostic fields and add the reusable input projection:

```json
{
  "document": {
    "documentId": "...",
    "path": "src/Example.cs",
    "projectId": "..."
  },
  "span": {
    "start": 163,
    "length": 19
  },
  "line": 7,
  "column": 23,
  "snapshot": {
    "workspaceId": "...",
    "workspaceEpoch": 1,
    "snapshotId": "...",
    "transactionRevision": null
  },
  "selector": {
    "span": {
      "document": {
        "documentId": "...",
        "project": {
          "projectId": "..."
        }
      },
      "range": {
        "start": 163,
        "length": 19
      }
    }
  }
}
```

The next request uses `location.selector` as its location selector and `location.snapshot` as `expectedSnapshot`. No returned field is moved, renamed or discarded by the caller.

`Selector` remains nullable because Roslyn can produce source locations for which the Host cannot create a Workspace-local document reference. Existing `Document`, `Span`, `Line`, `Column` and `Snapshot` members remain unchanged, making this an additive public and JSON contract change. The distinct .NET result type prevents copied-selection fields from entering output contracts; MCP clients can copy the structurally compatible JSON object unchanged into a `LocationSelector` input.

## Production design

Add `CreateCanonicalLocationSelector` to `IWorkspaceSelectorFactory` and share the underlying span projection with its existing `CreateLocationSelector` and `CreateSymbolSelector` operations. Inject the selector factory into `WorkspaceResolverFactory` and pass it to each `WorkspaceResolver`. `WorkspaceResolver.CreateResolvedLocation` should first construct the existing `ResolvedLocation`, ask the factory directly for its `CanonicalLocationSelector` and return the record with `Selector` populated. This keeps the path-versus-document-ID and span-only result decisions in one implementation, prevents selection text from entering output schemas and ensures every result produced through the central resolver receives the same projection.

Do not make `ResolvedLocation` itself an accepted input type. That would mix display metadata with selection semantics, admit both path and document ID at the input boundary and duplicate or conflict with the existing `expectedSnapshot` convention.

Do not remove `Document`, `Span`, `Line` or `Column`. They are useful result metadata, are already consumed by clients and plugins, and changing the established result shape would be a breaking contract change.

## Ambiguity decision

Do not add bounded candidates to `SymbolAmbiguous` as part of DOGFOOD-004. Once search results carry a canonical selector, the documented query-first recovery path is one bounded `search-symbols` call followed by direct reuse of the chosen result. Candidate-bearing rejections would require a broader generic resolution-result redesign, introduce limits and projection rules for several selector families, and partly duplicate `search-symbols` filters and pagination.

The ambiguity response should remain accurate and continue directing the caller to resolve the target again. If repeated use after this change shows that the extra discovery call is itself material friction, candidate-bearing errors can be designed as a separate improvement with evidence from more than one selector family.

## Test design

Add or update focused non-acceptance coverage:

- `WorkspaceSelectorFactoryTests` continues to own the canonical path-versus-document-ID projection rules.
- `WorkspaceResolverTests` verifies that a convertible source `ResolvedLocation` contains the canonical selector and that an unconvertible location leaves it null.
- `WorkspaceResolverFactoryTests` verifies the selector factory is passed into created resolvers through the existing factory boundary.
- `WorkspaceResolverIntegrationTests` creates a real resolved source location, serializes its published `Selector` as MCP JSON, deserializes that unchanged object as a `LocationSelector` input and proves it resolves the same document and span.
- `ToolResultEnvelopeSerializerTests` verifies the additive `selector` JSON shape is emitted with a resolved location, remains structurally distinct from the complete result metadata and contains no copied-selection or context fields.
- `SchemaGenerationTests` continues to prove nested inspection output schemas do not advertise unconditional source text or selection context.
- `WorkspaceWorkflowIntegrationTests` starts the published Host, obtains a canonical selector from a `search-symbols` result, verifies its span-only wire shape and passes that JSON object unchanged to `get-symbol-members` with the returned snapshot precondition.

The acceptance scenario is permanent evidence for the published executable boundary that the non-acceptance conversion and serialization tests cannot cover. The published Codex projection will still be validated against a dogfood candidate after commit.

## Validation plan

After implementation:

1. Format only the changed production and test files.
2. Build the solution normally with the WSL artefacts path.
3. Run `latest-all` analyser builds for each affected production and test project.
4. Run the affected Workspace unit, Workspace integration and Host protocol/unit test projects using the repository's preferred non-acceptance fast loop where applicable.
5. Run the complete published-Host acceptance suite through the platform wrapper because an acceptance-test source changed.
6. Verify all changed CRLF-governed files use CRLF and both unstaged and staged diffs pass `git diff --check` at their respective process gates.

After the independently reviewed change is committed, publish that exact `HEAD` to a new dogfood candidate. Repeat `search-symbols`, pass the returned `location.selector` unchanged into `get-symbol-members`, pass `location.snapshot` as `expectedSnapshot`, and require success without a malformed intermediate request. Confirm that documentation-comment ambiguity still returns the existing accurate rejection and remains recoverable through the bounded search.

## Approval and scope gates

This document is a proposal only. Do not change production or test code until the user explicitly approves it.

Implementation scope is limited to:

- the additive `CanonicalLocationSelector` contract and `ResolvedLocation.Selector` public result member;
- central population through `WorkspaceResolver` and its factory;
- focused selector-factory, resolver, wire-compatibility and serialization tests; and
- one focused published-Host acceptance scenario proving the returned selector shape and unchanged reuse; and
- DOGFOOD-004 design, usage and final worklist documentation required by the remediation process.

Do not change the existing selector input shapes, snapshot-precondition semantics, ambiguity result contracts, documentation-comment resolution, search limits, MCP SDK version or other acceptance-test assets as part of DOGFOOD-004.
