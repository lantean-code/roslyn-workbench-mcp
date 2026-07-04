# Tool Response Shaping Design

**Status:** Draft for review. No production or test code changes are part of this document.

**Goal:** Redesign tool metadata, request contracts, and runtime response contracts so the server returns the smallest agent-useful payload by default, while still allowing richer follow-up detail when a tool genuinely needs it.

## Summary

The current response model is too uniform for the actual tool surface.

Today, both plugin tools and server-owned tools are normalised into `ToolResult<TData>`, which makes every tool pay for the same top-level envelope whether or not the fields are useful for that tool. This produces avoidable context usage in agent conversations, especially for mutation, lifecycle, and transaction tools that often need only a compact success or failure result.

The server should move to:

- a tiny shared control/failure base
- family-specific success contracts
- opt-in request shaping for heavy branches
- dedicated follow-up tools where large detail is not needed on the first call
- slim `tools/list` publication by default, with accurate `inputSchema` and optional `outputSchema`

This is a greenfield contract redesign. Backward compatibility with the current response shapes is not a requirement.

## Current Repository Baseline

The current universal-response design is enforced in:

- `src/Roslyn.Workbench.Mcp.Contracts/Results/ToolResult.cs`
- `src/Roslyn.Workbench.Mcp.Plugins/PluginExecutionResult.cs`
- `src/Roslyn.Workbench.Mcp.Plugins/ToolExecutor.cs`
- `src/Roslyn.Workbench.Mcp/ServerToolMcpServerTool.cs`
- `src/Roslyn.Workbench.Mcp.Plugins/ToolSchemaFactory.cs`
- `src/Roslyn.Workbench.Mcp.Contracts/Validation/ContractValidator.cs`

The current design and contract docs also describe the common envelope as the default runtime result:

- `docs/RoslynMcpToolDesign.md`
- `docs/RoslynMcpToolContracts.md`

Representative examples of current payload mismatch include:

- `transaction-start`, `transaction-history`, `transaction-rollback`, and `transaction-commit`, which currently return tiny data payloads but still sit inside the full tool envelope
- staged mutation tools, which currently use top-level `changes` and mutation metadata even when a caller may only need confirmation that staging succeeded
- collection queries such as `search-symbols`, `find-references`, `find-implementations`, and `list-code-actions`, which repeat collection metadata patterns such as `ReturnedCount` and `HasMore`
- singleton semantic queries such as `get-symbol-info` and `get-code-context`, which mix essential payload fields with optional heavyweight detail

The repository already contains a related metadata-slimming plan in:

- `docs/superpowers/specs/2026-07-01-tool-metadata-slimming.md`

That work remains directionally correct, but it does not solve the larger runtime contract problem on its own.

## Problem Statement

The current design has four material issues.

### 1. One response envelope is being used for semantically different tools

`ToolResult<TData>` assumes that most tools benefit from the same shared fields:

- `Outcome`
- workspace snapshot metadata
- `Diagnostics`
- `Warnings`
- `Error`
- `RequiredAction`
- optional `Changes`

That assumption does not hold across the actual tool surface.

### 2. Mutation and lifecycle tools return more framing than they need

Many mutation and lifecycle calls are operational. The agent mainly needs to know:

- whether the operation succeeded
- what state or handle to use next
- whether a follow-up action is required

They do not usually need a full generic envelope plus empty diagnostics and warnings.

### 3. Query DTOs often carry redundant or overly eager detail

Several collection results use repeated patterns such as:

- `ReturnedCount`
- `HasMore`
- optional truncation metadata

`ReturnedCount` is often derivable from the collection length and therefore costs tokens without adding information. Some requests also default to verbose branches that should be opt-in instead, such as context snippets and documentation text.

### 4. Rich data is sometimes returned too early

Some information is useful, but not on the first call.

Examples:

- code-action descriptor context after listing actions
- symbol documentation after symbol discovery
- diff content after mutation staging
- document inventories after project or solution summary queries

