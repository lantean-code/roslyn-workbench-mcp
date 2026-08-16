# Unit 4 — Bundled query and mutation tools

Date: 2026-08-16

**Report status:** Completed.

## Scope and architecture

This review covered all 39 bundled registrations: 37 queries plus `rename-symbol` and `format-document`. It inspected every tool family, shared query and mutation bases, request/result projections, selectors, validation, limits and ordering; diagnostic and AsyncFixer services; plugin-scoped registration and DI; Host schema, binding, adapters and error projection; Workspace leases, staging, linked-document reconciliation, commit planning and reload; and current unit, integration, acceptance and scenario claims.

The representative query path is MCP arguments → Host binding/schema validation → plugin query adapter → Workspace query context/lease → request resolver → Roslyn or bundled analysis → bounded projection → MCP serialization. Mutations follow the corresponding exclusive mutation context into a candidate solution, Workspace validation and staging, then later durable commit and reload. The singleton handlers inspected retain no per-request mutable state. AsyncFixer analyzers are loaded once from application-local plugin content.

## Executable evidence and test claims

Every concrete tool has a Plugins.Core unit-test class. Additional component coverage exists for registration and metadata, public API locks, semantic inspection, solution search, selectors/snapshots, mutation pipelines, AsyncFixer and Workspace projections. Host tests cover schemas, catalogues and adapters; acceptance and scenario assets cover published inspection, selectors, mutation, boundedness, determinism and profiling.

Two narrow read-only calls through the current published Host reproduced failures without changing repository files:

- `get-control-flow-graph` at an ordinary in-body source location failed with `ArgumentException: Given operation has a non-null parent`, correlation `b4e0e56f-77aa-40d0-8684-0d258e17e40e`.
- `get-code-context` with `afterLines = int.MaxValue` failed after integer overflow with a negative `Enumerable.Range` count, correlation `721e23b7-be34-4ed7-99b7-ae9954ba0137`.

No broad suite was rerun. The material gaps are that CFG tests select an executable declaration rather than a nested statement/expression, code-context tests omit schema-permitted extreme line counts, rename-file tests stop at the candidate document name rather than commit/reload, and format-range tests do not bind the range to another document.

## Cross-unit conclusions

- `RWMCP3-001` affects all bundled tools because they trust the Workspace solution after acquisition.
- `RWMCP3-002` is strongly corroborated across direct and scoped project selectors; handlers do not independently validate authoritative target-framework identity.
- `RWMCP3-003` can admit both bundled mutations into concurrently retained transactions.
- `RWMCP3-004` is strongly corroborated because rename and format use the public snapshot tuple without an opaque snapshot identity.
- `RWMCP3-005` remains applicable to add/delete semantics; `renameFile=true` adds the distinct same-document path-transition defect recorded below.
- `RWMCP3-006` occurs before bundled handlers are entered.
- `RWMCP3-007` is inherited by both bundled mutations through the common writer, with no compensating handler check.

## Candidates

### RWMCP3-008 — Location-based CFG requests can throw for ordinary executable locations

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetControlFlowGraphTool.cs:41-67`; `src/Roslyn.Workbench.Mcp/ToolExecution/Plugins/PluginQueryMcpServerTool.cs:53-94`

A valid location inside a method expression or statement resolves to the innermost syntax node. `ControlFlowGraph.Create(node, semanticModel)` then receives an operation with a non-null parent and throws, producing a generic correlated Host failure instead of a graph or structured invalid request. Ascend to and validate a supported enclosing executable root before creating the graph, and cover statement, expression, method-body and unsupported locations through the handler and published adapter.

### RWMCP3-009 — Maximum permitted `afterLines` overflows the code-context window

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/GetCodeContextRequest.cs:17-28`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetCodeContextTool.cs:24-33`

The declared range accepts `afterLines = 2147483647`. For a selected line after the first, `endLine + afterLines` wraps negative and the resulting negative range count throws. Use overflow-safe remaining-line arithmetic and a meaningful bounded output maximum; cover maximum, near-maximum and end-of-file requests at handler and protocol boundaries.

### RWMCP3-010 — `renameFile=true` cannot complete a durable same-document path transition

**Severity:** P2  
**Confidence:** Medium  
**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Refactorings/RenameSymbolTool.cs:30-46`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceMutationCandidateValidator.cs:72-83`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitPlanner.cs:99-109,165-190`

Roslyn returns a same-`DocumentId` candidate whose document name/path and text change. Staging accepts the metadata transition because the text changed, but commit planning treats it as a replacement at the new path, expects that path to exist and creates no deletion or move for the old path. Model the transition explicitly as a validated move/delete-plus-create while preserving representation, links and project inclusion, or reject `renameFile=true` until supported. Add full stage/commit/reload coverage including explicit compile items.

### RWMCP3-011 — Format range silently ignores its document binding

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Abstractions/Workspace/Selectors/TextSpanSelector.cs:8-25`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/FormatDocumentRequest.cs:8-16`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Refactorings/FormatDocumentTool.cs:13-45`

A request can set top-level `document=A` and `range.document=B`. The binder validates both selectors, but the handler resolves only A and applies the range offsets to A without resolving or comparing B. Replace the nested type with a documentless span contract, or require both selectors to resolve to the same document and reject mismatches. Add handler and published-protocol mismatch tests.

## Exported questions

Unit 6 must verify published binder/schema and error outcomes for these paths. Unit 8 must assess whether current suites and scenarios genuinely cover repository-scale bounded work and mutation commit/reload semantics. All four candidates require independent Stage 4 validation.

