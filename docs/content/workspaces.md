# Loading and inspecting workspaces

Open a trusted solution or project, inspect its current snapshot, and reload when external changes make that snapshot stale. For source changes, continue with [Workspaces and safe transactions](workspaces-and-transactions.md).

## Workspace trust

A workspace is an executable input, not just a collection of source files. `workspace-open` evaluates MSBuild project logic, including repository-controlled projects and imports, before an agent can inspect every input. Later diagnostic and Code Action operations can load and execute project analyzers with the Host's operating system permissions. The Host does not sandbox workspace build logic or analyzers.

Open only a workspace whose source, project files, imported build logic, SDK configuration and analyzer dependencies are fully trusted. Inspect an untrusted repository outside Roslyn Workbench or in an operating-system sandbox first. The absence of a trust-confirmation request property is deliberate: a caller-provided confirmation would not isolate or validate executable repository content.

The connected MCP agent is also part of the local trust boundary. `get-error-details` may disclose local exception messages and Workspace context to that agent without a separate reporting-consent prompt. External submission remains a distinct sanitised and user-approved workflow; do not copy the local diagnostic record into a report or another service. See [Error reporting and privacy](error-reporting.md).

## Open a solution or project

After the caller has established that the workspace is fully trusted, `workspace-open` loads an absolute `.sln`, `.slnx` or `.csproj` into a workspace session. Use the returned workspace ID or alias to select it in later calls. When exactly one workspace is loaded, tools that accept an optional workspace selector may omit it.

Solutions may contain unsupported languages, projects without usable paths and non-SDK-style projects. The Host removes those projects from the loaded solution and returns `WorkspaceProjectSkipped` diagnostics. Loading fails when no supported SDK-style C# project remains. Unresolved analyser references are also removed and reported rather than preventing otherwise supported projects from loading.

## Workspace roots and external documents

By default, the workspace root is inferred from the loaded path. A caller may supply an existing absolute `workspaceRoot` that contains the loaded path to define the repository, coordination and transaction boundary. When the Host has configured allowed Workspace roots, the loaded path must be physically contained by one of them and a caller-supplied root may narrow but never widen that authority. If an inferred Git root lies above the matching allowed root, the effective Workspace root is capped at the allowed root. Every retained project must remain within the effective root.

An `artifactsPath` must name an existing absolute directory because it is an MSBuild evaluation setting, not a filesystem permission. Project files must remain inside `workspaceRoot`.

Under the default `allow-read-only` policy, source, additional and analyzer-config documents selected by evaluated projects may reside outside it, including linked files, package-provided source and generated intermediates; those documents are transparently queryable but read-only. The Host certifies their loaded content against disk and polls their individual fingerprints for later changes without watching or trusting their containing trees.

Under `reject-workspace`, any such evaluated external document rejects the complete open or reload. Source mutations remain strictly limited to `workspaceRoot` in either policy.

Admission is checked before MSBuild is invoked for the requested top-level path, again for every retained project after evaluation, and on reload. MSBuild itself remains trusted execution and may consume SDKs, imports, packages and metadata references outside the source authority boundary.

## Build configuration

`workspace-open` may also include `msBuildProperties` with the allowlisted global properties `artifactsPath`, `configuration`, `platform`, `targetFramework` and `runtimeIdentifier`. Omit the object when the project needs no caller-specific build configuration.

For a multi-target solution whose outer project evaluations do not expose the compiler target, supply the intended `targetFramework` so the Workspace contains target-specific compilable projects; the server does not silently choose one framework on the client's behalf.

Values apply only to that workspace's MSBuild evaluation and are retained across reloads; they do not change process environment variables. Unknown properties and empty values are rejected rather than passed through to MSBuild.

## Inspect status and reload

`workspace-status` reports the selected workspace state, current transaction, reload requirement, diagnostics, other live Roslyn Workbench instances and the effective configured external error-reporting consent state. `workspace-list` provides a lightweight identity list and the current global transaction owner; it does not refresh cross-instance diagnostics.

If source inputs change outside the loaded session, the workspace becomes out of date or its active transaction becomes conflicted. Do not reuse old source locations, spans or symbol results against a different immutable solution snapshot. Echo the complete published `snapshot` object unchanged as `expectedSnapshot`, including its opaque snapshot ID; an epoch and transaction revision are not sufficient identity on their own. Reload or resolve the target again as directed by the structured error.

## WSL filesystem performance

When the server runs under WSL and opens a workspace on a mounted Windows filesystem, `workspace-open` returns a `WorkspaceOnWindowsFileSystemFromWsl` warning. This layout is supported but can substantially reduce load and query performance. Prefer WSL-native storage or run the server directly on Windows.

## Cross-instance safety

Instance status is advisory. A durable inter-process lock serialises the final commit boundary, but it does not prevent two agents from independently staging transactions against the same workspace.

When `workspace-open` or `workspace-status` reports `WorkspaceInUse`, unavailable instance status or unreadable live-instance data:

- treat that workspace as query-only;
- use it only when necessary;
- expect query results to become stale as the other instance changes files; and
- coordinate mutation ownership before starting a transaction.

The Host does not infer coordination and does not reject `transaction-start` solely from advisory instance state.

Before committing, also read the [coordination and recovery rules](transaction-recovery.md).