Returning this data by default makes the common path more expensive than it needs to be.

## Design Goals

- Optimise the tool surface for agent consumption rather than human readability.
- Keep default responses compact and semantically focused.
- Preserve strong machine parsing and explicit contracts.
- Use one tiny shared base only for true cross-tool control semantics.
- Let tool families define their own success shapes.
- Move large optional branches behind explicit request parameters or follow-up tools.
- Keep `inputSchema` accurate and useful for discovery.
- Keep `outputSchema` accurate when published, but omit it by default for a slim `tools/list`.

## Non-Goals

- This design does not preserve wire compatibility with the current `ToolResult<TData>`-based runtime shape.
- This design does not require every tool to share the same success payload shape.
- This design does not attempt to keep rich status, inventory, or diagnostic tools artificially minimal when their purpose is inherently descriptive.
- This design does not remove structured machine-readable failures.

## Proposed Response Model

The response model should have two layers.

### Layer 1: Minimal shared control base

Every tool should share only the smallest cross-tool control semantics.

Recommended shared fields:

- `ok`
- `error`, when `ok` is `false`
- `next`, when a specific machine-meaningful follow-up hint is needed

Recommended error shape:

```json
{
  "ok": false,
  "error": {
    "code": "SnapshotMismatch",
    "message": "The request snapshot does not match the current workspace snapshot."
  },
  "next": "resolve-target-again"
}
```

This shared base should not automatically include:

- workspace identifiers
- workspace epoch
- transaction revision
- diagnostics
- warnings
- change summaries

Those belong only on tool families that genuinely need them.

### Layer 2: Family-specific success contracts

Each tool family should define its own success shape.

Outlier tools may use dedicated success contracts and reuse only the minimal failure/control base.

## Tool Family Design

### 1. Collection query result

Use for list-style query tools such as:

- `search-symbols`
- `find-references`
- `find-callers`
- `find-callees`
- `find-implementations`
- `find-overrides`
- `find-derived-types`
- `find-overloads`
- `get-symbol-members`
- `get-symbol-attributes`
- `get-symbol-dependencies`
- `get-symbol-dependents`
- `get-api-surface`
- `get-diagnostics`
- `list-code-actions`

Recommended default shape:

```json
{
  "ok": true,
  "items": [ ... ],
  "hasMore": true
}
```

Optional only when needed:

- `truncatedBy`
- `nextQueryHint`
- a family-specific lightweight handle or selector field

Default omissions:

- `returnedCount` when it equals `items.length`
- empty diagnostics and warnings
- workspace and transaction metadata unless the result is explicitly intended to be fed back as a snapshot-bearing selector

### 2. Singleton query result

Use for one-object tools such as:

- `resolve-symbol`
- `get-symbol-info`
- `go-to-definition`
- `get-code-context`
- `get-document-options`
- `get-operation-tree`
- `get-control-flow-graph`
- `analyze-data-flow`
- `analyze-control-flow`
- `workspace-status`, if kept query-shaped

Recommended default shape:

```json
{
  "ok": true,
  "value": { ... }
}
```

Optional only when needed:

- `snapshot`
- `related`
- one compact family-specific summary branch

The default payload should contain only the data needed for the tool's primary purpose.

### 3. Staged mutation result

Use for refactoring, code-fix, and formatting tools that stage source changes.

Recommended default shape:

```json
{
  "ok": true,
  "staged": true,
  "summary": "Extracted method",
  "transaction": {
    "revision": 3
  }
}
```

Optional only when requested:

- `changeSummary`
- `preview`
- `affectedSymbols`

This means the current automatic top-level `changes` concept should stop being universal. Change information should be part of the mutation success contract only when it is explicitly requested or the tool family considers it core.

### 4. Transaction and lifecycle result

Use for:

- `transaction-start`
- `transaction-history`
- `transaction-rollback`
- `transaction-commit`
- `workspace-open`
- `workspace-close`
- `workspace-reload`
- `workspace-list`

