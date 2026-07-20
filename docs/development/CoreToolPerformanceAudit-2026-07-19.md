# Core Tool Performance Audit

Date: 2026-07-19

## Purpose

This audit records the static performance analysis of the bundled core query tools before further optimisation. It provides a reproducible inventory of suspected costs, identifies work that is performed after the result is already known to be bounded, and separates high-confidence code improvements from hypotheses that require measurement.

This is not a benchmark report. A code pattern is evidence of avoidable work or allocation, not evidence of a user-visible regression. End-to-end profiling and focused benchmarks remain necessary before making higher-risk algorithmic, caching or concurrency changes.

The repeatable measurement and profiling framework now lives under `tools/Roslyn.Workbench.Mcp.Performance`. It drives the published Host through MCP against exact commits of external small, medium and large GitHub repositories. The framework is retained in source control; clones, restored assets and published binaries use operating-system-local temporary storage, while raw results, validation, traces and heap captures are kept beneath the gitignored `artifacts/performance/results` directory in the repository root.

## Scope

The comprehensive scan covered 50 production files:

- all 38 handlers under `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection`;
- `CompilerDiagnosticHelpers` and `DefaultCompilerDiagnosticService`;
- `DefaultDependencyAnalysisService`;
- `DefaultInspectionContextService`;
- `DefaultProjectStructureService`;
- `DefaultToolRequestResolver`;
- `DocumentOutlineProjectionFactory` and `InspectionProjectionFactory`;
- `QueryToolHandler`, `ToolExecutionHelpers` and `ToolExecutionServices`.

Request contracts were reviewed where their bounds or traversal controls affect execution. The original 50-file pass excluded mutation and refactoring handlers because core query tools were the initial priority.

A focused follow-up reviewed the three bundled refactoring handlers and `MutationToolHandler`. It replaced the full-document string allocations used by `format-document` for no-change detection with `SourceText.ContentEquals`. No other pre-measurement handler optimisation was justified: `rename-symbol` is dominated by Roslyn's solution-wide rename, while `sort-usings` operates on one document's normally small using collection.

The measurement phase includes `format-document`, `rename-symbol` and `sort-usings`, together with the shared mutation staging path. This is measurement scope rather than a claim that query and mutation operations have identical performance characteristics.

Target framework: .NET 10.

## What the analysis looks for

The review uses the following order of importance:

1. **Work performed before a published bound.** Find calls to `ToolExecutionHelpers.CreateBoundedCollection`, `Take`, maximum-depth controls and other truncation points, then trace backwards to determine whether Roslyn analysis, semantic enrichment, projection, sorting or materialisation was performed for results that cannot be returned.
2. **Repeated Roslyn acquisition or traversal.** Find repeated `GetCompilationAsync`, `GetSemanticModelAsync`, `GetSyntaxRootAsync`, `GetTextAsync`, `DescendantNodes`, `DescendantsAndSelf` and `IOperation` walks within one request, especially repeated work for the same document, method or symbol.
3. **Repeated contract projection.** Find sorting keys that call `CreateSymbolReference` or `CreateResolvedLocation`, followed by a second projection of the same item. These operations can calculate display strings, documentation IDs, source text positions and document identities.
4. **Avoidable enumeration and materialisation.** Review LINQ chains, repeated enumeration, intermediate arrays/lists/dictionaries, grouping and sorting. Simple LINQ over small Roslyn-owned collections is not a finding by itself; it becomes actionable when it is on a solution-scale or per-node hot path, duplicates work, or materialises before a bound.
5. **Algorithmic scaling.** Look for nested scans such as rescanning a complete executable body for every local, rebuilding dependencies for every graph node, or normalising every candidate before knowing whether it can contribute to the response.
6. **String and payload allocations.** Look for syntax `ToString`, whitespace normalisation, splitting, trimming, context-line creation and response projection performed for discarded results.
7. **Async and blocking hazards.** Check sync-over-async, repeated `ValueTask` consumption, unnecessary task scheduling, missing cancellation in long loops and unbounded concurrent work.
8. **Runtime construction costs.** Check per-request regular expressions, serializers, HTTP clients, file streams, static lookup tables and unnecessary compilation or MSBuild evaluation.
9. **Structural opportunities.** Verify leaf classes are sealed and check read-only static lookup structures, without changing public or plugin architecture merely for a speculative micro-optimisation.
10. **Correctness constraints.** Preserve deterministic ordering, `HasMore`, snapshot semantics, cancellation, complete cycle/duplicate discovery and the distinction between limiting response projection and limiting semantic analysis.

For every proposed optimisation, validation must answer:

- Does the returned order remain identical?
- Is `HasMore` based on at least one additional valid result rather than merely one additional candidate?
- Can an early stop omit a later item that should sort before an already selected item?
- Is a complete source graph, reference count or duplicate grouping required by the contract?
- Does caching remain local to the immutable request snapshot, avoiding stale cross-request state?
- Is the saved work large enough to justify additional code and tests?

