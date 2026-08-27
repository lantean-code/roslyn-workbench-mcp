# DOGFOOD-007 — Symbol-search scope enforcement

## Purpose

DOGFOOD-007 makes `search-symbols` honour an exact document scope before deterministic ordering and response bounding. It also protects the already-correct solution, project and multi-project behaviour with focused non-acceptance coverage.

## Reproduced behaviour

The published DOGFOOD-006 Host was queried for fields matching `_target` with a limit of 10:

| Scope | Published result |
| --- | --- |
| `Solution` | 10 of 91 matching fields across the solution. |
| `Project` (`Roslyn.Workbench.Mcp.Workspace.Test`) | 10 of 43 matching fields, all from the selected project. |
| `Projects` (`Workspace.Test` and `Plugins.Core.Test`) | 10 of 43 matching fields, all from the only selected project containing matches. |
| `Document` (`WorkspaceSelectorFactoryTests.cs`) | 10 of 43 matching fields from other documents in the containing project; the selected document was not enforced. |

The project and multi-project paths therefore already constrain eligible symbols before ordering and bounding. The defect is specifically the document path.

## Discovery

`SearchSymbolsTool` begins by calling `IToolRequestResolver.ResolveProjects`. For solution scope it searches `context.CurrentSolution`; for every other scope it searches each resolved project with Roslyn's `SymbolFinder.FindSourceDeclarationsWithPatternAsync(Project, ...)` overload. It then applies the metadata-name, kind, accessibility and namespace filters, de-duplicates symbols, projects deterministic references and takes the requested prefix.

`ToolRequestResolver.ResolveProjects` intentionally maps a document scope to the document's containing project. That behaviour is appropriate for project-oriented consumers, but it loses the exact document identity needed by symbol search. Consequently, `SearchSymbolsTool` searches the complete containing project and has no later document filter.

`IToolRequestResolver` already exposes `ResolveDocument`, so no resolver or public plugin API change is needed. Roslyn provides solution and project declaration-search overloads, but no document overload. The document path must therefore search the containing project and retain only symbols with a source declaration in the resolved document. This filter must run before projection, ordering and the response bound.

## Operating-model assessment

- **Actor:** an authorised agent performing an ordinary read-only symbol query.
- **Action:** send `search-symbols` with `scope.kind = Document` and a valid document selector.
- **Plausibility:** document-scoped search is a published contract and was used during normal repository inspection.
- **Existing control:** the resolver validates that the selected document exists, but reducing it to its project does not preserve the requested search boundary. Result bounding limits response size but can exclude every relevant result after unrelated project declarations are ordered first.
- **Impact:** the agent receives symbols outside the requested document, misleading `totalCount` and `hasMore` values, and may receive no symbol from the selected document even when one exists.
- **Decision:** remediate the document path in `SearchSymbolsTool`; retain and test the existing solution, project and multi-project behaviour.

## Production design

Keep the existing Roslyn solution search for solution scope and project search for project and multi-project scopes.

For document scope:

1. Resolve the exact document through `IToolRequestResolver.ResolveDocument` and return its normal rejection unchanged when resolution fails.
2. Search the resolved document's containing project with the existing Roslyn project overload.
3. Retain a symbol only when at least one source declaration location belongs to the resolved document's syntax tree.
4. Apply the existing metadata-name, kind, accessibility and namespace filters to those eligible symbols.
5. De-duplicate, project, order and bound the surviving symbols through the existing pipeline.

A symbol declared in more than one document, such as a partial type, is eligible when any declaration belongs to the selected document. `CreateSymbolReference` continues to choose the symbol's canonical first source location; document scope governs whether the symbol is eligible, not which declaration is used for its reusable canonical reference. The response count describes eligible symbols after all scope and semantic filters, before the caller's result limit.

Keep the branching readable. Use small named helpers for project search and document-membership testing rather than a combined LINQ pipeline. Do not enumerate syntax roots or semantic models: declaration locations and the resolved document syntax tree are sufficient. Do not change `ToolRequestResolver.ResolveProjects`, because other callers legitimately use a document selector to identify its containing project.

## Alternatives rejected

- **Resolve every scope to documents and search each document independently:** Roslyn has no document declaration-search overload, and enumerating every solution or project document would add work without improving the already-correct paths.
- **Search the whole solution and filter projected paths:** this performs unnecessary work for project scopes, filters too late, and can mishandle symbols with multiple declarations.
- **Change `ResolveProjects` so document scope returns no project:** this would break the resolver's documented project-oriented meaning and its other consumers.
- **Add a new public resolver result carrying both projects and documents:** the existing `ResolveDocument` and `ResolveProjects` operations are sufficient; a new abstraction would exceed this defect's scope.

## Test design

Add focused unit coverage in `SearchSymbolsToolTests` using a solution with at least two projects and multiple documents containing deliberately overlapping symbol names:

- solution scope returns matches from every project;
- project scope returns matches only from the selected project;
- projects scope returns the union of the selected projects without duplicates;
- document scope returns only symbols declared in the selected document;
- a low document-scoped limit proves unrelated declarations cannot consume the bound and verifies `Items`, `HasMore` and `TotalCount` after scope filtering;
- a partial type declared in the selected document remains eligible; and
- a document-resolution rejection is returned unchanged.

Add one Plugins.Core integration test using the materialised multi-project `SolutionHierarchy` Workspace. Invoke the real bundled catalogue for solution, project, projects and document scopes and assert the returned paths and counts are restricted before the bound. This covers selector resolution, the real Workspace, Roslyn search and the tool handler together without changing a published-Host acceptance asset.

No acceptance-test source or fixture changes are proposed. Published dogfood validation supplies executable-boundary evidence after the reviewed change is committed.

## Documentation changes

Update `RoslynMcpToolContracts.md` only if its current `search-symbols` scope wording does not already state the exact behaviour. Record discovery and validation calls in the dogfood usage ledger and update the worklist status at the applicable process gates.

## Validation plan

After implementation:

1. Format only the changed C# production and test files.
2. Build the affected Plugins.Core production, unit-test and integration-test projects with the WSL artefacts path.
3. Run `latest-all` analyser builds with code-style enforcement for every affected C# project.
4. Run the Plugins.Core unit and integration test projects through the repository's preferred non-acceptance commands.
5. Verify all changed CRLF-governed files use CRLF and the unstaged diff passes `git diff --check` before confirmation.

After the independently reviewed change is committed, publish that exact `HEAD` to a new dogfood candidate. Restart Codex and repeat the `_target` search against `WorkspaceSelectorFactoryTests.cs` with a low bound. Require every returned symbol and `totalCount` to belong to that document, then make representative project, projects and solution calls to guard the retained paths.

## Approval and scope gates

The user approved this design before production and test implementation began.

Implementation scope is limited to:

- exact document-scope enforcement inside `SearchSymbolsTool`;
- focused Plugins.Core unit and integration coverage for every scope kind and pre-bound filtering;
- any necessary correction to the existing tool-contract wording;
- DOGFOOD-007 design, usage and final worklist documentation; and
- published dogfood validation after the independently reviewed change is committed.

Do not change scope-selector contracts, general resolver semantics, search matching heuristics, symbol-reference canonicalisation, default limits, MCP SDK version or acceptance-test assets as part of DOGFOOD-007.