These results should be direct and operational.

Examples:

```json
{ "ok": true, "transaction": { "revision": 1 } }
```

```json
{ "ok": true, "committed": true }
```

```json
{ "ok": true, "workspace": { ... }, "projectCount": 5, "documentCount": 42 }
```

They should not inherit rich query framing by default.

### 5. Status and inventory result

Use for tools whose purpose is inherently descriptive:

- `server-status`
- `workspace-list`, if kept richer than other lifecycle tools
- `describe-code-action`
- similar inventory or health surfaces

These tools may remain richer than other families, but they should still default to the smallest broadly useful projection for their purpose.

### 6. Dedicated outlier result

Use when a tool is materially unlike the rest of its family.

The clearest current example is `transaction-preview`, because summary preview and detailed diff preview have meaningfully different payload needs.

Other likely outliers include:

- deep graph tools
- code-action description and preflight tools
- any future tool whose detailed branch is large and structurally different from its summary branch

## Request Shaping Strategy

The default rule should be:

> every tool returns the smallest payload that is broadly useful for its core purpose, and anything materially larger must be explicitly requested

The server should standardise on a small set of request-shaping patterns rather than introducing arbitrary one-off booleans everywhere.

Preferred shaping controls:

- `detail`
- `include`
- `limit`
- dedicated follow-up tools

### `detail`

Use when a tool can offer progressively richer projections of the same logical result.

Recommended values:

- `minimal`
- `standard`
- `full`

### `include`

Use when a tool has orthogonal optional branches that should not appear by default.

Examples:

- diagnostics
- context snippets
- documentation text
- diff
- inherited members
- reasons

### `limit`

Use only on genuine collection results. It should control item count, not attempt to describe payload verbosity.

### Dedicated follow-up tools

Prefer a separate follow-up tool when:

- the extra data is large
- the extra data is usually inspected only after narrowing to one result
- the extra data has a different access pattern than the parent result

Examples:

- symbol documentation or expanded detail after symbol search
- diff content after mutation success
- code-action preflight after action listing
- full document inventory after project summary

## Current-Surface Request Changes

The current contracts already contain some shaping fields. The redesign should build on the good ones and tighten the expensive defaults.

### Collection queries

- Default to compact items.
- Move verbose per-item fields behind `detail` or `include`.
- `find-references` should not default to context snippets.
- `find-callers` should not default to context snippets.
- Context-like fields should be opt-in or enabled only at a higher detail level.

### Singleton semantic queries

- `get-symbol-info` should default to core symbol identity, kind, declarations, and essential signature/type data.
- XML documentation should not default on.
- If member expansion is large, callers should prefer `get-symbol-members` rather than asking `get-symbol-info` to become a large composite DTO.

### Context and graph tools

- `get-code-context` should default to the code window only.
- diagnostics and enclosing symbol chains should be opt-in or tied to `detail`
- graph and tree tools should expose shape controls such as node or depth limits instead of relying only on response-size rejection

### Mutation tools

- success should default to a minimal staged confirmation
- preview, symbol impact, and detailed change lists should not return automatically
- richer mutation inspection should move to preview or follow-up query tools

### Transaction preview

`transaction-preview` already has the right directional split with `IncludeDiff`. The redesign should go further:

- summary preview should be the default
- detailed diff should be requested explicitly
- a dedicated diff-style follow-up tool is acceptable if the summary and detailed contracts diverge too much

### Status tools

- `server-status` should support a minimal default and an expanded mode for plugin, configuration, and recovery detail
- `workspace-status` should default without load diagnostics unless explicitly requested

### Code actions

- `list-code-actions` should default to the minimum descriptor set required to choose a next step
- richer preflight and parameterisation context belongs in `describe-code-action`
- if `CodeActionInfo` remains heavy, list-item and full-descriptor shapes should be split

## Metadata Strategy

The same slimming policy should apply to `tools/list`.

### `inputSchema`

