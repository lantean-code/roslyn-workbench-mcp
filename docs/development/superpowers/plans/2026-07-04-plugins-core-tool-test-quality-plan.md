# Plugins.Core Tool Test Quality Implementation Plan

**Status:** Historical implementation plan. The later comprehensive reassessment in [Tool Test Inventory](../../Tool%20Test%20Inventory.md) found no known untested supported tool behaviour and records the disposition of defensive branches. This document is not an active worklist.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Raise the `Roslyn.Workbench.Mcp.Plugins.Core` tool tests to true unit-test quality, with every tool covered to 100% line and branch coverage and each test name reflecting the exact state variation being exercised.

**Architecture:** Introduce thinner, injectable seams around tool execution helpers and Roslyn/workspace resolution so that tool tests can target branch behaviour directly instead of constructing large real-workspace scenarios for every variation. Keep a smaller set of fixture-backed integration-style tests where the behaviour genuinely depends on Roslyn end-to-end execution, but move branch-heavy validation and rejection paths into direct unit tests.

**Tech Stack:** .NET 10, xUnit, Moq, AwesomeAssertions, Roslyn, existing `Roslyn.Workbench.Mcp.TestSupport` fixtures.

---

## Recommended Approach

### Option 1: Fixture-heavy only

Keep the current pattern and improve each dedicated tool test by adding more sample-workspace scenarios.

- Pros: Lowest production churn.
- Cons: Poor branch isolation, slower tests, hard-to-hit error branches, naming drifts toward scenario labels instead of real state variation.

### Option 2: Hybrid seam-first refactor

Add targeted seams around static helper usage, selector resolution, snapshot validation, code action staging, and bounded-result shaping. Rewrite direct tool tests to use mocks for rejection and branch paths, while preserving a thin real-workspace happy-path layer.

- Pros: Best balance of coverage quality, maintainability, and production churn.
- Cons: Requires modest refactoring before broad test rewrites.

### Option 3: Deep service extraction

Push most tool logic into dedicated services and leave tool classes as thin adapters.

- Pros: Excellent long-term testability.
- Cons: Highest churn, larger behavioural risk, too broad for the immediate goal.

### Recommendation

Use **Option 2**.

That addresses the current problem directly:

- The tests need to assert branches such as `ResolveDocuments`, `ParseMinimumAccessibility`, selector ambiguity, snapshot mismatch, and response shaping failures.
- Those branches are currently hidden behind static helpers and real Roslyn setup.
- A small seam-first refactor will let each tool test express exact `GIVEN_[state]` conditions instead of building oversized fixture worlds.

## Quality Rules For This Work

- Every tool class in `Roslyn.Workbench.Mcp.Plugins.Core` must reach 100% line and branch coverage.
- Every tool keeps its own dedicated `*ToolTests` class.
- Test method names must describe the exact branch-driving state, not a loose scenario label.
- Each tool test suite must cover:
  - Success path
  - Invalid request path(s)
  - Resolution failure path(s)
  - Snapshot mismatch path(s) where applicable
  - Response bounding / truncation / rejection path(s) where applicable
  - Mutation staging rejection path(s) where applicable
- Shared helper tests are allowed, but they do not replace tool-specific branch coverage.
- Integration-style fixture tests should remain only where Roslyn end-to-end behaviour is the thing under test.

## Naming Standard

Use `GIVEN_[state]_WHEN_CallingExecute_THEN_Should...` where `[state]` names the branch condition.

Examples:

- `GIVEN_UnableToResolveDocument_WHEN_CallingExecute_THEN_ShouldReturnRejection`
- `GIVEN_UnableToParseAccessibility_WHEN_CallingExecute_THEN_ShouldReturnInvalidRequest`
- `GIVEN_SnapshotMismatch_WHEN_CallingExecute_THEN_ShouldReturnConflict`
- `GIVEN_ResponseExceedsConfiguredLimit_WHEN_CallingExecute_THEN_ShouldReturnNarrowRequestRejection`

Avoid labels such as:

- `GIVEN_InspectionWorkspace`
- `GIVEN_DefaultCoordinator`
- `GIVEN_TestProviders`

unless that is truly the branch-driving state.

## Phase 0: Testing Seam Refactor

This phase is a prerequisite if it materially reduces per-tool setup.

### Objectives

- Break direct dependence on broad static helper flows where they hide branch entry points.
- Make selector resolution, snapshot validation, bounded-result shaping, and replay/staging calls mockable.
- Reduce the amount of real workspace setup needed to test rejection and validation branches.

### Likely Refactor Targets

