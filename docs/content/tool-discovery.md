# Tool discovery and result contracts

## Live tool inventory

Use MCP `tools/list` for the exact names, schemas and annotations published by the running Host. The startup operational mode, error-reporting policy and explicitly enabled plugins determine what is available.

The tool list does not vary with workspace or transaction state and does not change during the process lifetime. A tool that cannot run in the current state returns a structured state error rather than disappearing from discovery.

Inspection-only omits transaction and source-mutation tools but retains Code Action discovery and preparation. Transactional and autonomous-trusted use preview; approval-required uses review receipts. An omitted tool cannot be invoked by guessing its name. See [Operational modes](configuration.md#operational-modes).

`get-error-details` remains available for local failures. External-report preparation and submission are unavailable when startup consent is `never`. Prompt outcomes do not change the catalogue.

`server-status` reports the total mode-specific published tool count. With `detail: Full`, it also reports plugin load results, Code Action availability, effective operational policy, external-plugin enablement, connected-client elicitation support, startup warnings and unfinished recovery state.

Its Workspace configuration projection reports `workspaceAdmission`, `allowedWorkspaceRootCount` and `externalDocumentPolicy` without disclosing configured root paths. Internal Code Actions are reported as a component, not as a plugin.

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

### Failures and next actions

Handled failures return `ok: false`, a structured `error`, and an optional `continuation`; they do not publish a replacement snapshot. Its `kind` distinguishes a required exact tool call, a required choice between tools, a retry, a request revision, or external resolution; every variant includes a natural-language `instruction`.

Unexpected correlated failures also identify whether local details are available and project the current external-reporting state, so an agent does not attempt preparation when reporting is disabled or approval is unavailable. Agents should follow the returned continuation instead of guessing how to repair stale selectors, transaction conflicts or unavailable workspace state.

Read a failed result in this order:

1. `ok: false` means the operation did not succeed; do not treat it as a successful mutation.
2. Inspect `error.code` and its message.
3. Follow the returned `continuation` when present; its kind and target identify the next action.
4. If there is no continuation, explain the failure rather than inventing a recovery action.

For example, a `WorkspaceBusy` failure with a retry continuation means another exclusive Workspace operation is active. Retry as directed; do not replace the expected snapshot or assume the failed operation completed.

## Bounded collections

Collection-returning tools expose named result limits and return deterministic bounded collections with `hasMore`. Each tool publishes its curated default in the input schema and uses that same value when the limit is omitted. The Host's `DefaultMaxResults` remains available to third-party plugins as a compatibility baseline, but does not override the bundled tools' declared defaults. There is no global serialised byte ceiling.

When `hasMore` is true, an agent may request a larger collection if the extra context is useful or narrow the workspace scope, selector or filter. A larger request recomputes the deterministic result from the beginning; there are no generic cursors or continuation tokens.

Defaults differ by operation and result shape. Read each tool's schema or [tool reference](reference/tools/index.md) for its limit, depth and context-window defaults rather than applying a single global value.

The Host's internal publication and resource-default details are recorded in the [maintainer catalogue guide](https://github.com/lantean-code/roslyn-workbench-mcp/blob/develop/docs/maintainers/tool-catalogue.md).