## Scan execution checklist

The counts below are exact textual signals within the 50-file scope. Manual review removed false positives before findings were classified.

| Scan | Hits | Review outcome |
|---|---:|---|
| `async void` | 0 | No issue. |
| Sync-over-async candidates | 1 | False positive from the `DiagnosticSeverity` alias; no blocking async use found. |
| `ValueTask` declarations | 77 | Expected handler/service contracts; no multiple-await use found. |
| `Substring` | 0 | No issue. |
| Literal `IndexOf`, `StartsWith`, `EndsWith` or string `Contains` without comparison | 0 | No issue. |
| Parameterless `ToLower`/`ToUpper` | 0 | No issue. |
| Three-or-more chained `Replace` calls | 0 | No issue. |
| `params` signatures | 0 | No issue. |
| `string.Format` | 0 | No issue. |
| Generated, compiled or per-call regular expressions | 0 | Regex is not used in scope. |
| Static `Dictionary` / `FrozenDictionary` declarations | 0 / 0 | No static lookup-table candidate. |
| Per-request `new List` / `new Dictionary` sites | 27 / 6 | Reviewed in context; only solution-scale or repeatedly populated collections contribute to findings. |
| `StringComparer.CurrentCulture` | 0 | No issue. |
| `Select`/`Where`/`Cast`/`Take`/`Aggregate` calls | 112 | Reviewed manually; ordinary small projections were not treated as findings. |
| All scanned LINQ operators | 344 | Confirms extensive use; actionable sites are listed below. |
| `ContainsKey` / `TryGetValue` | 1 / 5 | The single `ContainsKey` is a membership check in Tarjan traversal, not a double lookup. |
| `GetSyntaxRootAsync` / `GetSemanticModelAsync` | 18 / 23 | Repeated-document acquisition is present in reference and dependency paths. |
| `GetCompilationAsync` | 3 | Two are required for diagnostics; one in `GetProjectDetailsTool` appears unnecessary. |
| `DescendantNodes` or `DescendantsAndSelf` | 15 | Several occur inside nested per-symbol or per-local processing. |
| `CreateSymbolReference` / `CreateResolvedLocation` | 50 / 28 | Repeated sort-key and discarded-result projection is present. |
| `CreateBoundedCollection` / `CreatePreboundedCollection` | 30 / 5 | Late bounding is the dominant systematic smell. |
| JSON serializer/options, `HttpClient`, `FileStream`, `stackalloc`, `ArrayPool` | 0 | Not used in the query execution scope. |
| Concrete sealed / unsealed classes | 48 / 0 | All concrete classes in scope are sealed; no structural action. |

## Findings

No critical correctness or deadlock finding was identified.

### Moderate

#### P1. Result bounds are applied after work at 30 call sites

**Impact:** Many tools analyse, enrich, project, sort and materialise every result before discarding items beyond a small curated response limit.

**Files:** See the complete `CreateBoundedCollection` inventory below.

**Fix:** Move each tool's limit to the earliest correctness-preserving stage, retain one additional valid item for `HasMore`, and publish with `CreatePreboundedCollection`. Push limits into services when the service currently performs the dominant work.

**Caveat:** Cycle detection and duplicate discovery require complete candidate analysis to preserve global semantics; those paths need deferred projection or a service redesign rather than a mechanical early exit.

#### P2. Dependency graphs and test impact repeatedly rebuild semantic dependencies

**Impact:** Type and symbol graphs call dependency collection for every source symbol or type, repeatedly retrieving syntax and semantic models and walking operations; test impact builds a complete dependency set for every candidate test before checking for one target match.

**Files:** `DefaultDependencyAnalysisService.cs` dependency collection, graph construction and test-impact paths.

**Fix:** Introduce request-local document analysis state, reuse semantic models, avoid collecting type dependencies for the same member more than once, and use a target-aware early-exit dependency check for test impact. Pass graph response bounds into the service where bounded nodes can safely restrict returned-edge analysis.

**Caveat:** Full graph construction remains necessary for cycle detection. Benchmark type, symbol and test-impact granularities separately before changing graph algorithms or introducing concurrency.

#### P3. Code metrics calculate expensive metrics for every candidate before bounding

**Impact:** Each candidate is projected and traversed repeatedly for logical lines, cyclomatic complexity, nesting depth and coupling before the ordered result is truncated. Logical-line counting creates a full syntax string, splits it, trims every line and enumerates it again.

**Files:** `GetCodeMetricsTool.cs:8`, `GetCodeMetricsTool.cs:38`, `GetCodeMetricsTool.cs:48`, `GetCodeMetricsTool.cs:108`, `GetCodeMetricsTool.cs:121`, `GetCodeMetricsTool.cs:174`, `GetCodeMetricsTool.cs:226`.

