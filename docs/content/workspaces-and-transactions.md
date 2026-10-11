# Workspaces and safe transactions

Use small transactions to stage, inspect and either persist or discard one coherent source change. Start with [Loading and inspecting workspaces](workspaces.md) if you have not opened a trusted Workspace.

## Transaction workflow

Transaction tools are available in `transactional`, `approval-required` and `autonomous-trusted` operational modes. The default `inspection-only` mode publishes neither transaction tools nor mutation tools and rejects lower-level transaction acquisition or mutation staging. Only one loaded workspace may own the server's active transaction slot when mutation is enabled.

1. Check `workspace-status`, including cross-instance warnings.
2. Call `transaction-start` for the selected workspace.
3. Run mutation or Code Action tools. Successful operations stage a new revision; they do not write source files directly.
4. Use `transaction-history` to inspect, undo or redo staged revisions. In transactional or autonomous-trusted mode, use `transaction-preview` to inspect the staged change.
5. In approval-required mode, call `transaction-review` with the current snapshot and inspect its exact file operations, digest, validations and any explicitly requested bounded document diff.
6. Call `transaction-commit` to write the final staged source changes, or `transaction-rollback` to discard them. Approval-required commit also requires the current review's `receiptId` and asks the user to approve only that receipt.

Commit writes source files, not a Git commit. Run your normal build, tests and Git workflow after the Workbench transaction succeeds.

## Choose the review path

| Operational mode | Review | Commit approval |
| --- | --- | --- |
| `inspection-only` | No transaction tools | No source changes |
| `transactional` | `transaction-preview` | Approve this commit or commits for this Host session |
| `approval-required` | `transaction-review` | One-use approval bound to the returned receipt |
| `autonomous-trusted` | `transaction-preview` | No Host confirmation |

Compiler-impact validation is independently opt-in. When enabled, it is enforced by review and commit regardless of the mutation mode.

## Keep changes focused

Keep each transaction scoped to one coherent mutation or a tightly related set of changes. Start it when ready to mutate, inspect the resulting scope through `transaction-preview` or `transaction-review`, then commit or roll it back promptly. Do not use an open transaction to accumulate unrelated work. Run queries outside a transaction unless they need to inspect its staged solution, and treat broad solution-wide operations such as symbol rename as their own transaction. If the preview or review reveals an unexpectedly large or unrelated change set, roll back and reassess it before committing.

This keeps recovery work proportional to the current task because durable commit temporarily processes the original and intended content of every changed source file.

Queries run against the effective solution: the staged working solution while a transaction is active, otherwise the loaded baseline. Mutation, lifecycle and transaction operations require exclusive workspace access and may return `WorkspaceBusy` with a retry action instead of waiting in a server-side queue.

For Roslyn Code Fixes and refactorings, discover an action against the current revision and stage its opaque reference through the [three-tool Code Action workflow](code-actions.md). After a successful stage advances the revision, rediscover any subsequent action against that new current revision.

## Transactional confirmation

In `transactional` mode, the Host requests MCP elicitation immediately before commit. `commit-once` permits that invocation, `commit-for-session` permits later transaction commits for any Workspace served by the same running Host, and `do-not-commit` refuses it.

The process-local session choice is never persisted and is lost on restart. Refusal, cancellation, missing elicitation capability, client-policy rejection, interaction failure or an invalid response writes no files and leaves the transaction active for inspection, rollback or a later deliberate retry.

Confirmation only authorises the persistence attempt: every normal snapshot, conflict, filesystem, containment, commit-lock and recovery check still runs. `autonomous-trusted` omits this prompt but retains those deterministic checks.

## Exact-change review receipts

Approval-required receipts are process-local, one-use and valid for 15 minutes while the Workspace epoch, transaction, snapshot and revision remain unchanged. A declined, cancelled or unavailable prompt leaves both transaction and receipt active.

Accepted approval consumes the receipt before final validation or persistence begins; any later failure requires a new `transaction-review`. The Workspace rebuilds the commit plan and canonical SHA-256 identity before writing, so a stale or altered persistence set cannot reuse earlier approval.

## Generated-source warnings

Actual Roslyn source-generated documents and regular source under `obj` are not addressable mutation targets. For checked-in source that appears generated from its filename or opening auto-generated header, the default `warn` policy allows staging but returns a bounded `GeneratedSourceMutation` warning. Preview recalculates that warning across the complete active transaction. Approval-required review classifies each persistence entry as `project-source` or `generated-looking-source`; this classification is included in the receipt digest.

An operator can select `deny` and configure narrow Workspace-relative wildcard exceptions for intentionally maintained generated source. Exceptions never bypass containment, external-document, source-kind or intermediate-output denials. This deterministic policy does not request separate elicitation.

## Mutation provenance

Transaction preview and review provenance identifies every active staged operation by revision, operation name and summary. Revisions created by `stage-code-action` additionally contain response-local references to concise Code Action provider attribution: runtime type, assembly simple name and assembly version, plus the Fix All provider when one assembled the action.

The response-level `providers` object deduplicates those identities and is resolved by key rather than entry order. Undo excludes inactive revisions and redo restores their immutable provenance. Other mutation tools do not manufacture Code Action attribution, and provenance does not affect the exact persistence-set digest used by approval receipts.

## Compiler-impact validation

Enable `--commit-validation no-new-compiler-errors` only when you want a compiler-error gate. It compares affected loaded compilations with the transaction-start baseline; it is not an analyser or test run. Read [Compiler-impact validation](compiler-validation.md) for cost, scope and recovery instructions.

## Commit coordination and recovery

While commit is in progress, do not edit its target files or perform structural repository operations. Startup recovery also needs coordinated ownership. Read [Commit coordination and recovery](transaction-recovery.md) before using transactions alongside other development tools.

## Project membership

Source-file creation, deletion and same-directory rename do not update project membership. Default SDK compile globs normally reflect those changes after reload. For explicitly included, removed or excluded source paths, update the project file separately before relying on the reloaded project graph. Roslyn Workbench does not infer or persist that project-file change.


## Workspace trust

Open only fully trusted build inputs. Loading evaluates MSBuild and later operations may execute analysers with the Host user's permissions; this is not a sandbox. See [Workspace trust](workspaces.md#workspace-trust).

## Workspace lifecycle

See [Loading and inspecting workspaces](workspaces.md) for supported projects, build properties, roots, snapshots and reload.

## Cross-instance safety

Treat reported `WorkspaceInUse`, unavailable instance status or unreadable live-instance data as query-only until mutation ownership is coordinated. Instance status is advisory, not a lock on staging; see [Cross-instance safety](workspaces.md#cross-instance-safety).