`inputSchema` should remain accurate and published by default. It is the most valuable discovery surface before the first call.

### `outputSchema`

Published `outputSchema` should reflect the true family-specific runtime response when enabled.

The default startup mode should continue to omit published `outputSchema` to reduce `tools/list` cost. The server should not publish a fake compact schema that diverges from the real runtime payload.

### Tool descriptions

Descriptions should include only:

- purpose
- operational preconditions
- one short result hint when needed

They should not try to compensate for oversized contracts by restating DTO structure in prose.

## Execution-Pipeline Implications

The redesign requires replacing the current assumption that every tool runtime result becomes `ToolResult<TData>`.

The main implementation seams that must change are:

- `src/Roslyn.Workbench.Mcp.Contracts/Results/ToolResult.cs`
- `src/Roslyn.Workbench.Mcp.Plugins/PluginExecutionResult.cs`
- `src/Roslyn.Workbench.Mcp.Plugins/ToolExecutor.cs`
- `src/Roslyn.Workbench.Mcp/ServerToolMcpServerTool.cs`
- `src/Roslyn.Workbench.Mcp.Plugins/ToolSchemaFactory.cs`
- `src/Roslyn.Workbench.Mcp.Contracts/Validation/ContractValidator.cs`

The server should move from:

- one universal runtime result envelope

to:

- one tiny shared failure/control base
- multiple family success shapes
- per-tool response registration that points to the correct family or dedicated contract

## Recommended Migration Order

### Phase 1: Introduce the new response abstraction layer

Replace the hard dependency on `ToolResult<TData>` in the execution and validation pipeline with a smaller shared base plus family-specific response contracts.

### Phase 2: Convert transaction and lifecycle tools

These are the clearest wins and the lowest-risk family to migrate first.

Expected tools:

- `workspace-open`
- `workspace-list`
- `workspace-close`
- `workspace-status`
- `workspace-reload`
- `transaction-start`
- `transaction-history`
- `transaction-rollback`
- `transaction-commit`

### Phase 3: Convert staged mutation tools

Adopt minimal staged-success results and stop returning large mutation framing by default.

### Phase 4: Convert collection query tools

Standardise collection success contracts and remove repeated metadata such as `ReturnedCount` where it is redundant.

### Phase 5: Convert singleton semantic queries

Add request shaping and trim default payloads for symbol, context, graph, and analysis tools.

### Phase 6: Split dedicated outliers

Handle:

- `transaction-preview`
- `describe-code-action`
- deep graph and analysis tools
- any tool whose summary and detailed forms have materially different structures

## Testing Strategy

The test model should change from:

- every tool deserialises as `ToolResult<T>`

to:

- each tool deserialises as its declared family or dedicated contract
- shared failure/control semantics are consistent everywhere
- default responses are compact
- opt-in request parameters add only the requested branches
- follow-up tools return omitted detail without forcing the parent tool to over-return

Key test areas:

- response-family contract tests
- metadata publication tests for family-specific `outputSchema`
- default compactness assertions
- opt-in branch assertions
- outlier tool tests for summary versus detailed mode divergence

## Risks

- Removing the universal result envelope will touch execution, validation, metadata generation, and many existing tests.
- Some current request contracts use booleans where a future `detail` or `include` structure may be cleaner; migration must avoid replacing one inconsistent scheme with another.
- Deep graph and analysis tools may still need explicit node, edge, or text-shaping limits even after family splitting.
- A few tools may initially look like they fit a family but still need dedicated contracts after implementation review.

## Decision Summary

- Keep one tiny shared control and failure base.
- Do not keep one universal success envelope.
- Use family-specific success contracts by default.
- Allow dedicated outlier contracts when family fitting would make the response worse.
- Keep default responses minimal and agent-oriented.
- Move rich optional data behind request-shaping controls or separate follow-up tools.
- Keep `inputSchema` accurate and published by default.
- Omit `outputSchema` by default, but publish the real family-specific shape when enabled.
