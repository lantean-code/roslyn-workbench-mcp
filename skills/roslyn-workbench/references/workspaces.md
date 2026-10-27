# Workspace lifecycle

## Select and load

Use `workspace-list` to reuse a matching workspace ID or alias. Pass an explicit `workspace` selector when several workspaces are open.

For `workspace-open`, use an absolute `.sln`, `.slnx` or `.csproj` path; `.slnf` is unsupported. Load the solution when cross-project relationships matter: opening one project cannot establish solution-wide impact. Loading evaluates repository MSBuild logic, and analysis can execute its analysers.

Normal project evaluation usually suffices. For a multi-target project whose outer evaluation has no compilable projects, set `msBuildProperties.targetFramework` to the intended framework. Inspect `data.loadDiagnostics` for skipped projects or missing analysers before drawing conclusions about coverage.

Let the Host infer the source root unless a specific repository boundary is needed. An explicit `workspaceRoot` must contain the loaded path and all retained projects. Under the default policy, evaluated documents outside that root remain queryable but read-only.

## Check freshness

`workspace-status` detects external changes and refreshes other-instance diagnostics; `workspace-list` only inventories sessions. Check status before mutation, after ordinary file edits or when results appear stale. Use `detail: "Full"` for diagnosis.

If another Host reports `WorkspaceInUse`, or instance status is unavailable, coordinate mutation ownership before staging. The final commit lock does not reserve ownership while changes are staged.

## Reload or close

Reload only after `WorkspaceOutOfDate` or `reloadRequired`:

1. Finish an active transaction; roll back a conflicted one.
2. Call `workspace-reload` with the workspace selector.
3. Retain the new snapshot and resolve targets again.

Reload preserves the workspace ID, alias and MSBuild properties but replaces its epoch and loaded view. `WorkspaceReloadNotRequired` means keep the current view. Reload reevaluates project membership; it does not maintain explicit project includes.

`workspace-close` rejects active or conflicted transactions instead of discarding them. A Host restart loses loaded workspaces and uncommitted staging; reopen and resolve new targets. Interrupted disk commits have a separate [recovery workflow](recovery.md).
