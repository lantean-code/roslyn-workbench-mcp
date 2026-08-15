# Agent guide

This guide describes how an MCP agent should use Roslyn Workbench safely and efficiently. It supplements the instructions returned during MCP initialisation; the running server's tool catalogue, schemas, structured results and next actions remain authoritative.

## Trust boundary

Open only fully trusted C# workspaces. Loading a workspace evaluates MSBuild project logic, and later operations can load and execute project analysers with the Host process's operating-system permissions. Roslyn Workbench does not sandbox this code.

When a project needs caller-specific build evaluation, `workspace-open` accepts the allowlisted `msBuildProperties` values `artifactsPath`, `configuration`, `platform`, `targetFramework` and `runtimeIdentifier`. Supply only values required by that workspace; standard SDK, NuGet and Visual Studio locations do not need to be repeated. These values control MSBuild evaluation and do not grant filesystem permissions. Roslyn-evaluated source, additional and analyzer-config documents outside `workspaceRoot` are transparently queryable read-only inputs, while source mutations remain restricted to `workspaceRoot`.

Treat the connected MCP agent as part of the local trust boundary. Local error details can contain exception messages and Workspace context. Do not copy them into an external report; use the server's explicit preparation, review and consent workflow when external reporting is appropriate.

## Discover before acting

Use `tools/list` for the live tool inventory and schemas. Call `server-status` with full detail near the start of a session to inspect startup warnings, recovery state, component availability and the published tool count.

Prefer queries before mutations. Resolve symbols, documents and projects from the current workspace state instead of guessing paths or source spans.

Prefer standard compiler and analyzer diagnostics when assessing code quality. `analyze-async` is a focused view of the six bundled AsyncFixer diagnostics and compiler diagnostic CS4014; it runs independently of whether AsyncFixer is installed in the target solution while respecting the project's analyzer configuration. Use `get-diagnostics` when you need the wider configured diagnostic set. Roslyn Workbench does not publish its own code-metric scale, so use the repository's normal build or metrics workflow when a task requires Microsoft code metrics.

## Workspace state

Workspace epochs, transaction revisions and structured next actions are authoritative. When a result says the workspace or selector is stale, reload or resolve the target again. Do not reuse source spans, symbol locations, opaque Code Action references or other snapshot-bound values against a newer state.

Multiple workspaces may be open, but only one may own the active transaction slot. Cross-process status is advisory; follow reported coordination guidance and allow the commit pipeline to enforce its durable boundary.

## Mutation workflow

Use this sequence for mutations:

1. Complete the queries needed to identify the change.
2. Start a transaction only when ready to mutate.
3. Apply one coherent mutation or a tightly related set of mutations.
4. Inspect `transaction-preview` and, when useful, `transaction-history`.
5. Call `transaction-commit` or `transaction-rollback` promptly.

Do not accumulate unrelated work in an open transaction. Run queries outside a transaction unless they must observe staged state. Treat broad solution-wide operations, such as a symbol rename, as standalone transactions. If a preview is unexpectedly large or contains unrelated changes, roll it back and reassess the operation.

`transaction-commit` writes the staged source changes to disk; it does not compile the solution, edit project files or create a Git commit. Validate and commit through the repository's normal development workflow after the Workbench transaction succeeds.

Small, frequent Workbench transactions keep previews reviewable and bound the temporary original and intended source content processed by durable commit and recovery. Query-heavy sessions do not need an open transaction.

## Failures and recovery

Follow the structured next action returned by a failed tool call. Do not retry stale inputs unchanged when the server asks for a reload, a new selector, a newer revision or rollback.

Use `get-error-details` only for an unexpected correlated failure. Unfinished durable recovery is reported by `server-status`; resolve it before attempting further mutations.

## More documentation

The release documentation in the same tagged repository includes detailed workspace and transaction semantics, tool discovery and result contracts, configuration, Code Action workflows, plugin authoring and error-reporting privacy boundaries.