- `src/Roslyn.Workbench.Mcp.Plugins.Core/ToolExecutionHelpers.cs`
- `src/Roslyn.Workbench.Mcp.Plugins.Core/QueryToolHandler.cs`
- `src/Roslyn.Workbench.Mcp.Plugins.Core/MutationToolHandler.cs`
- `src/Roslyn.Workbench.Mcp.Plugins.Core/CodeStructureAnalysisHelpers.cs`
- `src/Roslyn.Workbench.Mcp.Plugins.Core/DependencyAnalysisHelpers.cs`
- `test/Roslyn.Workbench.Mcp.TestSupport/BundledCoreToolTestHarness.cs`

### Refactor Direction

- Extract helper responsibilities behind injectable collaborators or thin wrapper interfaces.
- Prefer seams around behaviour groups, not one interface per method.
- Candidate seams:
  - selector/document/project/scope resolution
  - symbol resolution
  - snapshot validation
  - bounded collection shaping
  - replay code action staging
  - mutation staging
- Keep tool logic readable; do not convert tools into constructors with excessive dependency noise.

### Exit Criteria

- A tool test can force helper-driven rejection paths with Moq instead of requiring a full workspace fixture.
- A tool test can verify result-shaping branches without serializing large ad hoc solutions.
- Happy-path integration scenarios still exist for representative tools.

## Phase 1: Shared Test Infrastructure Upgrade

### Objectives

- Replace broad harness-style execution with focused builders/factories for unit scenarios.
- Separate unit helpers from integration fixture helpers.

### Work Items

- Add mock-friendly tool execution context builders in `Roslyn.Workbench.Mcp.TestSupport`.
- Add factory helpers for common rejection results:
  - invalid request
  - selector not found
  - selector ambiguous
  - snapshot mismatch
  - response limit exceeded
- Add reusable assertions for:
  - `ToolOutcome`
  - `ToolError.Code`
  - `RequiredAction`
  - returned count / `HasMore`
  - mutation operation naming

### Exit Criteria

- Tool tests read as branch specifications, not fixture assembly scripts.
- Shared setup code no longer hides the state under test.

## Phase 2: Code Action Tool Rewrite

### Coverage Themes

- missing or invalid action id
- unavailable code action service
- snapshot mismatch
- replay vs parameterised vs unsupported action modes
- mutation staging success and rejection
- transaction-required branches where applicable

### Checklist

#### CodeActions

- [ ] DescribeCodeActionTool
- [ ] ListCodeActionsTool
- [ ] StageCodeActionTool
- [ ] StageCodeFixTool
- [ ] StageFixAllTool

## Phase 3: Inspection Tool Rewrite

### Coverage Themes

- scope/document/project resolution failures
- symbol resolution not found / ambiguous
- invalid request parsing
- snapshot mismatch for location/symbol-based requests
- bounded response trimming and rejection
- Roslyn null-path handling
- filtering branches such as obsolete handling, accessibility thresholds, include flags, and metadata/document selection

### Priority Batches

#### Batch A: Selector and structure heavy

- [ ] GetSolutionStructureTool
- [ ] GetProjectDetailsTool
- [ ] GetDocumentOptionsTool
- [ ] GetDocumentOutlineTool
- [ ] SearchSymbolsTool
- [ ] ResolveSymbolTool
- [ ] GetSymbolInfoTool
- [ ] GoToDefinitionTool

#### Batch B: Relationship and reference queries

- [ ] FindReferencesTool
- [ ] FindCallersTool
- [ ] FindCalleesTool
- [ ] FindImplementationsTool
- [ ] FindDerivedTypesTool
- [ ] FindOverloadsTool
- [ ] FindOverridesTool
- [ ] GetTypeHierarchyTool
- [ ] GetSymbolMembersTool
- [ ] GetSymbolAttributesTool
- [ ] GetSymbolDependenciesTool
- [ ] GetSymbolDependentsTool
- [ ] GetPartialDeclarationsTool
- [ ] GetChangeImpactTool
- [ ] GetTestImpactTool

#### Batch C: Analysis and graphing

- [ ] GetDiagnosticsTool
- [ ] AnalyzeControlFlowTool
- [ ] AnalyzeDataFlowTool
- [ ] GetOperationTreeTool
- [ ] GetControlFlowGraphTool
- [ ] GetCodeContextTool
- [ ] GetCodeMetricsTool
- [ ] GetDependencyGraphTool
- [ ] FindDependencyCyclesTool
- [ ] FindDuplicateCodeTool
- [ ] FindUnusedSymbolsTool
- [ ] AnalyzeNullabilityTool
- [ ] AnalyzeAsyncTool
- [ ] AnalyzeDisposablesTool

#### Batch D: Scope-driven query/mutation hybrids

