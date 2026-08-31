# DOGFOOD-012 — Cross-project test-impact matching

**Status:** Confirmed through published dogfood validation.

## Finding

`get-test-impact` returns no tests when the selected production symbol and candidate tests belong to different projects connected by a project reference. The current dogfood reproduction resolves `CommitRecoveryStore` from `Roslyn.Workbench.Mcp.Workspace`, scans `Roslyn.Workbench.Mcp.Workspace.Test`, and returns an empty bounded collection even though `CommitRecoveryStoreTests` stores the production type in its `_target` field and its test methods invoke that field.

The actor is an authorised agent making a read-only query over a normal solution. The concrete action is to select a source symbol in a production project and ask for likely tests in a referencing test project. This is a routine and plausible use of `get-test-impact`. Snapshot and scope resolution succeed, but there is no existing control that repairs symbol identity across compilations. The user-visible impact is an inaccurate empty result that can cause an agent to omit directly affected tests. This is a product defect within the operating model and should be remediated.

## Validated cause

`DependencyAnalysisService.FindTestImpactsAsync` resolves the target once from the production project's compilation. It then obtains each test method and its operations from the test project's compilation. `HasTargetDependencyAsync` ultimately compares those symbols with `SymbolEqualityComparer.Default` after normalising named types and members to their original definitions.

Original-definition normalisation does not move a symbol into another compilation. The field-reference operation in a test method exposes the referenced production type as represented by the test compilation, while the selected target remains the symbol owned by the production compilation. Their semantic identities describe the same declaration but their Roslyn symbol instances are not directly comparable across that boundary, so the existing checks return false.

This is the complete cause demonstrated by the current path:

- the same-compilation unit fixtures pass because the target and test methods come from one `AdhocWorkspace` project;
- the current published solution call still returns zero tests across the real project reference;
- the algorithm already inspects operation types, including the type of an accessed `_target` field, so the missing class-held target is not caused by absent field analysis; and
- `get-change-impact` and Roslyn reference discovery already find the cross-project dependency, showing that the solution graph and source reference are present.

## Existing coverage

`DependencyAnalysisServiceTests` contains two test-impact scenarios. One verifies limiting and `hasMore`; the other verifies target types nested inside arrays and generic types. Both create one project containing the target and tests in the same compilation. Neither exercises a project reference or a second compilation.

`GetTestImpactToolTests` verifies symbol resolution, scope rejection, effective limits, service invocation and bounded response projection. It deliberately mocks `IDependencyAnalysisService`, so it cannot expose service-level symbol identity failures.

A focused coverage run confirms that `GetTestImpactTool` itself currently has 100% line coverage and 100% branch coverage. The service path behind the tool is materially weaker: `FindTestImpactsAsync` currently has 92.72% line coverage and 76.92% branch coverage, while `HasTargetDependencyAsync` has 66.66% line coverage and 60% branch coverage. The design must therefore preserve the handler's complete structural coverage and bring the complete test-impact service path, including the new rebinding logic, to 100% line and branch coverage.

No Plugins Core integration test, Host acceptance test, Scenario Runner definition or checked-in scenario invokes `get-test-impact`. The surviving gap is therefore both a service defect and missing end-to-end coverage through a realistic project-reference boundary.

## Supported impact meaning

The correction retains the current deliberately direct meaning of a likely impacted test:

- a candidate method is considered impacted when its return type, a parameter type, or an operation in that method directly references the selected symbol or the selected symbol's owning type;
- accessing a class field such as `_target` counts because that method's field-reference operation has the field's type;
- merely declaring a field on the test class does not mark every test method in the class; only methods whose own signature or operation tree exposes the target are returned; and
- the service does not infer transitive impact through arbitrary helper calls, constructors or fixture setup. Expanding into call-graph propagation is a separate product decision and is outside DOGFOOD-012.

The existing reason text, `Direct reference to the target symbol or its owning type.`, remains accurate and no MCP request or response contract changes are required.

## Proposed production design

