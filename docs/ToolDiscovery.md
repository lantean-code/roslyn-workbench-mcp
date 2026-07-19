# Tool discovery and results

## Live tool inventory

Use MCP `tools/list` to discover the exact tools published by the running
process. The surface is assembled once at startup from:

- server-owned status, workspace and transaction tools;
- bundled Roslyn inspection and mutation tools;
- the internal Code Action catalogue; and
- enabled third-party plugins.

The tool list does not vary with workspace or transaction state and does not
change during the process lifetime. A tool that cannot run in the current state
returns a structured state error rather than disappearing from discovery.

`server-status` reports the total published tool count. With `detail: Full`, it
also reports plugin load results, Code Action availability, effective
configuration, startup warnings and unfinished recovery state. Internal Code
Actions are reported as a component, not as a plugin.

## Schemas and metadata

Every tool publishes its name, title, description, input schema and behavioural
annotations. By default, output schemas are omitted to keep `tools/list`
compact. Start the server with `--tool-output-schema-mode Full` when clients need
the generated family-specific output schemas.

Operational requirements that an agent must follow are stated in tool
descriptions and structured diagnostics rather than hidden in client-specific
metadata.

## Result envelope

Successful calls use a common structured envelope:

```json
{
  "ok": true,
  "data": {}
}
```

Failures return `ok: false`, a structured `error`, and an optional `next`
action. Agents should follow the returned action instead of guessing how to
repair stale selectors, transaction conflicts or unavailable workspace state.

## Bounded collections

Collection-returning tools expose named result limits and return deterministic
bounded collections with `hasMore`. Each tool publishes its curated default in
the input schema and uses that same value when the limit is omitted. The Host's
`DefaultMaxResults` remains available to third-party plugins as a compatibility
baseline, but does not override the bundled tools' declared defaults. There is
no global serialised byte ceiling.

When `hasMore` is true, an agent may request a larger collection if the extra
context is useful or narrow the workspace scope, selector or filter. A larger
request recomputes the deterministic result from the beginning; there are no
generic cursors or continuation tokens.

Bundled collection defaults are curated by result shape and expected cost:

- 16 results: base types;
- 25 results: dependency cycles and duplicate-code groups;
- 32 results: partial declarations;
- 50 results: async, disposable, nullability and unused-symbol findings,
  overloads, attributes, project references and analyzers;
- 64 results: implemented interfaces;
- 100 results: symbol searches, API symbols, metrics, callers, callees,
  references, implementations, overrides, derived types, hierarchy-derived
  types, dependencies, dependents, members, change-impact locations, tests,
  projects and metadata references;
- 200 results: diagnostics, project documents, solution folders and dependency
  graph nodes; and
- 400 results: dependency graph edges.

Other bounded numeric inputs also publish their effective defaults: hierarchy
depth is 3, code-context windows are 10 lines on each side, transaction diff
context is 3 lines, and fix-all is capped at 50 changed source documents unless
the caller supplies another value.
