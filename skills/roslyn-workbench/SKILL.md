---
name: roslyn-workbench
description: Use Roslyn Workbench MCP for C#/.NET symbol navigation, semantic analysis, diagnostics, change and test impact, Code Actions, Fix All, renaming, formatting and transactional source changes. Also use for workspace lifecycle, stale snapshots and commit recovery.
---

# Roslyn Workbench

Treat the live tool schemas and `server-status` as authoritative for supported parameters, capabilities and operating mode.

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
