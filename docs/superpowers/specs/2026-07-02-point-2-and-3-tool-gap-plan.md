# Point 2 And Point 3 Tool Gap Plan

**Status:** Approved planning-stage deliverable. No production or test code changes are part of this document.

**Goal:** Bring the implemented MCP tool surface into line with the documented end-state for the remaining point 2 and point 3 gaps by adding the still-documented missing tools and correcting the `get-control-flow-graph` contract behaviour.

## Summary

This plan covers two related gaps identified during the functional review:

- `Point 2`
  The repository documentation still describes a set of query and mutation tools that are not present in the current server registration surface.
- `Point 3`
  `get-control-flow-graph` is registered and documented, but its `regions` result is currently hard-coded to an empty array instead of reflecting Roslyn control-flow regions.

The implementation should move the server monotonically toward the documented catalogue. During the work, documentation may briefly lag behind implementation, but the repository should not continue to document a tool as shipping when the server does not actually expose it.

## Current Repository Baseline

The current registered tool surface is defined by:

- `src/Roslyn.Workbench.Mcp.Plugins.Core/BundledCoreToolRegistrar.cs`
- `src/Roslyn.Workbench.Mcp/WorkspaceLifecycleToolFactory.cs`
- `src/Roslyn.Workbench.Mcp/TransactionToolFactory.cs`
- `src/Roslyn.Workbench.Mcp/ServerStatusToolFactory.cs`

The current design and contract documents still describe tools that are absent from those registrations:

- `docs/RoslynMcpToolDesign.md`
- `docs/RoslynMcpToolContracts.md`

The current point 3 defect is in:

- `src/Roslyn.Workbench.Mcp.Plugins.Core/GetControlFlowGraphTool.cs`

The plan assumes the documented catalogue remains the intended target surface for these items.

## Scope

### Point 2: Missing documented tools

Implement the documented-but-missing read-only analysis/query tools:

- `get-code-context`
- `get-code-metrics`
- `find-callees`
- `find-overrides`
- `get-symbol-dependencies`
- `get-symbol-dependents`
- `find-unused-symbols`
- `find-duplicate-code`
- `get-dependency-graph`
- `find-dependency-cycles`
- `get-change-impact`
- `get-api-surface`
- `get-test-impact`
- `analyze-nullability`
- `analyze-async`
- `analyze-disposables`

Implement the documented-but-missing mutation / refactoring tools:

- `move-type-to-file`
- `move-type-to-namespace`
- `convert-to-async`
- `convert-expression-body`
- `convert-property`
- `convert-to-pattern-matching`
- `generate-constructor`
- `generate-tostring`
- `add-null-checks`
- `extract-interface`
- `extract-base-class`
- `change-signature`
- `generate-equals-hashcode`
- `generate-overrides`
- `implement-interface`

### Point 3: Existing tool contract correction

Correct `get-control-flow-graph` so that the returned `regions` payload is populated from Roslyn control-flow graph analysis instead of always returning an empty array.

## Design Constraints

- Keep the existing project boundaries intact:
  - shared DTOs and result shapes in `Roslyn.Workbench.Mcp.Contracts`
  - tool implementations in `Roslyn.Workbench.Mcp.Plugins.Core`
  - host-owned lifecycle and transaction orchestration in `Roslyn.Workbench.Mcp` and `Roslyn.Workbench.Mcp.Workspace`
- Preserve snapshot-precondition semantics. New query tools must not reinterpret stale spans, symbols, or locations against a newer workspace snapshot.
- Preserve the transaction pipeline. New mutation tools must stage candidate changes through the existing transaction flow and must not write directly to disk.
- Prefer Roslyn semantic APIs and existing code-action / MEF infrastructure over text-based transforms.
- Keep output deterministic and compatible with the existing result-bounding model until a separate result-bounding redesign is scheduled.

## Implementation Strategy

### Tranche 1: Contract and registration foundation

Add the missing request, response, and supporting contract types in `src/Roslyn.Workbench.Mcp.Contracts` before implementing handlers.

Expected work in this tranche:

- add the missing DTOs for the point 2 query tools
- add the missing request types for the point 2 mutation tools
- add any shared data contracts required for graphs, metrics, context, impact, and analysis payloads
- register every new tool name in `BundledCoreToolRegistrar.cs` only when its handler exists
- keep naming, result envelopes, and schema publication aligned with `docs/RoslynMcpToolDesign.md` and `docs/RoslynMcpToolContracts.md`

This tranche should not introduce host-level behaviour changes beyond the registration needed for newly implemented plugin tools.

### Tranche 2: Read-only analysis and graph tools

Implement the missing read-only tools first so the inspection catalogue reaches parity before the mutation catalogue expands.

Expected work in this tranche:

- introduce shared internal helpers in `src/Roslyn.Workbench.Mcp.Plugins.Core` for:
  - call graph traversal
  - override graph lookup
  - dependency and dependent graph extraction
  - project and symbol impact projection
  - metrics and context projection
- reuse existing helpers in `ToolExecutionHelpers.cs`, `InspectionProjectionFactory.cs`, and workspace resolution infrastructure rather than creating per-tool one-offs
- keep each tool snapshot-bound, deterministic, and bounded using the same conventions as the existing inspection tools

