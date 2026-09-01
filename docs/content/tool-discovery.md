# Tool discovery and result contracts

## Live tool inventory

Use MCP `tools/list` to discover the exact tools published by the running process. The surface is assembled once at startup from:

- server-owned status, workspace and transaction tools;
- bundled Roslyn inspection and mutation tools;
- the Host-owned `list-code-actions`, `prepare-fix-all` and `stage-code-action` tools; and
- `get-error-details`, plus the conditionally published `prepare-error-report` and `submit-error-report` tools; and
- enabled third-party plugins.

The tool list does not vary with workspace or transaction state and does not change during the process lifetime. A tool that cannot run in the current state returns a structured state error rather than disappearing from discovery.

`get-error-details` is always published for local correlated failure inspection. The two external-reporting tools are published whenever startup consent is not `never`; the Host supplies the provider and destination as application configuration. Their names remain reserved against plugin collisions when omitted under `never`. Per-report prompt outcomes do not change `tools/list` or establish runtime overrides.

`server-status` reports the total published tool count. With `detail: Full`, it also reports plugin load results, Code Action availability, effective configuration, startup warnings and unfinished recovery state. Internal Code Actions are reported as a component, not as a plugin.

The three-tool [Code Action workflow](code-actions.md) discovers ordinary Roslyn Code Fixes and refactorings through concise opaque references. Code Action providers do not publish separate provider-specific MCP tools.

## Schemas and metadata

Every tool publishes its name, title, description, input schema and behavioural annotations. By default, output schemas are omitted to keep `tools/list` compact. Start the server with `--tool-output-schema-mode Full` when clients need the generated family-specific output schemas. The Host validates response contracts during startup in both modes; this setting changes publication only, not whether a plugin response is safe to advertise and serialise.

Operational requirements that an agent must follow are stated in tool descriptions and structured diagnostics rather than hidden in client-specific metadata.

Error-report preparation is read-only and performs no network activity, but each call creates a new temporary immutable handle. Submission is state-changing, idempotent for that handle, and marked as an open-world external effect. It accepts only the handle and maps the stored immutable external report to the provider SDK; the returned preview is representative rather than a byte-for-byte transport envelope. See [Error reporting and privacy](error-reporting.md).

## Result envelope

Successful calls use a common structured envelope. Workspace-bound plugin and Code Action query or mutation successes include the required authoritative `snapshot`; server-owned successes include it when a Workspace context is available, while successes unrelated to a Workspace omit it:

```json
{
  "ok": true,
  "snapshot": {
    "workspaceId": "11111111-1111-1111-1111-111111111111",
    "workspaceEpoch": 1,
    "snapshotId": "22222222-2222-2222-2222-222222222222",
    "transactionRevision": 0
  },
  "data": {}
}
```

Handled failures return `ok: false`, a structured `error`, and an optional `continuation`; they do not publish a replacement snapshot. Its `kind` distinguishes a required exact tool call, a required choice between tools, a retry, a request revision, or external resolution; every variant includes a natural-language `instruction`. Unexpected correlated failures also identify whether local details are available and project the current external-reporting state, so an agent does not attempt preparation when reporting is disabled or approval is unavailable. Agents should follow the returned continuation instead of guessing how to repair stale selectors, transaction conflicts or unavailable workspace state.

## Bounded collections

Collection-returning tools expose named result limits and return deterministic bounded collections with `hasMore`. Each tool publishes its curated default in the input schema and uses that same value when the limit is omitted. The Host's `DefaultMaxResults` remains available to third-party plugins as a compatibility baseline, but does not override the bundled tools' declared defaults. There is no global serialised byte ceiling.

When `hasMore` is true, an agent may request a larger collection if the extra context is useful or narrow the workspace scope, selector or filter. A larger request recomputes the deterministic result from the beginning; there are no generic cursors or continuation tokens.

Bundled collection defaults are curated by result shape and expected cost:

- 16 results: base types;
- 25 results: dependency cycles and duplicate-code groups;
- 32 results: partial declarations and control-flow regions;
- 50 results: async, disposable, nullability and unused-symbol findings, overloads, attributes, project references and analyzers;
- 64 results: implemented interfaces and control-flow blocks;
- 100 results: symbol searches, API symbols, metrics, callers, callees, references, implementations, overrides, derived types, hierarchy-derived types, dependencies, dependents, members, change-impact locations, tests, projects and metadata references;
- 200 results: diagnostics, project documents, solution folders and dependency graph nodes; and
- 400 results: dependency graph edges.

Other bounded numeric inputs also publish their effective defaults: hierarchy, derived-type and callee depth is 3; operation-tree depth is 8; duplicate-code matching requires at least 3 statements; code-context windows are 10 lines on each side; transaction diff context is 3 lines; Code Action discovery returns at most 50 leaves; Fix All preparation allows at most 50 changed source documents and returns at most 20 affected document identities unless the caller supplies other values.