Keep target matching semantic and compilation-local by rebinding the selected target into each candidate test project's compilation before analysing that project's methods.

1. Continue normalising the selected target symbol and its owning type once at the start of `FindTestImpactsAsync`.
2. For each distinct candidate project, use Roslyn's supported [`SymbolFinder.FindSimilarSymbols<TSymbol>`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.findsymbols.symbolfinder.findsimilarsymbols) API to locate the corresponding target symbol and owning type in that project's compilation. The API is specifically defined to return equivalent symbols when the supplied compilation is not the symbol's originating compilation.
3. Accept a rebound symbol only when Roslyn returns exactly one match. If it returns no match or an ambiguous set, retain the original target symbol for that project. Direct equality can still succeed for the originating compilation, while a different compilation will safely produce no impact rather than selecting an uncertain declaration.
4. Cache the resolved target pair by `ProjectId` during one analysis call so rebinding happens at most once per candidate project, not once per document, method or operation.
5. Associate each discovered test method with the resolved target pair for its project. Preserve the existing deterministic method ordering, limit-plus-one behaviour, signature checks, operation traversal, composite-type recursion and result projection.
6. Keep the existing `SymbolsMatch` helper unchanged. Broadening it globally would also affect dependency graph self-edge removal and could collapse symbols that those workflows intentionally keep distinct.

Represent the resolved target pair with a small private class containing explicit `Symbol` and nullable `OwningType` properties and an explicit constructor. Do not use a positional record or a tuple threaded through the analysis. Keep the unique-match helper short and explicit: inspect at most two results from `FindSimilarSymbols`, return the single match only when the count is exactly one, and otherwise return the original symbol.

This design delegates cross-compilation identity to Roslyn rather than comparing display names, metadata names or hand-built documentation IDs. It therefore follows SDK assembly and signature semantics, accommodates future Roslyn identity changes, and avoids name-based false positives. The ambiguity rule deliberately prefers a false negative over claiming impact for an uncertain symbol.

## Test design

### Service unit coverage

Extend `DependencyAnalysisServiceTests` using the existing `RoslynTestFactory.CreateSolution` project-reference support.

Add a multi-project test with:

- a production project declaring `Sample.Target`;
- a test project referencing the production project;
- a test class holding a `Target` field;
- one test-like method that accesses that field; and
- one test-like method that does not access it.

Resolve the selected target from the production project, analyse only the test-project document, and require exactly the field-using method with `hasMore: false`. This locks the live failure path and the supported class-field semantics.

Add a false-positive test with two independent production projects that declare the same namespace and type name, where the test project references only the non-selected project. Select the symbol from the other project and require no impacted tests. This proves that the correction follows Roslyn symbol identity rather than matching names.

Retain the existing limit and composite-type tests unchanged. Together, the unique-match and no-match cases cover the new rebinding decision while existing tests continue to cover same-compilation behaviour.

Add the remaining focused service cases needed to execute every line and branch used exclusively by test-impact analysis. These cases must cover return-type and parameter-type matches, operation-type and referenced-symbol matches, methods with and without source locations, reason inclusion and omission, invalid or unavailable syntax/semantic paths, same-compilation fallback, unique cross-compilation rebinding, no-match and ambiguous-match fallback, cancellation checkpoints, result limiting and the final unbounded return. Structure the implementation so every defensive decision can be exercised through its public service entry point without invalid Roslyn mocks.

Measure coverage against the production types after the implementation. `FindTestImpactsAsync`, `HasTargetDependencyAsync` and every new helper or state type introduced solely for test-impact matching must each report 100% line coverage and 100% branch coverage. Shared dependency-analysis helpers are outside this item only where they are already exercised by other dependency-analysis features and are not changed for DOGFOOD-012.

### Tool unit coverage

Retain `GetTestImpactToolTests` as the contract-level unit boundary and require `GetTestImpactTool` to remain at 100% line coverage and 100% branch coverage. Add a tool unit case only if the implementation changes the handler or the measured coverage falls below that threshold; the cross-project defect itself must not be simulated in this mocked suite because it belongs to the real service and integration coverage below.