Recommended sequencing within this tranche:

1. `get-code-context`, `get-code-metrics`
2. `find-callees`, `find-overrides`
3. `get-symbol-dependencies`, `get-symbol-dependents`
4. `get-dependency-graph`, `find-dependency-cycles`
5. `get-change-impact`, `get-api-surface`, `get-test-impact`
6. `find-unused-symbols`, `find-duplicate-code`
7. `analyze-nullability`, `analyze-async`, `analyze-disposables`

This order favours the tools most likely to share reusable semantic graph and projection primitives.

### Tranche 3: `get-control-flow-graph` contract fix

Correct `GetControlFlowGraphTool` after the read-only helper tranche is in place.

Expected work in this tranche:

- replace the placeholder `Regions = []` behaviour with a real projection of Roslyn control-flow regions
- preserve the existing block and edge payload shape unless the documented contract requires a compatible normalisation alongside region support
- ensure region identifiers, nesting, and associated block relationships are stable enough for repeated calls over the same snapshot

This change is isolated enough to ship independently once the contract and tests are complete.

### Tranche 4: Public-API-backed mutation tools

Implement the mutation tools most likely to fit the existing transaction and code-action-wrapper architecture without new low-level abstractions.

Expected work in this tranche:

- `move-type-to-file`
- `move-type-to-namespace`
- `convert-to-async`
- `convert-expression-body`
- `convert-property`
- `convert-to-pattern-matching`
- `generate-constructor`
- `generate-tostring`
- `add-null-checks`

Implementation expectations:

- expose stable request contracts in the contracts project
- resolve the intended Roslyn action deterministically
- produce transaction-backed previews through the existing mutation pipeline
- fail clearly when no matching action is available for the selected symbol or location

### Tranche 5: Deeper Roslyn-backed mutation tools

Implement the remaining documented mutation tools that depend on more complex Roslyn integration, option handling, or action selection.

Expected work in this tranche:

- `extract-interface`
- `extract-base-class`
- `change-signature`
- `generate-equals-hashcode`
- `generate-overrides`
- `implement-interface`

Implementation expectations:

- prefer the same dedicated-wrapper pattern already used by the existing bundled code-action tools
- isolate tool-specific Roslyn integration behind focused internal helpers rather than broad new host abstractions
- if a tool cannot be implemented safely with current public Roslyn APIs, define the exact missing prerequisite seam before writing partial production code

## Documentation Reconciliation

After each implementation tranche:

- update `docs/RoslynMcpToolDesign.md` so it reflects only shipping tools and any still-explicitly-deferred items
- update `docs/RoslynMcpToolContracts.md` so request and response contracts match the actual CLR DTOs and emitted JSON shape
- document any implementation-ahead dedicated code-action wrapper tools as additional surface area rather than leaving them invisible

The repository should not continue to treat the current documented gaps as acceptable once the corresponding tranche has shipped.

## Testing And Acceptance Criteria

### Registration coverage

- verify each implemented point 2 tool is registered exactly once
- verify the name used in registration matches the documentation exactly
- verify no existing tool registration regresses while the catalogue expands

### Read-only tool coverage

For each new read-only tool, add tests that cover:

- representative success cases against the sample workspaces
- empty or no-match results
- invalid selector or snapshot-precondition failures
- bounded-result behaviour
- deterministic ordering across repeated executions

Primary existing test location:

- `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/InspectionToolsTests.cs`

Supporting test expansion may also be needed under:

- `test/Roslyn.Workbench.Mcp.Contracts.Test`
- `test/Roslyn.Workbench.Mcp.Test`

### `get-control-flow-graph` coverage

Add tests that cover:

- a method with branching only
- a method with nested control-flow regions
- a method with exception-related flow
- a contract assertion that `regions` is non-empty when Roslyn exposes regions for the target body

### Mutation tool coverage

For each new mutation tool, add tests that cover:

- preview generation through the transaction pipeline
- apply flow producing the expected staged source update
- stale snapshot rejection
- no-op or unavailable-action failures
- deterministic selection of the intended Roslyn action when multiple actions are present

## Risks And Dependencies

- Some documented mutation tools may require Roslyn integration that is less stable or less directly exposed than the currently implemented dedicated wrappers.
- Graph-shaped read-only tools may expose weaknesses in the current result-bounding helper before the separate bounding redesign is implemented.
- Contract breadth may grow quickly in `Roslyn.Workbench.Mcp.Contracts`; the implementation should keep DTOs grouped by behaviour and avoid overloading existing files.
- A small number of the documented tools may need the documentation to be refined once the exact Roslyn-backed behaviour is confirmed, but the implementation must not silently diverge from the documented contract.

## Definition Of Done

This point 2 and point 3 work is complete when:

- every tool listed in the scope section is implemented, registered, and tested, or is explicitly reclassified in the documentation with a repository-approved reason
- `get-control-flow-graph` returns real region data consistent with the documented contract
- the design and contract documents describe the actual shipping server surface rather than the previous aspirational gap state
- the relevant restore, build, format, and test commands required by the repository instructions pass for the behaviour-affecting implementation changes

