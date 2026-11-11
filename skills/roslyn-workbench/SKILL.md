---
name: roslyn-workbench
description: Use Roslyn Workbench MCP for C#/.NET symbol navigation, semantic analysis, diagnostics, change and test impact, Code Actions, Fix All, renaming, formatting and transactional source changes. Also use for workspace lifecycle, stale snapshots and commit recovery.
---

# Roslyn Workbench

Treat the live tool schemas and `server-status` as authoritative for supported parameters, capabilities and operating mode.

## Discover tools by task

| Group | When to discover it | Starting points |
| --- | --- | --- |
| Workflow | Open or manage a trusted workspace, recover a failed call, discover/prepare Roslyn Code Actions, or stage and persist source changes when mutations are enabled. | `server-status`, workspace lifecycle, error details/reporting, transactions, rename, formatting and Code Actions. |
| Everyday inspection | Understand projects and source, find a symbol, inspect its members or usages, or investigate compiler/analyser diagnostics. | `get-solution-structure`, `get-project-details`, `get-document-outline`, `get-code-context`, `search-symbols`, `get-symbol-info`, `get-symbol-members`, `find-references`, `get-diagnostics`. |
| Specialist inspection | Answer a specific relationship, flow, architecture, configuration or focused analysis question. | Callers/callees, implementations/overrides, inheritance, overloads, attributes, partial declarations, API surface, document options, dependencies/cycles, change/test impact, async/nullability/disposal, unused symbols, duplicate code, data/control flow and operation trees. |

When the client defers tool definitions, use its discovery mechanism to search for the relevant capability or exact tool name, then read the returned schema before calling. A tool missing from the currently loaded model context may still be published. These are selection groups, not loading controls or a reduced server profile; startup policy and plugins still determine availability. Do not assume a policy-omitted tool can be enabled through discovery.

Use the narrowest query that answers the task. For example, search for incoming callers with `find-callers`, outgoing call targets with `find-callees`, architecture dependencies with `get-dependency-graph`, circular dependencies with `find-dependency-cycles`, or variable reads/writes and captures with `analyze-data-flow`. For full source declaration spans across partial parts, discover `get-partial-declarations`; `get-symbol-info` provides symbol metadata and declaration-name locations. Bind a known source occurrence with `resolve-symbol`, or navigate its declaration with `go-to-definition`.

Read only the references needed for the current task. Clients supporting the advertised `io.modelcontextprotocol/skills` extension can discover this skill with `skills/list` or retrieve its entry with `skills/get`, then verify file digests and sizes before loading. Other resource-capable clients can read `skill://roslyn-workbench/SKILL.md` directly. Reading an MCP resource does not itself install or activate a native client skill.

## Follow the relevant workflow

Reuse an open workspace or open the required solution/project. Load the reference for the task; use [selectors](references/selectors.md) when turning query results into subsequent requests. For source edits, read [transactions](references/transactions.md) alongside the relevant mutation reference.

Queries see loaded source, including active staged changes. Edits remain in memory until `transaction-commit`.

| Task | Reference |
| --- | --- |
| Open, select, check freshness, reload or close a workspace | [workspaces.md](references/workspaces.md) |
| Reuse symbol/location results, disambiguate projects, preserve snapshots, handle truncation | [selectors.md](references/selectors.md) |
| Find symbols, definitions, references, callers, callees, overloads or source context | [navigation.md](references/navigation.md) |
| Inspect projects/documents, type hierarchy, implementations, overrides, partial declarations, members, attributes or API surface | [structure.md](references/structure.md) |
| Investigate compiler/analyser, async, nullability or disposal findings | [diagnostics.md](references/diagnostics.md) |
| Analyse data/control flow, operation trees or control-flow graphs | [flow-analysis.md](references/flow-analysis.md) |
| Assess change/test impact, dependencies, cycles, unused symbols or duplicate code | [impact-analysis.md](references/impact-analysis.md) |
| Start, preview/review, validate, undo/redo, commit or roll back changes | [transactions.md](references/transactions.md) |
| Discover and stage fixes/refactorings or prepare Fix All | [code-actions.md](references/code-actions.md) |
| Rename symbols or format source | [rename-format.md](references/rename-format.md) |
| Resolve failed calls, interrupted commits or diagnostic reports | [recovery.md](references/recovery.md) |