- [ ] GetApiSurfaceTool
- [ ] FormatDocumentTool
- [ ] SortUsingsTool
- [ ] RenameSymbolTool

## Phase 4: Refactoring Tool Rewrite

### Coverage Themes

- selection required
- snapshot mismatch
- provider/action not available
- parameter parsing / invalid enum / invalid option
- proposal rejection
- mutation staging success
- replay-family branch differences
- multi-path tools with direction/kind/strategy branches

### Priority Batches

#### Batch A: Simple replay-style wrappers

- [ ] AddDebuggerDisplayTool
- [ ] AddNullChecksTool
- [ ] ConvertAnonymousTypeToTupleTool
- [ ] ConvertBetweenRegularAndVerbatimInterpolatedStringTool
- [ ] ConvertBetweenRegularAndVerbatimStringTool
- [ ] ConvertDirectCastToTryCastTool
- [ ] ConvertExpressionBodyTool
- [ ] ConvertForEachToForTool
- [ ] ConvertForToForeachTool
- [ ] ConvertLocalFunctionToMethodTool
- [ ] ConvertPrimaryToRegularConstructorTool
- [ ] ConvertToRecordTool
- [ ] ConvertTryCastToDirectCastTool
- [ ] IntroduceUsingStatementTool
- [ ] InvertConditionalTool
- [ ] InvertIfTool
- [ ] InvertLogicalTool
- [ ] MakeLocalFunctionStaticTool
- [ ] MoveDeclarationNearReferenceTool
- [ ] NameTupleElementTool
- [ ] ReplaceConditionalWithStatementsTool
- [ ] ReplaceDocCommentTextWithTagTool
- [ ] ReverseForStatementTool
- [ ] UseExplicitTypeTool
- [ ] UseImplicitTypeTool
- [ ] UseRecursivePatternsTool

#### Batch B: Replay tools with meaningful option branches

- [ ] AddAwaitTool
- [ ] AddImportTool
- [ ] AddMissingUsingsTool
- [ ] ConvertAnonymousTypeToClassTool
- [ ] ConvertIfToSwitchTool
- [ ] ConvertPropertyTool
- [ ] ConvertToInterpolatedStringTool
- [ ] MoveTypeToFileTool
- [ ] UseNamedArgumentsTool

#### Batch C: Tools that likely justify dedicated seam work

- [ ] ConvertAutoPropertyToFullPropertyTool
- [ ] ConvertForeachLinqTool
- [ ] EncapsulateFieldTool
- [ ] ExtractMethodTool
- [ ] InlineVariableTool
- [ ] IntroduceParameterTool
- [ ] IntroduceVariableTool
- [ ] RemoveUnusedUsingsTool

## Per-Tool Rewrite Template

For each tool, complete the following before calling it done:

- [ ] Read the tool implementation and list every return branch.
- [ ] Identify helper branches delegated through shared helpers.
- [ ] Decide which branches are true unit-test targets and which need Roslyn integration coverage.
- [ ] Rewrite the test names so each `GIVEN_[state]` matches the actual branch condition.
- [ ] Add direct tests for all rejection and validation branches.
- [ ] Add happy-path test(s) for the meaningful output shape, not just non-null assertions.
- [ ] Add response-bounding tests where the tool uses `CreateBoundedCollectionResult` or size guards.
- [ ] Run coverage and confirm 100% line and branch coverage for that tool.

## Expected Test File Pattern

Each tool’s dedicated test class should typically have:

- 1 or more success-path tests
- 1 invalid-request test per validation branch
- 1 test per selector-resolution branch
- 1 snapshot mismatch test where applicable
- 1 response shaping test where applicable
- 1 mutation staging rejection test where applicable

If a tool has helper-driven branch complexity, split setup through local private methods inside the test class or shared test-support builders, but keep assertions tool-specific.

## Deliverables

- Improved dedicated test classes for all `Plugins.Core` tools
- Refactored helper seams where required for direct branch testing
- Reduced dependence on broad fixture-only scenarios
- Verified 100% line and branch coverage per tool
- Retained or trimmed aggregate tests so they no longer carry branch-coverage responsibility

## Suggested Execution Order

1. Phase 0 seam refactor
2. Phase 1 test infrastructure
3. CodeActions
4. Inspection Batch A
5. Inspection Batch B
6. Inspection Batch C
7. Inspection Batch D
8. Refactorings Batch A
9. Refactorings Batch B
10. Refactorings Batch C
11. Final coverage audit and cleanup

## Open Decision To Confirm Before Execution

If the seam refactor starts pulling too much logic out of tool classes, stop and cap the change at thin wrapper abstractions. The goal is better testability for the existing tools, not a broad architectural rewrite.