**Fix:** Collect and deduplicate candidate symbols first, establish deterministic ordering and the result bound, then compute metrics only for returned symbols. Consolidate syntax traversals and count logical lines from source text/span data without `ToString().Split().Trim()` intermediates.

**Caveat:** The tool should be benchmarked independently because metric computation is deliberately CPU-intensive and nested declaration spans may overlap.

#### P4. Source-scanning analysers continue after sufficient ordered results exist

**Impact:** `analyze-async` and `analyze-disposables` scan every selected document and project every finding before bounding. The disposable analysis additionally rescans the containing executable body for every disposable local, while both analysers repeatedly resolve framework types inside inner loops.

**Files:** `AnalyzeAsyncTool.cs:15`, `AnalyzeAsyncTool.cs:25`, `AnalyzeAsyncTool.cs:35`, `AnalyzeAsyncTool.cs:53`, `AnalyzeAsyncTool.cs:105`, `AnalyzeDisposablesTool.cs:15`, `AnalyzeDisposablesTool.cs:25`, `AnalyzeDisposablesTool.cs:38`, `AnalyzeDisposablesTool.cs:71`, `AnalyzeDisposablesTool.cs:79`.

**Fix:** Scan documents in final output order, stop after one additional valid finding, resolve task/disposable framework symbols once per compilation, and analyse disposal invocations once per executable body rather than once per local.

**Caveat:** Prove that source traversal order exactly matches the existing path/span/kind ordering before removing the final sort.

#### P5. Symbol relationship tools repeatedly project every symbol before bounding

**Impact:** Ten ordering pipelines call `CreateSymbolReference` to calculate a sort key and then call it again for response projection; `SearchSymbolsTool` and `GetSymbolMembersTool` can call it three times per symbol. This work includes display formatting, documentation IDs and source-location projection.

**Files:** `FindCalleesTool.cs:101`, `FindDerivedTypesTool.cs:35`, `FindImplementationsTool.cs:26`, `FindOverridesTool.cs:31`, `GetApiSurfaceTool.cs:67`, `GetSymbolDependenciesTool.cs:41`, `GetSymbolDependentsTool.cs:46`, `GetSymbolMembersTool.cs:40`, `GetTypeHierarchyTool.cs:38`, `SearchSymbolsTool.cs:30`.

**Fix:** Use stable Roslyn display/path keys or a single pending projection, select the bounded ordered symbols plus one, and create response `SymbolReference` objects only for returned items.

**Caveat:** Ordering is contractual. Any cheaper key must be proven equivalent to the existing `SymbolReference.DisplayName` and location path ordering.

#### P6. Reference and caller tools repeat per-location document work

**Impact:** Caller context, containing-symbol lookup, syntax-root lookup and source-text lookup can repeat for many locations in the same document. `find-callers` performs all enrichment before its caller bound; the already-tuned reference paths still create resolved locations for every candidate before selection.

**Files:** `FindCallersTool.cs:24`, `FindCallersTool.cs:29`, `FindCallersTool.cs:38`, `FindReferencesTool.cs:30`, `FindReferencesTool.cs:52`, `FindReferencesTool.cs:78`, `FindReferencesTool.cs:126`, `GetChangeImpactTool.cs:34`, `GetChangeImpactTool.cs:43`, `GetChangeImpactTool.cs:83`, `DefaultInspectionContextService.cs:5`.

**Fix:** Select callers before context enrichment, group selected locations by document, reuse request-local syntax/text/semantic state, and investigate sorting raw locations before constructing `ResolvedLocation` objects.

**Caveat:** `get-change-impact` must still calculate complete reference, caller, override and implementation counts even when supporting locations are bounded.

#### P7. Control-flow graph projection serialises all blocks and regions before request limits

**Impact:** Every operation syntax string, block DTO and region DTO is produced before `MaxBlocks` and `MaxRegions` are applied.

**Files:** `GetControlFlowGraphTool.cs:75`, `GetControlFlowGraphTool.cs:83`, `GetControlFlowGraphTool.cs:84`, `GetControlFlowGraphTool.cs:85`, `GetControlFlowGraphTool.cs:97`.

**Fix:** Iterate blocks and regions only until the requested maximum plus one, project only returned entries, and derive truncation from the additional item.

#### P8. Project structure tools perform avoidable compilation and late per-project work

**Impact:** `get-project-details` requests a full compilation only to read compilation options already available from the project. `get-solution-structure` evaluates target frameworks and optionally projects documents for every project before applying the projects limit; target-framework evaluation loads project files through a new MSBuild `ProjectCollection` each time.

