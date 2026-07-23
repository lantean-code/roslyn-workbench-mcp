# Bounded Collection Total Count Audit — 2026-07-23

## Outcome

The shared `BoundedCollection<TItem>` contract now publishes an optional
`totalCount`. The value describes the complete result count after request scope
and semantic filters but before the response bound. It is omitted when obtaining
the count would require continuing an otherwise bounded Roslyn analysis.

The contract factories enforce the valid states:

- an untruncated collection publishes `totalCount` equal to the returned item
  count;
- a known total must be at least the returned item count, and `hasMore` is
  derived from whether it exceeds that count; and
- a truncated collection with an unknown total retains `hasMore: true` and
  omits `totalCount`.

## Producer inventory

### Authoritative count already materialised

These producers complete discovery or already hold the post-filter candidate
set before response selection, so they publish its count:

| Tool | Collections |
|---|---|
| `analyze-nullability` | findings |
| `find-callees` | callees |
| `find-callers` | callers |
| `find-dependency-cycles` | cycles |
| `find-duplicate-code` | groups |
| `find-implementations` | implementations |
| `find-overloads` | overloads |
| `find-overrides` | overrides |
| `find-references` | references |
| `get-change-impact` | locations |
| `get-code-metrics` | metrics |
| `get-diagnostics` | diagnostics |
| `get-project-details` | project references, metadata references, analyzers |
| `get-solution-structure` | folders, projects |
| `get-symbol-attributes` | attributes |
| `get-symbol-dependencies` | dependencies |
| `get-symbol-dependents` | dependents |
| `get-symbol-members` | members |
| `search-symbols` | symbols |

### Cheaply available from completed discovery

These producers already complete Roslyn discovery. Applying the requested depth
filter to that materialised set is cheap compared with discovery and avoids
additional Roslyn work:

| Tool | Collections |
|---|---|
| `find-derived-types` | derived types |
| `get-type-hierarchy` | base types, interfaces, derived types |

### Expensive or unknown when truncated

These producers stop semantic discovery or projection after establishing
`hasMore`, or their service deliberately builds only a bounded graph. They omit
`totalCount` when truncated:

| Tool | Collections | Reason |
|---|---|---|
| `analyze-async` | findings | Stops syntax and operation analysis at the bound. |
| `analyze-disposables` | findings | Stops disposable-flow analysis at the bound. |
| `find-unused-symbols` | candidates | Further candidates require semantic diagnostic projection. |
| `get-api-surface` | symbols | Stops recursive API projection at the bound. |
| `get-dependency-graph` | nodes, edges | The dependency service deliberately bounds graph construction. |
| `get-partial-declarations` | declarations | Further declarations require syntax and document resolution. |
| `get-project-details` | documents | Document projection is nullable and stops once the bound is established. |
| `get-test-impact` | tests | Further tests require dependency analysis. |

When any producer in this category completes without truncation, the shared
factory still publishes the returned item count as the authoritative total.

## Contract and validation coverage

- MCP output schemas publish optional non-negative `totalCount`.
- JSON omits the property when the total is unknown.
- Contract tests cover known, unknown, empty, untruncated and invalid totals.
- Tool tests cover known totals, deliberately unknown truncated totals and
  service-provided totals.
- The manual scenario runner records `totalCount` alongside item count,
  `hasMore` and ordered item hashes.
