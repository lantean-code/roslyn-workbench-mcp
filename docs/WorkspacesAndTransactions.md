# Workspaces and transactions

## Workspace trust

A workspace is an executable input, not just a collection of source files. `workspace-open` evaluates MSBuild project logic, including repository-controlled projects and imports, before an agent can inspect every input. Later diagnostic and Code Action operations can load and execute project analyzers with the Host's operating system permissions. The Host does not sandbox workspace build logic or analyzers.

Open only a workspace whose source, project files, imported build logic, SDK configuration and analyzer dependencies are fully trusted. Inspect an untrusted repository outside Roslyn Workbench or in an operating-system sandbox first. The absence of a trust-confirmation request property is deliberate: a caller-provided confirmation would not isolate or validate executable repository content.

## Workspace lifecycle

After the caller has established that the workspace is fully trusted, `workspace-open` loads an absolute `.sln`, `.slnx` or `.csproj` into a workspace session. Use the returned workspace ID or alias to select it in later calls. When exactly one workspace is loaded, tools that accept an optional workspace selector may omit it.

Solutions may contain unsupported languages, projects without usable paths and non-SDK-style projects. The Host removes those projects from the loaded solution and returns `WorkspaceProjectSkipped` diagnostics. Loading fails when no supported SDK-style C# project remains. Unresolved analyser references are also removed and reported rather than preventing otherwise supported projects from loading.

By default, the workspace root is inferred from the loaded path. A caller may supply an existing absolute `workspaceRoot` that contains the loaded path to define the repository, coordination and transaction boundary. Every retained project and source document must remain within that root.

When the server runs under WSL and opens a workspace on a mounted Windows filesystem, `workspace-open` returns a `WorkspaceOnWindowsFileSystemFromWsl` warning. This layout is supported but can substantially reduce load and query performance. Prefer WSL-native storage or run the server directly on Windows.

`workspace-status` reports the selected workspace state, current transaction, reload requirement, diagnostics and other live Roslyn Workbench instances. `workspace-list` provides a lightweight identity list and the current global transaction owner; it does not refresh cross-instance diagnostics.

If source inputs change outside the loaded session, the workspace becomes out of date or its active transaction becomes conflicted. Do not reuse old source locations, spans or symbol results against a newer workspace epoch or transaction revision. Reload or resolve the target again as directed by the structured error.

## Cross-instance safety

Instance status is advisory. A durable inter-process lock serialises the final commit boundary, but it does not prevent two agents from independently staging transactions against the same workspace.

When `workspace-open` or `workspace-status` reports `WorkspaceInUse`, unavailable instance status or unreadable live-instance data:

- treat that workspace as query-only;
- use it only when necessary;
- expect query results to become stale as the other instance changes files; and
- coordinate mutation ownership before starting a transaction.

The Host does not infer coordination and does not reject `transaction-start` solely from advisory instance state.

## Transaction workflow

Only one loaded workspace may own the server's active transaction slot.

1. Check `workspace-status`, including cross-instance warnings.
2. Call `transaction-start` for the selected workspace.
3. Run mutation or Code Action tools. Successful operations stage a new revision; they do not write source files directly.
4. Use `transaction-preview` and `transaction-history` to inspect, undo or redo staged revisions.
5. Call `transaction-commit` to write the final staged source changes, or `transaction-rollback` to discard them.

Queries run against the effective solution: the staged working solution while a transaction is active, otherwise the loaded baseline. Mutation, lifecycle and transaction operations require exclusive workspace access and may return `WorkspaceBusy` with a retry action instead of waiting in a server-side queue.

`transaction-commit` rechecks the source-file manifest and is the only public operation that writes staged source changes to disk. It does not compile the solution or modify project, props or targets files. Durable recovery records protect interrupted commits and are surfaced by `server-status`.