**Files:** `GetProjectDetailsTool.cs:15`, `GetProjectDetailsTool.cs:21`, `GetProjectDetailsTool.cs:22`, `GetSolutionStructureTool.cs:14`, `GetSolutionStructureTool.cs:18`, `GetSolutionStructureTool.cs:31`, `DefaultProjectStructureService.cs:10`, `DefaultProjectStructureService.cs:25`.

**Fix:** Remove the unnecessary compilation, bound ordered projects before target-framework/document projection, and measure target-framework evaluation before considering request-local or file-version-aware caching.

**Caveat:** Do not introduce cross-request caching that can outlive the immutable workspace snapshot or conceal external project-file changes.

### Informational or benchmark-gated

#### P9. Duplicate detection eagerly creates normalised strings and response context

**Impact:** Every executable block normalises and joins all statements, resolves a symbol/location and creates context before duplicate groups and group bounds are known.

**Files:** `FindDuplicateCodeTool.cs:39`, `FindDuplicateCodeTool.cs:55`, `FindDuplicateCodeTool.cs:71`, `FindDuplicateCodeTool.cs:90`, `FindDuplicateCodeTool.cs:117`, `FindDuplicateCodeTool.cs:134`.

**Fix:** Benchmark large documents, then retain lightweight syntax candidates through grouping and defer symbol, location and context projection until duplicate groups selected for return are known.

**Caveat:** All candidate fingerprints must still be computed to establish whether a duplicate exists.

#### P10. Search filters linearly rescan request lists per symbol

**Impact:** Kind and accessibility filters perform comparer-aware list scans for every matched symbol.

**Files:** `SearchSymbolsTool.cs:43`, `SearchSymbolsTool.cs:52`, `SearchSymbolsTool.cs:57`.

**Fix:** Prepare ordinal-ignore-case sets once when filters are non-empty, as `GetDiagnosticsTool` now does for IDs and severities.

**Caveat:** Defaults are small; retain only if profiling or large filter lists justify the added allocations.

#### P11. Local tree projections contain smaller allocation candidates

**Impact:** Operation trees materialise every child-operation sequence before deciding whether depth truncates it; code-context windows use `Enumerable.Range` and per-line strings; outline recursion creates a LINQ pipeline and array at every declaration level.

**Files:** `GetOperationTreeTool.cs:38`, `GetOperationTreeTool.cs:42`, `GetCodeContextTool.cs:49`, `DocumentOutlineProjectionFactory.cs:5`, `DocumentOutlineProjectionFactory.cs:29`.

**Fix:** Measure representative deep trees and large context windows before replacing these clear local projections with manual enumerators or builders.

**Status:** Partially addressed in Batch 5. Operation-tree truncation no longer materialises children that cannot be returned, and recursive outline projection now uses one explicit collection pass. Code-context window construction remains unchanged pending representative measurement.

## Baseline `CreateBoundedCollection` inventory

Every use found during the baseline is treated as a smell because the helper can only truncate after its input collection exists. The table records whether moving the limit can avoid meaningful upstream work.

| Tool and collection | Work completed before the bound | Disposition |
|---|---|---|
| `analyze-async` findings | Full document/operation scan and finding projection | High-value migration in source-scanner batch. |
| `analyze-disposables` findings | Full document scan, repeated executable scan and finding projection | High-value migration in source-scanner batch. |
| `find-callers` callers | All location/context enrichment and caller projection | High-value migration in relationship batch. |
| `find-callees` callees | Direct/indirect traversal and duplicate symbol projection for sorting | Bound projection; retain complete depth-limited traversal unless measurement supports a top-N design. |
| `find-dependency-cycles` cycles | Complete source graph and strongly connected component analysis | Complete analysis required; push output selection into service only if it reduces cycle DTO work. |
| `find-derived-types` derived types | Full Roslyn search, depth calculation and projection | Bound after search but before DTO projection. |
| `find-duplicate-code` groups | Complete fingerprint/group discovery and response enrichment | Complete discovery required; defer response enrichment. |
| `find-implementations` implementations | Full Roslyn search, sorting and projection | Bound after search but before DTO projection. |
| `find-overloads` overloads | All signatures and parameter/type DTOs | Move bound ahead of signature projection; expected collection is usually small. |
| `find-overrides` overrides | Full Roslyn search, sorting and projection | Bound after search but before DTO projection. |
| `get-api-surface` symbols | Full source scan, symbol projection and attribute checks | High-value migration: collect/sort symbols, then project bounded results. |
| `get-code-metrics` metrics | Full source scan and all metric calculations | Highest-value migration: bound symbols before metric calculation. |
| `get-dependency-graph` nodes | Complete graph construction | Push limits into graph service where contract semantics allow. |
| `get-dependency-graph` edges | Complete graph construction plus filtering/materialisation | Push node/edge limits into graph service; preserve edges only between returned nodes. |
| `get-partial-declarations` declarations | All source-location projections | Move bound before projection for consistency; normally a small collection. |
| `get-project-details` documents | All document sorting and projection | Move bound before document projection. |
| `get-project-details` project references | All reference resolution and projection | Move bound before projection; normally small. |
| `get-project-details` metadata references | All metadata DTO projection | Move bound before projection; potentially material for large SDK projects. |
| `get-project-details` analysers | All analyser DTO projection | Move bound before projection; normally small. |
| `get-solution-structure` folders | Complete hierarchy parse | Complete parse currently required for project-folder mapping; bound returned folder DTOs. |
| `get-solution-structure` projects | Target-framework evaluation and optional document projection for every project | High-value migration: bound ordered projects first. |
| `get-symbol-attributes` attributes | All inherited attribute and argument projection | Move bound before attribute argument projection; normally small. |
| `get-symbol-dependencies` dependencies | Complete operation walk, sorting and projection | Complete dependency discovery required; bound DTO projection. |
| `get-symbol-dependents` dependents | Complete reference search and per-reference enclosing-symbol lookup | High-value migration; determine unique ordered symbols before DTO projection and reuse document state. |
| `get-symbol-members` members | Complete hierarchy/interface collection and repeated symbol projection | Bound after unique ordering and project once. |
| `get-test-impact` tests | Full scan and dependency collection for every test candidate | Push limit and target-aware early exit into dependency service. |
| `get-type-hierarchy` derived types | Full Roslyn search, depth calculation and projection | Bound after search and depth filter but before DTO projection. |
| `get-type-hierarchy` base types | Walk up to `MaxDepth`, regardless of smaller collection limit | Stop after the collection limit plus one. |
| `get-type-hierarchy` interfaces | Sort and project every interface | Bound ordered interfaces before projection. |
| `search-symbols` symbols | Full project searches, filter scans, repeated sort-key projection | Bound unique ordered symbols before DTO projection; evaluate whether Roslyn search itself can accept safe limits. |

