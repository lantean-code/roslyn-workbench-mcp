# Commit coordination and recovery

Transactions protect source changes from stale assumptions and interrupted persistence. They do not arbitrate simultaneous writes by other development tools during final file replacement.

## Before committing

Coordinate ownership of the source paths shown in preview or review. Avoid starting a commit while another user or tool is editing those paths or changing the repository structure.

## While commit is running

Final filesystem application is a short, non-cancellable coordinated boundary. While `transaction-commit` is in progress, the user and other development tools must not edit the source paths shown in its preview or review, switch branches, check out or reset paths, move directory trees, or replace directories with links. Edits completed before commit application remain subject to change detection and revalidation, but concurrent writes to a commit-owned target cannot be safely arbitrated during its final replacement. The `transaction-commit` response marks the end of commit application.

## Commit checks and durable evidence

`transaction-commit` rechecks Host authority and the source-file manifest before entering the persistence pipeline, and it is the only public operation that writes staged source changes to disk. If the Workspace path or effective root no longer physically complies with authority, commit fails without acquiring its filesystem lock or creating recovery state.

Commit does not modify project, props or targets files. It compiles affected loaded evaluations only when compiler-impact validation is explicitly enabled.

Durable recovery records protect interrupted commits and are surfaced by `server-status`. Startup recovery will not acquire a Workspace lock or write Workspace files for a record outside the current Host authority; the record remains visible without its absolute solution path until an operator restarts the Host with authority covering that Workspace.

## After a restart

Automatic recovery runs before MCP transport starts; avoid Workspace writes and structural repository operations until MCP initialisation marks the end of that recovery attempt, then call `server-status` with full detail and resolve any unfinished `recovery` entry before those operations proceed.

Do not delete unresolved recovery evidence to clear status or force startup. If recovery lies outside the current Host authority, the operator must restart with authority covering the affected Workspace before recovery can write to it.

## Unsupported recovery versions

`RecoveryVersionUnsupported` means the Host cannot safely interpret the retained owner record or manifest. Stop the Host and use the same or a newer compatible version. Never edit the version field or remove evidence merely to clear the error. See [Durable recovery compatibility](compatibility.md#durable-recovery-state).