### Tool integration coverage

Add a dedicated `CrossProjectTestImpact` Workspace asset used only by `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest`. It will contain a small production project, a referencing test project, one field-using test method and one unrelated test method. Do not modify the shared `SolutionHierarchy` asset because it is also consumed by acceptance coverage.

Add `GetTestImpactIntegrationTests` that opens the real solution through `BundledComponentWorkspaceFactory`, invokes `get-test-impact` through `PluginComponentTestSession`, selects the production symbol by documentation-comment ID, scopes candidate tests to the test project, and requires the field-using method, its resolved source location, the direct-reference reason and no unrelated method. This exercises Workspace loading, project-reference compilations, request resolution, the real dependency service, tool execution and bounded response projection.

No acceptance source, acceptance fixture or Scenario Runner definition needs to change. The published dogfood recheck remains the executable-boundary validation, so the acceptance suite is not required for this implementation unless its files are changed separately.

## Validation outcome

The implementation passed the affected unit and integration suites, the solution build and the affected `latest-all` analyser builds. Focused coverage reported 100% line and branch coverage for `GetTestImpactTool`, the complete supported `FindTestImpactsAsync` and `HasTargetDependencyAsync` paths, and the new compilation-target helpers. Two explicit guards remain approved defensive Roslyn exceptions: a method declaration obtained from a document's syntax root is expected to resolve in that document's semantic model, and a declaring syntax reference obtained from a solution symbol is expected to map back to a document in the same solution. Genuine Roslyn objects cannot produce either null result through the supported flow; exercising those branches would require an invalid Roslyn substitute, reflection or a test-only production seam. The guards remain to avoid a null-reference failure if Roslyn cannot provide the expected semantic state. A fresh reviewed candidate was then validated through Codex's configured dogfood namespace: the real `CommitRecoveryStore` query against the distinct `Roslyn.Workbench.Mcp.Workspace.Test` project returned 10 bounded results with `hasMore: true` and the expected direct-reference reason, replacing the zero-result failure reproduced on the previous published build.

## Rejected alternatives

### Compare display names, metadata names or documentation IDs directly

These representations can collide across projects or assemblies and would turn an empty-result defect into false-positive test selection. They also duplicate identity rules already owned by Roslyn.

### Change `SymbolsMatch` globally

That helper also participates in dependency graph construction and self-edge removal. A service-wide fallback comparison would expand the change beyond test impact and risk collapsing intentionally distinct multi-project or multi-target symbols.

### Use `SymbolFinder.FindReferencesAsync` and derive test methods from reference locations

Reference discovery is valuable but does not preserve the current signature, composite-type and field-access semantics without a second propagation system. A field declaration can be outside every test method, while only some methods access that field. Rebinding the target keeps the existing tested algorithm and fixes the actual identity boundary.

### Mark every method in a class that declares a target-typed field

This would over-report tests that never access the field and silently change the product from direct dependency analysis to class-wide setup inference.

## Validation plan

After approved implementation:

1. Format only the changed C# files and normalise all changed CRLF-governed files.
2. Run the `Roslyn.Workbench.Mcp.Plugins.Test` fast unit/contract loop.
3. Run the complete `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` project because a new integration asset and integration test are added.
4. Build the affected production and test projects normally.
5. Run `latest-all` analyser builds for every affected C# project and review diagnostics in each changed file.
6. Run focused unit coverage and require 100% line and branch coverage for `GetTestImpactTool`, `FindTestImpactsAsync`, `HasTargetDependencyAsync` and every new helper or state type used solely by test-impact matching. Report the measured percentages explicitly.
7. After the two-confirmation and independent-review process, publish the approved candidate and repeat `get-test-impact` for `CommitRecoveryStore` against `Roslyn.Workbench.Mcp.Workspace.Test`. Require at least the test methods that directly access `_target`, intelligible reasons, stable bounded response metadata and no unrelated methods.

Implementation must not begin until this design receives explicit manual approval.