The five existing pre-bounded paths are `find-references`, `get-change-impact`, `get-diagnostics`, `analyze-nullability` and `find-unused-symbols`. The first two are partial improvements because they still construct `ResolvedLocation` values for all source references to preserve current ordering; the other three now avoid response projection beyond the published limit.

## Implementation progress

### 2026-07-19 — Source analysers

- `analyze-async` now scans in deterministic response order, stops when the next valid finding establishes `HasMore`, and resolves task-like framework symbols once per analysed document rather than once per invocation.
- `analyze-disposables` now applies the same ordered early bound, resolves disposable framework symbols once per document, and calculates disposed local symbols once per executable body rather than rescanning the body for every local.
- Both tools now publish through `CreatePreboundedCollection` without projecting the additional finding used to establish truncation.
- After this step, the inspection-handler count was 28 `CreateBoundedCollection` calls and seven `CreatePreboundedCollection` calls.
- A later cross-tool optimisation should introduce request-local reuse of compilation-derived framework symbols. The current inventory contains six `GetTypeByMetadataName` lookups: `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`, `IDisposable` and `IAsyncDisposable`. Cache results once per project/compilation within a request, include future well-known type lookups in the same audit, and do not retain symbols in process-wide static state because that would retain or mix workspace snapshots and project reference contexts.

### 2026-07-19 — Direct eager projections

- `get-api-surface` now orders lightweight Roslyn symbols, selects the bounded set, and projects only returned API DTOs. Accessibility-chain and obsolete-attribute checks no longer allocate per-symbol LINQ intermediates.
- `get-code-metrics` now retains lightweight candidates through deduplication, ordering and bounding. Syntax retrieval, location and symbol projection, and metric traversal run only for returned candidates; logical-line and multi-traversal algorithm changes remain deferred for separate measurement.
- `get-control-flow-graph` now projects only requested blocks and stops region traversal when the next region establishes truncation.
- `get-project-details` now bounds lazy document, project-reference, metadata-reference and analyser projections and reads `Project.CompilationOptions` directly instead of creating a compilation.
- Batch 2 is complete. The current textual inspection-handler count is 22 `CreateBoundedCollection` calls and eleven `CreatePreboundedCollection` calls; project details uses dedicated readable loops for its four independently bounded response collections.

### 2026-07-19 — Request-owned effective limits

- All 35 nullable collection limits now expose an internal `Effective...Limit` property on their owning request.
- Tool handlers consume those normalized values without repeating the request property, curated default and normalization helper at every call site.
- All 44 request-owned default constants are private. Public nullable request properties, initial values and `[DefaultValue]` metadata remain unchanged.

### 2026-07-19 — Symbol relationships and structure projection

