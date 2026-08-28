# DOGFOOD-011 — Generic-base hierarchy resolution

## Purpose

DOGFOOD-011 corrects derived-type queries for generic type definitions. The published `find-derived-types` and `get-type-hierarchy` tools currently return a successful empty derived-type collection when the selected root is a generic class or interface, even when Roslyn has discovered concrete derived types or implementations.

This is a query-correctness defect in the shared Workspace hierarchy service. It does not require an MCP contract, selector, response-shape or public plugin API change.

## Issue validation

The DOGFOOD-010 sweep selected `McpServerToolBase<TRequest>` from the main solution. `find-derived-types` and `get-type-hierarchy` both returned no derived types, while `find-overrides` found five concrete overrides on derived MCP tools. Both hierarchy queries returned the expected results for the non-generic `ResolvedFlowRegion` control.

DOGFOOD-011 design discovery reproduced the same behaviour for the generic interface `IQueryToolHandler<TRequest, TResponse>`. `find-derived-types` returned no derived types, while `find-implementations` returned 75 implementations. The failure therefore affects both the class and interface discovery branches and is not caused by the selected project scope or an absence of matching symbols.

Existing `TypeHierarchyServiceTests` cover a non-generic class hierarchy and a non-generic interface hierarchy. Existing `FindDerivedTypesToolTests` and `GetTypeHierarchyToolTests` cover result ordering, depth filtering, limits and projection, but none of these fixtures uses a generic root.

## Operating-model assessment

- **Actor:** an authorised agent issuing a read-only hierarchy query against a trusted loaded Workspace.
- **Action:** select a declared generic class or interface and request its derived types or complete type hierarchy.
- **Plausibility:** generic base classes and interfaces are ordinary C# designs, and the defect occurs against the Host's own tool and plugin abstractions.
- **Existing control:** Roslyn's `SymbolFinder` discovers the correct declarations, but the Workspace service silently discards them during local depth calculation. No diagnostic tells the caller that the successful empty result is incomplete.
- **Impact:** the agent receives an incorrect semantic result and may conclude that a generic extension point has no implementations or derived types.
- **Decision:** remediate the shared hierarchy service. No product-operating-model change is required.

## Root cause

`TypeHierarchyService.FindDerivedTypesAsync` asks Roslyn's `SymbolFinder` for transitive derived classes, derived interfaces or implementations. It then calculates the shortest hierarchy depth by walking each discovered type's direct parents.

For a concrete declaration such as `ServerStatusTool : McpServerToolBase<ServerStatusRequest>`, the direct parent is the constructed type `McpServerToolBase<ServerStatusRequest>`. The selected root is the declared generic definition `McpServerToolBase<TRequest>`. `SymbolEqualityComparer.Default` correctly treats those as different symbols, so `GetDistance` never reaches the root and returns `int.MaxValue`; the caller then discards the otherwise valid discovered type.

The same mismatch occurs for a concrete implementation of `IQueryToolHandler<Request, Response>` when the selected root is `IQueryToolHandler<TRequest, TResponse>`.

Roslyn's [`INamedTypeSymbol.OriginalDefinition`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.inamedtypesymbol.originaldefinition) explicitly returns the original source or metadata definition for a type produced by type substitution. [`SymbolFinder.FindDerivedClassesAsync`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.findsymbols.symbolfinder.findderivedclassesasync) already returns the declared derived types; the missing step is definition-level identity during the local parent walk.

## Proposed production design

Keep Roslyn discovery, project scoping, exact discovered-symbol deduplication and breadth-first depth calculation unchanged.

Change only the parent-to-root identity check in `TypeHierarchyService.GetDistance`:

1. Resolve the selected root's `OriginalDefinition` once for the distance calculation.
2. For each direct parent reached by the breadth-first walk, compare `parent.OriginalDefinition` with the root definition using `SymbolEqualityComparer.Default`.
3. Continue storing the actual constructed parent symbols in the visited set. The visited set prevents repeated traversal of real graph nodes and must not collapse different constructed paths merely because they share a definition.
4. Continue returning the discovered declaration symbol supplied by `SymbolFinder`; do not replace response symbols with their base definitions or constructed parents.

This gives hierarchy queries their existing declaration-level meaning: selecting the generic declaration `Base<T>` returns declarations derived through `Base<string>`, `Base<TItem>` or another construction, with depth measured between declarations. Non-generic symbols are unaffected because their `OriginalDefinition` is the symbol itself.

No change is proposed to `ITypeHierarchyService`, `TypeHierarchyMatch`, either MCP request or response contract, tool limits, ordering, selector resolution or snapshot handling.

## Test design

Add focused tests to `TypeHierarchyServiceTests`, where the failing Roslyn symbol relationship is owned:

- A generic class hierarchy containing direct construction, generic pass-through and an indirect derived class. Assert every declaration and its shortest depth.
- A generic interface hierarchy containing a derived generic interface, a direct concrete implementation and an implementation through the derived interface. Assert every declaration and its shortest depth.

Retain the existing non-generic class and interface tests as regression coverage. The new tests execute the real `SymbolFinder` and breadth-first traversal with in-memory Roslyn solutions; no mocks or test-only production seams are required.

Do not add projection-only tests to `FindDerivedTypesToolTests` or `GetTypeHierarchyToolTests`. Those tools do not perform hierarchy identity comparison, and their existing tests already cover service-result ordering, depth filtering, bounded collections and response projection. Repeating the same generic Roslyn fixture in both tool classes would duplicate the service test and rely further on the existing test helper's real production-service wiring. The published dogfood recheck will exercise both tool entry points after implementation.

No acceptance-test or Scenario Runner asset changes are proposed. The defect is fully reproducible at the service boundary, and the published Host has already supplied the end-to-end failing evidence.

## Validation design

After implementation:

1. Format the changed production and Workspace test files only.
2. Run the affected Workspace non-acceptance test project.
3. Build the affected production and test projects normally.
4. Run the SDK `latest-all` analyser build for the affected production and test projects and review diagnostics in both changed C# files.
5. Verify CRLF line endings and `git diff --check`.
6. After the implementation and review process is complete, publish the approved build and repeat `find-derived-types` and `get-type-hierarchy` for `McpServerToolBase<TRequest>` plus the generic-interface control through Codex's configured dogfood tools.

The acceptance suite is not required because no acceptance artifact or protocol boundary changes. If later implementation changes a contract or acceptance asset, return to design discovery and apply the repository's complete acceptance-wrapper requirement.

## Alternatives rejected

- **Compare display names, metadata names or documentation IDs:** these are projections rather than Roslyn symbol identity and can introduce ambiguity across namespaces, assemblies, nested types or overload-like generic shapes.
- **Compare constructed parents directly with the generic root:** this is the current behaviour and cannot match a type-substituted parent with its declaration.
- **Replace every traversed symbol with `OriginalDefinition`:** unnecessary. Definition normalization belongs only at the identity boundary; retaining constructed nodes in the visited set preserves the actual graph traversal.
- **Repeatedly call `SymbolFinder` for each depth:** this would replace a small in-memory breadth-first walk with multiple solution searches, increasing cost without improving correctness.
- **Fix each MCP tool independently:** both tools consume the same incorrect service result. Duplicating normalization in the consumers would leave the shared service defective for plugins and future callers.

## Approval and review gates

This document is the design proposal only. Production and test code must not change until the user manually approves it.

After implementation and required non-acceptance validation, present the unstaged change for the user's first code review. Stage only after confirmation, then use a fresh context-free Review Agent with the supplied validation evidence. Keep any material review remediation unstaged for comparison, repeat the independent review when required, and obtain final confirmation before the user commits.
