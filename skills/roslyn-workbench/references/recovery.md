# Recover failed operations

| Condition | Next step |
| --- | --- |
| `WorkspaceBusy` with `RetryRequest` | Retry after the competing operation completes. |
| Snapshot/selector mismatch | [Resolve a fresh target and snapshot together](selectors.md). |
| `WorkspaceOutOfDate` | [Check status, finish or roll back the transaction, then reload](workspaces.md). |
| `WorkspaceAuthorityChanged` | Restore the intended directory topology, roll back, then close/reopen. |
| Missing, expired or changed Code Action | [Rediscover or reprepare](code-actions.md). |
| Missing/stale review receipt | [Review the current revision again](transactions.md). |

## Uncertain or interrupted commits

If the commit result is unknown, inspect `workspace-status` and recovery state before retrying. Final disk application is non-cancellable; keep affected paths free of concurrent edits until it finishes.

After a Host restart, automatic recovery runs before MCP initialization. Inspect `server-status` with `detail: "Full"` for unresolved `data.recovery` entries. There is no public recovery tool: unresolved entries can require operator repair and another restart.

Preserve journal/manifests and owner records until recovery completes. `RecoveryVersionUnsupported` requires a compatible Host version. Durable recovery concerns interrupted disk commits; uncommitted staging, action handles and review receipts do not survive a restart.

## Diagnose or report an unexpected failure

Use `get-error-details` with the failure's `correlationId` for local diagnosis. Its `LocalDiagnostic` output is marked `safeForExternalSubmission: false` and can contain private workspace data.

When the user requests external reporting:

1. Call `prepare-error-report` with that correlation ID; preparation sends nothing.
2. Inspect the prepared destination and exact `payloadJson`. Exception messages are bounded but not filtered for secrets.
3. Call `submit-error-report` with the returned `submissionHandle`; the prepared payload and destination are already bound to it.

`ApprovalUnavailable` or `ErrorReportNotApproved` means nothing was sent. After the client interaction issue is resolved, prepare a fresh handle. Correlation IDs and report handles are process-local and expire.