- All Batch 3 collections now establish `HasMore` from one additional ordered item and publish through `CreatePreboundedCollection`.
- Resolver-created `SymbolReference` values are retained as the pending projection for relationship and search ordering. This guarantees that resolver-defined display names and projected paths remain the ordering contract while eliminating repeated projection of the same symbol.
- Callers are selected before location and context enrichment. Symbol dependents now reuse semantic models per document within the request.
- Overload signatures, attribute arguments, dependency DTOs and type-hierarchy nodes are created only for returned items. Base-type traversal stops once its depth or response bound is established.
- Solution projects are selected before target-framework evaluation, project-reference projection and optional document projection. Folder DTOs returned by the hierarchy service are bounded without further enrichment.
- After this step, the inspection-handler count was five `CreateBoundedCollection` calls and 34 `CreatePreboundedCollection` calls. The remaining late-bound sites belonged to the dependency, cycle, test-impact and duplicate-code work deferred to Batch 4.

### 2026-07-19 — Dependency and duplicate algorithms

- Dependency graph bounds now enter the analysis service. Project, namespace, type and symbol graphs select the response nodes before collecting dependencies, analyse edges only between selected nodes, and stop after the first additional ordered edge establishes truncation.
- Dependency analysis reuses semantic models only for the lifetime of the request. It does not retain Roslyn state across requests or workspace snapshots.
- Cycle detection still constructs the complete graph and runs complete strongly connected component discovery. Only the ordered response projection is bounded because stopping graph discovery early would change cycle semantics.
- Test impact discovers and orders candidate test methods first, then checks signature and operation dependencies directly. Dependency traversal exits on the first target match and the service stops after the first additional impacted test establishes `HasMore`.
- Duplicate detection still fingerprints every executable candidate and validates its source location, but retains syntax and symbols as lightweight candidates. Symbol references and context strings are created only for occurrences in selected groups.
- An isolated BenchmarkDotNet short run compared the existing duplicate fingerprint `string.Join`/`Select` expression with a manual `StringBuilder` loop at 3, 10 and 30 statements. The loop was within 2% of the existing implementation but allocated approximately 3% more in every case, so the simpler existing expression was retained.
- Deterministic service tests cover all four graph granularities, bounded project edges, full cycle discovery with a zero response limit, target-impact truncation and bounded duplicate projection. The inspection-handler inventory is now zero `CreateBoundedCollection` calls and 39 `CreatePreboundedCollection` calls.

### 2026-07-20 — Request-local compilation lookups and tree projections

- Workspace now owns an internal request-local compilation type-symbol cache. It resolves metadata names lazily and keys results by compilation identity, including missing results, without retaining compilations across tool executions.
- The async and disposable analysers reuse the cache for all six framework-type lookups across documents in the same compilation while isolating distinct projects and target frameworks.
- Operation-tree projection checks the depth boundary before enumerating the full child sequence, so truncated children are not materialised.
- Document-outline recursion uses an explicit single-pass loop instead of creating a LINQ projection/filter pipeline at every declaration level.
- Code-context window construction remains unchanged. Its bounded, readable projection is not being replaced without representative measurements showing a useful gain.

### 2026-07-20 — Bundled refactoring follow-up

- `format-document` compares the original and formatted `SourceText` instances directly instead of allocating two complete document strings for no-change detection.
- `rename-symbol` has no actionable local pre-measurement issue; Roslyn's solution-wide rename is the dominant operation.
- `sort-usings` retains its readable document-local ordering pipeline pending evidence that using-directive projection is material.
- All three refactoring tools and the shared mutation staging path are included in the measurement phase.

## Recommended implementation batches

### Batch 1 — Completed bounded diagnostic/reference work

- Added depth control to indirect callee traversal.
- Deferred containing-symbol, context and write-reference enrichment in `find-references` until after selection.
- Deferred containing-symbol and context enrichment in `get-change-impact` until after selection while retaining complete summary counts.
- Moved diagnostic/nullability projection behind limits.
- Made unused-symbol analysis ordered, bounded and document-state-aware.
- Centralised non-negative result-limit resolution.

### Batch 2 — Source scanners and direct eager projections — completed 2026-07-19

Implement the high-confidence changes in:

- `analyze-async` — completed 2026-07-19;
- `analyze-disposables` — completed 2026-07-19;
- `get-api-surface` — completed 2026-07-19;
- `get-code-metrics` — completed 2026-07-19;
- `get-control-flow-graph` — completed 2026-07-19; and
- `get-project-details` — completed 2026-07-19.

These share a clear dependency: establish ordered candidates and `max + 1` validity before expensive DTO or metric projection. Keep the code-metrics traversal consolidation separate within the batch if it materially enlarges review risk.

### Batch 3 — Symbol relationship and search projection — completed 2026-07-19

Migrated:

- callers, callees, derived types, implementations and overrides;
- symbol dependencies, dependents and members;
- type hierarchy;
- symbol search, overloads, attributes and partial declarations; and
- project/solution structure collections not handled in Batch 2.

Use a consistent pending-symbol shape only if it removes repeated projection without obscuring per-tool ordering. Do not introduce a general top-N abstraction until at least two tools demonstrate the same correctness requirements.

