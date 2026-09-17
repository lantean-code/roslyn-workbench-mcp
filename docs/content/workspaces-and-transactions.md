# Workspaces and safe transactions

## Workspace trust

A workspace is an executable input, not just a collection of source files. `workspace-open` evaluates MSBuild project logic, including repository-controlled projects and imports, before an agent can inspect every input. Later diagnostic and Code Action operations can load and execute project analyzers with the Host's operating system permissions. The Host does not sandbox workspace build logic or analyzers.

Open only a workspace whose source, project files, imported build logic, SDK configuration and analyzer dependencies are fully trusted. Inspect an untrusted repository outside Roslyn Workbench or in an operating-system sandbox first. The absence of a trust-confirmation request property is deliberate: a caller-provided confirmation would not isolate or validate executable repository content.

The connected MCP agent is also part of the local trust boundary. `get-error-details` may disclose local exception messages and Workspace context to that agent without a separate reporting-consent prompt. External submission remains a distinct sanitised and user-approved workflow; do not copy the local diagnostic record into a report or another service. See [Error reporting and privacy](error-reporting.md).

## Workspace lifecycle

After the caller has established that the workspace is fully trusted, `workspace-open` loads an absolute `.sln`, `.slnx` or `.csproj` into a workspace session. Use the returned workspace ID or alias to select it in later calls. When exactly one workspace is loaded, tools that accept an optional workspace selector may omit it.

Solutions may contain unsupported languages, projects without usable paths and non-SDK-style projects. The Host removes those projects from the loaded solution and returns `WorkspaceProjectSkipped` diagnostics. Loading fails when no supported SDK-style C# project remains. Unresolved analyser references are also removed and reported rather than preventing otherwise supported projects from loading.

By default, the workspace root is inferred from the loaded path. A caller may supply an existing absolute `workspaceRoot` that contains the loaded path to define the repository, coordination and transaction boundary. When the Host has configured allowed Workspace roots, the loaded path must be physically contained by one of them and a caller-supplied root may narrow but never widen that authority. If an inferred Git root lies above the matching allowed root, the effective Workspace root is capped at the allowed root. Every retained project must remain within the effective root.

`workspace-open` may also include `msBuildProperties` with the allowlisted global properties `artifactsPath`, `configuration`, `platform`, `targetFramework` and `runtimeIdentifier`. Omit the object when the project needs no caller-specific build configuration. For a multi-target solution whose outer project evaluations do not expose the compiler target, supply the intended `targetFramework` so the Workspace contains target-specific compilable projects; the server does not silently choose one framework on the client's behalf. Values apply only to that workspace's MSBuild evaluation and are retained across reloads; they do not change process environment variables. Unknown properties and empty values are rejected rather than passed through to MSBuild.

An `artifactsPath` must name an existing absolute directory because it is an MSBuild evaluation setting, not a filesystem permission. Project files must remain inside `workspaceRoot`. Under the default `allow-read-only` policy, source, additional and analyzer-config documents selected by evaluated projects may reside outside it, including linked files, package-provided source and generated intermediates; those documents are transparently queryable but read-only. The Host certifies their loaded content against disk and polls their individual fingerprints for later changes without watching or trusting their containing trees. Under `reject-workspace`, any such evaluated external document rejects the complete open or reload. Source mutations remain strictly limited to `workspaceRoot` in either policy.

Admission is checked before MSBuild is invoked for the requested top-level path, again for every retained project after evaluation, and on reload. MSBuild itself remains trusted execution and may consume SDKs, imports, packages and metadata references outside the source authority boundary.

When the server runs under WSL and opens a workspace on a mounted Windows filesystem, `workspace-open` returns a `WorkspaceOnWindowsFileSystemFromWsl` warning. This layout is supported but can substantially reduce load and query performance. Prefer WSL-native storage or run the server directly on Windows.

`workspace-status` reports the selected workspace state, current transaction, reload requirement, diagnostics, other live Roslyn Workbench instances and the effective configured external error-reporting consent state. `workspace-list` provides a lightweight identity list and the current global transaction owner; it does not refresh cross-instance diagnostics.

If source inputs change outside the loaded session, the workspace becomes out of date or its active transaction becomes conflicted. Do not reuse old source locations, spans or symbol results against a different immutable solution snapshot. Echo the complete published `snapshot` object unchanged as `expectedSnapshot`, including its opaque snapshot ID; an epoch and transaction revision are not sufficient identity on their own. Reload or resolve the target again as directed by the structured error.

## Cross-instance safety

Instance status is advisory. A durable inter-process lock serialises the final commit boundary, but it does not prevent two agents from independently staging transactions against the same workspace.

When `workspace-open` or `workspace-status` reports `WorkspaceInUse`, unavailable instance status or unreadable live-instance data:

- treat that workspace as query-only;
- use it only when necessary;
- expect query results to become stale as the other instance changes files; and
- coordinate mutation ownership before starting a transaction.

The Host does not infer coordination and does not reject `transaction-start` solely from advisory instance state.

## Transaction workflow

Transaction tools are available in `transactional`, `approval-required` and `autonomous-trusted` operational modes. The default `inspection-only` mode publishes neither transaction tools nor mutation tools and rejects lower-level transaction acquisition or mutation staging. Only one loaded workspace may own the server's active transaction slot when mutation is enabled.