### Batch 4 — Benchmark-gated dependency and duplicate algorithms — completed 2026-07-19

Measure and then address:

- project, namespace, type and symbol dependency graphs;
- cycle detection;
- test impact; and
- duplicate-code detection.

Complete discovery remains where global semantics require it: cycle detection constructs the full graph and duplicate detection computes all fingerprints. Response bounds now prevent dependency analysis or response projection that cannot affect the returned result.

### Batch 5 — Request-local Roslyn lookup reuse and small projections — completed 2026-07-20

Workspace owns the request-local cache for compilation-derived framework symbols. The six metadata-name lookups used by the async and disposable analysers are cached by compilation identity, with focused tests covering successful lookup reuse, missing lookup reuse and distinct-compilation isolation. No cross-request Roslyn state is retained.

Operation-tree child enumeration and document-outline recursion now avoid their unnecessary intermediate projections. Code-context window construction and other small collection projections remain unchanged pending measurement.

## Full concrete-tool compliance re-audit — 2026-07-20

This follow-up audited every concrete published tool rather than selecting files from analyser findings. The scope contains 100 tool handlers:

- 38 bundled inspection tools;
- 3 bundled mutation/refactoring tools;
- 43 internal Code Action refactoring tools;
- 5 internal Code Action catalogue/workflow tools; and
- 11 server-owned lifecycle and transaction tools.

The review applied the repository rules for simple LINQ, explicit hot-path or multi-stage logic, bounded collection construction and readable result assembly. A LINQ call is not a finding by itself. Ordering pipelines retained deliberately by the earlier optimisation batches remain appropriate when they are short, named and preserve a contractual sort key more clearly than a custom comparer or loop.

### Re-audit scan checklist

| Scan | Hits | Review outcome |
|---|---:|---|
| Concrete sealed / unsealed tool classes | 100 / 0 | All concrete tools are sealed. |
| Files containing LINQ / total LINQ operator calls | 36 / 194 | Nine files contain multi-stage or heavily repeated pipelines that should be simplified. The remaining uses are short predicates, projections, contractual ordering or previously measured code. |
| `Select` / `Where` / `Cast` / `Take` / `Aggregate` calls | 57 | Manually reviewed with their surrounding stages and bounds. |
| `CreateBoundedCollection` / `CreatePreboundedCollection` | 0 / 39 | The previous early-bounding work remains intact. |
| Per-call `new List` / `new Dictionary` | 53 / 4 | These are mutable accumulators, deduplication state or bounded result builders; no static-data collection is rebuilt per request. |
| Nested `Success(new Response...)` result construction | 11 | Violates the repository result-assembly convention and should use a named response/candidate local. |
| Nested `ValueTask.FromResult(Rejected(...))` construction | 6 | Five direct returns should use a named rejection local; the one concise dispatch switch arm is readable and can remain. |
| Sync-over-async, `async void`, `ConfigureAwait(false)` execution | 0 | The only text match is the title of the Code Action that adds `ConfigureAwait(false)`; no tool executes that pattern. |
| Literal string comparisons without explicit comparison, `Substring`, parameterless case conversion, three-call `Replace` chains | 0 | No issue. |
| Per-call regex, serializer options or `HttpClient` construction | 0 | No issue. |

### Actionable compliance findings

#### C1. Nine tools compressed multi-stage logic into LINQ pipelines

**Status:** Completed 2026-07-20.

**Impact:** Filtering, classification, projection, ordering, null removal and materialisation are combined or repeated in ways that make the execution order and allocation points harder to verify.

**Files:** `ListCodeActionsTool.cs:90`, `AnalyzeControlFlowTool.cs:22`, `AnalyzeDataFlowTool.cs:22`, `FindCallersTool.cs:24`, `GetCodeContextTool.cs:20`, `GetSolutionStructureTool.cs:57`, `GetSymbolInfoTool.cs:20`, `GoToDefinitionTool.cs:16`, `ResolveSymbolTool.cs:40`.

**Fix:** Introduce named candidate/result collections and explicit loops for classification, filtering and projection. Retain a short ordering stage where it remains the clearest way to express the contractual ordering. Preserve existing deduplication, null filtering and ordering exactly.

**Caveat:** This is primarily a maintainability correction. It may remove delegate and iterator allocations, but no material latency improvement is claimed without measurement.

#### C2. Eleven success results constructed their payload inside the factory call

**Status:** Completed 2026-07-20.

**Impact:** The principal response or mutation candidate is obscured inside the return expression, contrary to the repository-wide result-assembly convention.