1. Check `workspace-status`, including cross-instance warnings.
2. Call `transaction-start` for the selected workspace.
3. Run mutation or Code Action tools. Successful operations stage a new revision; they do not write source files directly.
4. Use `transaction-history` to inspect, undo or redo staged revisions. In transactional or autonomous-trusted mode, use `transaction-preview` to inspect the staged change.
5. In approval-required mode, call `transaction-review` with the current snapshot and inspect its exact file operations, digest, validations and any explicitly requested bounded document diff.
6. Call `transaction-commit` to write the final staged source changes, or `transaction-rollback` to discard them. Approval-required commit also requires the current review's `receiptId` and asks the user to approve only that receipt.

Approval-required receipts are process-local, one-use and valid for 15 minutes while the Workspace epoch, transaction, snapshot and revision remain unchanged. A declined, cancelled or unavailable prompt leaves both transaction and receipt active. Accepted approval consumes the receipt before final validation or persistence begins; any later failure requires a new `transaction-review`. The Workspace rebuilds the commit plan and canonical SHA-256 identity before writing, so a stale or altered persistence set cannot reuse earlier approval.

## Compiler-impact validation

An operator can independently enable `--commit-validation no-new-compiler-errors` for any mutation-enabled operational mode. When enabled, call `transaction-validate` with the current snapshot after staging to see whether the transaction introduced compiler errors before attempting review or commit. Review and both commit workflows enforce the same gate automatically; commit performs an authoritative final validation before its filesystem work.

Validation compares the transaction-start solution with the current staged revision for changed loaded project evaluations and their loaded transitive dependants. A transaction at revision zero is a complete empty comparison and preserves the normal no-change commit result. Existing compiler errors are permitted. A moved error is not considered new, while an additional occurrence of the same error is. The result reports evaluated projects, error counts, elapsed duration, introduced diagnostics, structured incomplete reasons and limitations. It covers only the target-framework evaluations loaded by the Workspace and does not run third-party analyser validation or tests.

If new errors are reported, correct or restage the transaction and validate the new snapshot. Incomplete validation cannot be recovered by restaging the same transaction because its result is bound to the captured baseline and loaded project evaluations. Follow the rollback continuation, resolve the reported load, compilation or external-input condition, reload the Workspace and start a new transaction. A failed validation writes no files, creates no review receipt and leaves the transaction active until it is rolled back.

Keep each transaction scoped to one coherent mutation or a tightly related set of changes. Start it when ready to mutate, inspect the resulting scope through `transaction-preview` or `transaction-review`, then commit or roll it back promptly. Do not use an open transaction to accumulate unrelated work. Run queries outside a transaction unless they need to inspect its staged solution, and treat broad solution-wide operations such as symbol rename as their own transaction. If the preview or review reveals an unexpectedly large or unrelated change set, roll back and reassess it before committing. This keeps recovery work proportional to the current task because durable commit temporarily processes the original and intended content of every changed source file.

Queries run against the effective solution: the staged working solution while a transaction is active, otherwise the loaded baseline. Mutation, lifecycle and transaction operations require exclusive workspace access and may return `WorkspaceBusy` with a retry action instead of waiting in a server-side queue.

For Roslyn Code Fixes and refactorings, discover an action against the current revision and stage its opaque reference through the [three-tool Code Action workflow](code-actions.md). After a successful stage advances the revision, rediscover any subsequent action against that new current revision.

`transaction-commit` rechecks Host authority and the source-file manifest before entering the persistence pipeline, and it is the only public operation that writes staged source changes to disk. If the Workspace path or effective root no longer physically complies with authority, commit fails without acquiring its filesystem lock or creating recovery state. Commit does not modify project, props or targets files. It compiles affected loaded evaluations only when compiler-impact validation is explicitly enabled. Durable recovery records protect interrupted commits and are surfaced by `server-status`. Startup recovery will not acquire a Workspace lock or write Workspace files for a record outside the current Host authority; the record remains visible without its absolute solution path until an operator restarts the Host with authority covering that Workspace.

In `transactional` mode, the Host requests MCP elicitation immediately before commit. `commit-once` permits that invocation, `commit-for-session` permits later transaction commits for any Workspace served by the same running Host, and `do-not-commit` refuses it. The process-local session choice is never persisted and is lost on restart. Refusal, cancellation, missing elicitation capability, client-policy rejection, interaction failure or an invalid response writes no files and leaves the transaction active for inspection, rollback or a later deliberate retry. Confirmation only authorises the persistence attempt: every normal snapshot, conflict, filesystem, containment, commit-lock and recovery check still runs. `autonomous-trusted` omits this prompt but retains those deterministic checks.

The non-cancellable filesystem application phases of `transaction-commit` and automatic startup recovery are short coordinated boundaries. While `transaction-commit` is in progress, the user and other development tools must not edit the source paths shown in its preview or review, switch branches, check out or reset paths, move directory trees, or replace directories with links. Edits completed before commit application remain subject to change detection and revalidation, but concurrent writes to a commit-owned target cannot be safely arbitrated during its final replacement. The `transaction-commit` response marks the end of commit application. Automatic recovery runs before MCP transport starts; avoid Workspace writes and structural repository operations until MCP initialisation marks the end of that recovery attempt, then call `server-status` with full detail and resolve any unfinished `recovery` entry before those operations proceed.

Source-file creation, deletion and same-directory rename do not update project membership. Default SDK compile globs normally reflect those changes after reload. For explicitly included, removed or excluded source paths, update the project file separately before relying on the reloaded project graph. Roslyn Workbench does not infer or persist that project-file change.