**Files:** `ListCodeActionsTool.cs:102`, `AnalyzeControlFlowTool.cs:22`, `AnalyzeDataFlowTool.cs:22`, `AnalyzeNullabilityTool.cs:68`, `FindReferencesTool.cs:110`, `FindUnusedSymbolsTool.cs:83`, `GetChangeImpactTool.cs:109`, `GetCodeContextTool.cs:46`, `GetDiagnosticsTool.cs:51`, `RenameSymbolTool.cs:32`, `SortUsingsTool.cs:40`.

**Fix:** Assign the response or mutation candidate to a named local, then pass that local to `Success`.

#### C3. Five direct Code Action rejection returns retained a nested wrapper/factory expression

**Status:** Completed 2026-07-20.

**Impact:** The expected rejection is constructed inside `ValueTask.FromResult`, making already long validation returns unnecessarily dense.

**Files:** `AddMissingUsingsTool.cs:20`, `ConvertForeachLinqTool.cs:21`, `ExtractMethodTool.cs:24`, `IntroduceParameterTool.cs:23`, `IntroduceVariableTool.cs:18`.

**Fix:** Assign the rejection to a named local before returning the completed `ValueTask`. Retain the equivalent expression in `ConvertPropertyTool` because it is one concise arm of a readable dispatch switch rather than a multi-stage return.

### Deliberately retained LINQ

- Relationship tools retain short projection-and-ordering stages where the projected `SymbolReference` defines the response ordering contract and is then consumed by an explicit bounded loop.
- Diagnostic and nullability tools retain named filter-and-order stages followed by explicit bounded projection loops.
- `sort-usings` retains its small document-local ordering pipeline; the earlier measurement plan already treats Roslyn transformation and staging as the material costs.
- Duplicate fingerprint construction retains its `string.Join`/`Select` implementation because the measured explicit-loop alternative allocated more and did not improve throughput.
- Small Roslyn-owned collections such as method parameters, attribute arguments and direct child locations retain simple projections where a loop would add code without clarifying control flow.

### Recommended next batch

C1–C3 were completed as one focused readability batch before the exception-flow B2 work. Filtering, classification, nullable projection and repeated collection traversal now use named stages and explicit loops. Short LINQ pipelines remain only where they express contractual ordering or a deliberately retained local projection more clearly.

The post-remediation scan records 155 LINQ operator calls across 34 tools, down from 194 calls across 36 tools. Nested `Success(new Response...)` and direct `return ValueTask.FromResult(Rejected(...))` sites are both zero. `CreateBoundedCollection` remains at zero and all 39 pre-bounded result publications remain intact.

| Severity | Finding count | Top issue |
|---|---:|---|
| Critical | 0 | None identified. |
| Remaining | 0 | No actionable concrete-tool compliance finding remains from this re-audit. |
| Completed compliance | 3 | Multi-stage LINQ and nested result assembly were simplified without changing bounds or ordering. |

> **Disclaimer:** This static audit can contain false positives or miss runtime costs. Preserve behaviour with tests and use the existing profiling framework before claiming a performance improvement.

## Measurement plan

Before Batch 4, and before claiming any material performance improvement from earlier batches, capture:

- cold and warm execution;
- end-to-end MCP latency and handler-only elapsed time;
- allocated bytes and Gen 0/1/2 collections;
- peak working set;
- counts of projects, documents, syntax nodes, candidates and returned items;
- bounded runs at zero, one, the curated default and above-result-count limits;
- cancellation latency during large source scans; and
- output equality, deterministic ordering and `HasMore` equivalence.

Representative scenarios should include small, medium and realistically large checked-in workspaces. Use profiling or traces for complete tool calls, and BenchmarkDotNet only for isolated repeatable helpers such as metric traversal, diagnostic filtering or duplicate fingerprint creation.

The tool-call scenarios must include the bundled refactoring tools: whole-document and range formatting, small and large symbol renames, and using sorting with both `SystemFirst` modes. Measure both no-change and staged-change outcomes so Roslyn transformation cost can be distinguished from mutation staging and diff construction.

## Positive findings

- All 48 concrete classes in the original query scope and all three refactoring handlers are sealed.
- No sync-over-async, `async void`, repeated `ValueTask` consumption, per-call regex, serializer-options, HTTP-client or stream-construction issue was found.
- String comparisons in the scanned scope already specify ordinal semantics where applicable.
- Query loops generally propagate cancellation.
- Curated per-tool defaults and depth limits now exist, and five high-volume result paths already publish pre-bounded collections.

## Summary

| Severity | Finding count | Top issue |
|---|---:|---|
| Critical | 0 | None identified. |
| Moderate | 8 | Thirty collection bounds are applied only after their input collections have been built. |
| Informational / benchmark-gated | 3 | Duplicate fingerprinting and local tree projections require measurement. |

> **Disclaimer:** These results are generated by an AI assistant and are non-deterministic. Findings may include false positives, miss real issues, or suggest changes that are incorrect for this specific context. Verify recommendations with benchmarks and human review before applying them to production code.
