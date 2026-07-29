# Code Action Architecture Implementation Audit — 29 July 2026

## Outcome

The aggregate implementation from the five Code Action architecture commits made on 28 July 2026, together with the uncommitted Batch 5 work reviewed on 29 July 2026, conforms to the repository's active architecture, coding, contract, testing, analyser, formatting and documentation standards after the corrections recorded below. The initial seven-finding review was not sufficiently broad: it found substantive defects but did not repeat the repository readability audit over the complete changed-file set. This revised audit records that second pass and its additional corrections. No open audit finding remains.

This audit covers 189 distinct implementation files: 15 documentation files, 101 production-source files and 73 test files. This report is audit evidence and is not included in that implementation-file count.

## Scope

The aggregate comparison starts at `6e48bbf5718f4b1de1caabe69e2ac9ca7a2efda2`, the parent of the first 28 July commit, and includes the following commits plus the current worktree:

| Commit | Local time | Subject |
| --- | --- | --- |
| `7a6ecd00bbff23cd5803cb2cb2dba45b334eeee3` | 28 July 2026 16:34 BST | Batch 1 of Code Actions refactor |
| `9acaec1cba4bf225f7d7d85e4036f685d4de1b3f` | 28 July 2026 18:19 BST | Batch 2 of Code Actions refactor |
| `543b2efbffe1579267fcf977d9d6c03bf81a2acc` | 28 July 2026 19:50 BST | Batch 3 of Code Actions refactor |
| `6e2e957a9973e6507731c86c8c6700a5520ef7f1` | 28 July 2026 20:50 BST | Batch 4 of Code Actions refactor |
| `36bef6aba8106f6ca6bc8296e05ea405a5e63760` | 28 July 2026 21:45 BST | Batch 4A of Code Actions refactor |
| Current index and worktree | 29 July 2026 | Batch 5 implementation and audit corrections |

The inventory was produced from the union of the baseline-to-worktree diff and untracked files, then de-duplicated. Renames were reviewed as one logical change while both deletion and destination semantics were checked.

## Governing Standards

| Authority | Standards applied |
| --- | --- |
| `AGENTS.md` | Project ownership, dependency direction, validation commands, analyser policy, minimal changes, public documentation, Markdown formatting and CRLF requirements |
| `src/AGENTS.md` | C# structure, naming, nullability, constructor and guard conventions, asynchronous code, member-body style, public XML documentation and source line endings |
| `test/AGENTS.md` | xUnit, Moq and AwesomeAssertions conventions; GIVEN/WHEN/THEN naming; no structural Arrange/Act/Assert comments; isolation and classification; no delay-based synchronisation; coverage and line endings |
| `docs/development/CodeActionArchitecturePlan-2026-07-27.md` | Sole active Code Action architecture, three-tool target surface, policy, discovery, replay, reference lifecycle, Fix All preparation and staging, validation and batch boundaries |
| `docs/development/ArchitectureAuditChecklist.md` | Assembly ownership, Host/CodeActions/Workspace/Plugins separation and superseded-architecture cleanup |
| `docs/development/TestingStrategy.md` | Project dependency map, unit/component/audit ownership, real-boundary integration coverage and acceptance policy |
| `docs/development/Analyzer Inventory.md` | `latest-all` baseline, intentional exclusions and prohibition on reintroducing `ConfigureAwait(false)` |
| `docs/development/ReadabilityAudit-2026-07-23.md` | Explicit control flow, statement separation and repository readability conventions |
| `docs/development/BoundedCollectionTotalCountAudit-2026-07-23.md` | Bounded top-level collection shape and truthful `HasMore`/`TotalCount` semantics |
| `docs/development/PreReleaseReadinessAudit-2026-07-24.md` | Public-surface, schema, test and release-readiness consistency |

## Audit Method

Every file in the appendix was reviewed in its aggregate baseline-to-current form, not only in the commit where it first appeared. The inventory is the de-duplicated union of the baseline-to-worktree diff, deleted paths and implementation files newly created in the worktree. The review applied architecture and contract checks, then repeated the Roslyn syntax categories from the repository readability audit over every changed C# file. Candidate syntax was manually classified; it was not treated as an automatic rewrite instruction.

| Check | Result | Evidence |
| --- | --- | --- |
| Assembly ownership and dependency direction | Pass | CodeActions remains independent of Plugins and the MCP SDK; Workspace remains neutral; shared bounded-result contracts reside in Abstractions |
| Published Code Action surface | Pass | Registration, Host composition, schema tests and catalogue tests agree on `list-code-actions`, `prepare-fix-all` and `stage-code-action` for the implemented batch state |
| Contract consistency | Pass after corrections CA-AUD-001, CA-AUD-002, CA-AUD-003, CA-AUD-006, CA-AUD-008, CA-AUD-009 and CA-AUD-011 | Shared bounded results, nested collection bounds, effective limits, required state and schema assertions now follow existing project patterns |
| Fix All scope construction | Pass | `IFixAllActionFactory` has document-, project- and solution-specific methods; each method accepts only the context required by that scope |
| Replay and reference lifecycle | Pass after correction CA-AUD-004 | Creation, registration and cache publication are linearized with lifecycle invalidation; revision reachability, expiry and consumption remain covered |
| Candidate-solution safety | Pass after correction CA-AUD-005 | Fix All preparation validates the source candidate, merges linked documents, validates the merged candidate and measures the solution that will be staged |
| Code style and readability | Pass after corrections CA-AUD-007, CA-AUD-010, CA-AUD-012 and CA-AUD-013 | The complete changed-file syntax pass now has zero multiline-statement separation and complex-conditional findings; named construction stages, nullable/required semantics, member structure and repository async conventions were checked across all changed C# files |
| Public API and XML documentation | Pass | Normal and `latest-all` builds report no public-documentation or API-shape diagnostics; public API contract tests include the shared abstraction |
| Unit-test structure and ownership | Pass | Tests remain in owner projects, use repository naming/assertion conventions and do not use timing-based synchronisation |
| Integration and audit classification | Pass | Real composition and workspace boundaries remain in integration projects; Roslyn compatibility remains in the audit project |
| Active documentation consistency | Pass | Architecture, testing, tool inventory, readiness and plugin-authoring documents reflect the implemented ownership and contracts |
| Formatting and line endings | Pass | Scoped format verification, `git diff --check` and CRLF inspection passed |

## Findings and Corrections

| ID | Finding | Correction | Disposition |
| --- | --- | --- | --- |
| CA-AUD-001 | `BoundedCollection` was owned by Plugins even though Workspace-facing results, CodeActions and plugin contracts need the same neutral wire contract. | Moved the type to Abstractions under `Workspace.Results`; updated analyser metadata, public API expectations, authoring guidance and owner tests. | Closed |
| CA-AUD-002 | `PrepareFixAllData` duplicated `AffectedDocuments`, count and truncation fields, while `PrepareFixAllTool` manually assembled them and request effective limits did not match existing nullable-limit conventions. | Replaced the fields with `BoundedCollection<DocumentReference>`, used `CreatePrebounded`, and aligned both effective properties and negative-value validation with existing request patterns. | Closed |
| CA-AUD-003 | `CodeActionListData` retained a second bespoke bounded-list shape despite the architecture plan requiring the shared convention. | Changed `Actions` to `BoundedCollection<CodeActionListItem>` and updated tools, consumers, schemas, tests and readiness documentation. | Closed |
| CA-AUD-004 | Reference creation could interleave with lifecycle invalidation between registration and cache publication, leaving a newly published reference for an invalidated snapshot. | Moved identity creation, options, registration and cache insertion into the lifecycle critical section and retained focused concurrency coverage. | Closed |
| CA-AUD-005 | Fix All impact measurement occurred before the complete candidate-validation and linked-document merge path used by staging. | Applied source-candidate validation, linked-document merging and post-merge validation before counting and publishing the prepared result. | Closed |
| CA-AUD-006 | The optional affected-diagnostic count could serialize as `null`, and the new Prepare Fix All output contract lacked sufficiently focused schema evidence. | Suppressed null serialization and added explicit response-schema and contract tests for the concise output. | Closed |
| CA-AUD-007 | Three touched implementation files contained statement-separation inconsistencies relative to the readability standard. | Normalised separation in Code Action discovery, Workspace session storage and transaction handling. | Closed |
| CA-AUD-008 | `CodeActionListItem.Diagnostics` was an unbounded collection inside the already bounded action collection, allowing response size to multiply with diagnostic fan-out. | Changed the property to `BoundedCollection<CodeActionDiagnosticContext>`, imposed a fixed maximum of 10 contexts per action, retained the known total count, added truncation and schema tests, and documented why the closed three-value Fix All scope set does not need the same treatment. | Closed |
| CA-AUD-009 | Code Action request effective limits repeated `Math.Max` expressions instead of following the `ToolExecutionHelpers.GetMaxResults` convention used by comparable requests. | Added the assembly-local `ToolExecutionHelpers` implementation and routed list, prepare and legacy Fix All effective limits through it; added requested/default-value tests. | Closed |
| CA-AUD-010 | `CodeActionInfoFactory` combined nested object construction with conditional expressions whose branches performed projection work. | Split path normalisation, kind selection, location, bounded diagnostics and Fix All scopes into named stages before constructing the final response item. | Closed |
| CA-AUD-011 | Several newly introduced data/state records represented mandatory values with default values, including empty-string replay identities and default value-type identifiers. | Marked mandatory action, diagnostic identity, replay recipe and transaction revision members `required`; kept only collections whose empty value is semantically meaningful; updated construction sites and tests. | Closed |
| CA-AUD-012 | `WorkspaceSessionSnapshot.CurrentSnapshotIdentity` constructed a new derived value on every access instead of storing the identity belonging to the immutable session snapshot. The broader fixture review also found a revision-1 test transaction with no revision data. | Made the identity a required init-only property, centralised derivation in `WorkspaceSnapshotIdentity.Create`, and set it on load, transaction start, staging, history movement, rollback and commit transitions. Test fixtures that alter transaction state now update the identity at the same construction boundary, and the invalid transaction fixture now contains its claimed revision and uses a consistent lifecycle state. | Closed |
| CA-AUD-013 | The first audit did not repeat the established readability scan over all production and test files changed since the pre-Batch-1 baseline. The full pass found 18 production expression/construction candidates, 80 exact test statement-separation violations and 14 test conditional expressions performing work. | Corrected every production candidate and every exact separation/conditional violation. Reviewed the remaining 33 coalescing and 50 nested-return test candidates individually: invariant `?? throw` checks and 42 narrow test-data/Roslyn fixture factories were retained; composite result wrappers, nested dependencies and fallback construction were split into named stages. Eight production LINQ pipelines were retained as short single-purpose ordering or materialisation operations. | Closed |

## Validation Evidence

Validation was run after all behaviour-affecting corrections. Subsequent changes only updated this audit record with the final evidence:

| Validation | Result |
| --- | --- |
| Pinned SDK availability | Pass |
| Normal solution build | Pass, 0 warnings and 0 errors |
| Solution `latest-all` analyser build with code style enforced and no incremental compilation | Pass, 0 warnings and 0 errors |
| CodeActions unit and contract tests | Pass, 639 |
| Host unit and contract tests | Pass, 289 |
| Workspace unit and contract tests | Pass, 873 |
| Plugins unit and contract tests | Pass, 99 |
| Plugins.Core unit and contract tests | Pass, 282 |
| Plugin analyser tests | Pass, 45 |
| CodeActions integration tests | Pass, 17 |
| Workspace integration tests | Pass, 73 |
| Host integration tests | Pass, 54 |
| Plugins.Core integration tests | Pass, 7 |
| Code Action compatibility audit tests | Pass, 173 |
| Total non-acceptance tests | Pass, 2,551 |
| Scoped `dotnet format` verification | Pass, no changes required |
| `git diff --check` | Pass |
| CRLF verification | Pass for every CRLF-governed implementation file |
| Prohibited-pattern scans | Pass: no `ConfigureAwait(false)`, `Task.Delay`, `async void`, TODO/FIXME marker, broad pragma suppression or production null-forgiving operator in scope |
| Dependency-boundary scan | Pass: no CodeActions-to-Plugins reference |

Published-host acceptance tests were not run because neither Batch 5 nor these audit corrections change acceptance infrastructure and repository policy requires explicit user instruction before running that suite. This is an intentional validation boundary, not an unresolved audit finding. Batches 6 and 7 remain future work in the architecture plan and their unchecked requirements were not assessed as if already implemented.

## File-by-File Disposition

`Pass` means the aggregate file conforms without a file-specific correction. `Corrected` identifies the closed finding that changed the file. `Removed` means deletion is the conforming architectural disposition. Renamed destinations are reviewed under their destination paths.

### Documentation — 15 files

| File | Disposition |
| --- | --- |
| `docs/PluginAuthoring.md` | Corrected — CA-AUD-001 |
| `docs/PluginAuthoringDiagnostics.md` | Corrected — CA-AUD-001 |
| `docs/development/AcceptanceCoverageAudit-2026-07-23.md` | Pass — acceptance ownership and future migration references |
| `docs/development/ArchitectureAuditChecklist.md` | Pass — active architecture cross-reference and ownership |
| `docs/development/ArgumentNullExceptionAudit.md` | Pass — guard and nullable conventions |
| `docs/development/CodeActionArchitecturePlan-2026-07-27.md` | Corrected — CA-AUD-002, CA-AUD-003 and CA-AUD-005 |
| `docs/development/IntegrationTestingBaseline-2026-07-17.md` | Pass — historical test evidence remains internally consistent |
| `docs/development/IntegrationTestingImplementationPlan.md` | Pass — integration ownership references |
| `docs/development/IntegrationTestingStage4Results-2026-07-17.md` | Pass — historical result references |
| `docs/development/IntegrationTestingStrategyProposal.md` | Pass — project boundary terminology |
| `docs/development/PluginAuthoringAnalyserAudit-2026-07-24.md` | Corrected — CA-AUD-001 |
| `docs/development/PreReleaseReadinessAudit-2026-07-24.md` | Corrected — CA-AUD-003 |
| `docs/development/TestArchitectureReaudit-2026-07-18.md` | Pass — project counts and ownership references |
| `docs/development/TestingStrategy.md` | Pass — CodeActions, Workspace and Plugins dependency map |
| `docs/development/Tool Test Inventory.md` | Pass — tool ownership and coverage mapping |

### Production Source — 101 files

| File | Disposition |
| --- | --- |
| `src/Roslyn.Workbench.Mcp.Abstractions/Workspace/Results/BoundedCollection.cs` | Corrected — CA-AUD-001; renamed from Plugins and reviewed as a public Abstractions contract |
| `src/Roslyn.Workbench.Mcp.Abstractions/Workspace/Selectors/TextSpanRange.cs` | Pass — selector contract and nullability |
| `src/Roslyn.Workbench.Mcp.CodeActions/Composition/CodeActionAssemblyResolver.cs` | Pass — assembly resolution and isolation |
| `src/Roslyn.Workbench.Mcp.CodeActions/Composition/CodeActionCompositionState.cs` | Pass — state ownership and naming |
| `src/Roslyn.Workbench.Mcp.CodeActions/Composition/CodeActionCompositionStatus.cs` | Pass — concise status contract |
| `src/Roslyn.Workbench.Mcp.CodeActions/Composition/CodeActionProviderCatalogComposition.cs` | Removed — obsolete catalogue composition contract |
| `src/Roslyn.Workbench.Mcp.CodeActions/Composition/CodeActionProviderCatalogStatus.cs` | Removed — obsolete catalogue status contract |
| `src/Roslyn.Workbench.Mcp.CodeActions/Composition/CodeActionProviderIdentity.cs` | Pass — provider identity |
| `src/Roslyn.Workbench.Mcp.CodeActions/Composition/CodeActionProviderSelection.cs` | Pass — policy-based provider selection |
| `src/Roslyn.Workbench.Mcp.CodeActions/Composition/ICodeActionComposition.cs` | Pass — renamed composition boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Composition/ICodeActionProviderSelection.cs` | Pass — focused selection abstraction |
| `src/Roslyn.Workbench.Mcp.CodeActions/Composition/MefCodeActionComposition.cs` | Pass — renamed MEF composition implementation |
| `src/Roslyn.Workbench.Mcp.CodeActions/Configuration/CodeActionExecutionOptions.cs` | Corrected — CA-AUD-008; fixed nested diagnostic bound |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/CodeActionDiagnosticContext.cs` | Corrected — CA-AUD-011; mandatory diagnostic values |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/CodeActionFixAllScope.cs` | Pass — published scope values |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/CodeActionKind.cs` | Pass — action-kind contract |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/CodeActionKindSelection.cs` | Pass — request selection semantics |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/CodeActionListData.cs` | Corrected — CA-AUD-003 |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/CodeActionListItem.cs` | Corrected — CA-AUD-008 and CA-AUD-011; bounded diagnostics and mandatory action values |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/CodeActionLocation.cs` | Pass — location contract |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/ListCodeActionsRequest.cs` | Corrected — CA-AUD-009; effective-limit convention |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/PrepareFixAllData.cs` | Corrected — CA-AUD-002 and CA-AUD-006 |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/PrepareFixAllRequest.cs` | Corrected — CA-AUD-002 and CA-AUD-009 |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/StageCodeActionRequest.cs` | Pass — unified staging request |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/StageCodeFixRequest.cs` | Removed — superseded dedicated request |
| `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/StageFixAllRequest.cs` | Corrected — CA-AUD-009; retained pre-migration request follows the common effective-limit pattern |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionAnalyzerActivator.cs` | Pass — isolated built-in analyser compatibility adapter |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionAnalyzerIndexWarning.cs` | Pass — typed warning contract |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionBuiltInAnalyzerIndex.cs` | Pass — pinned built-in analyser index |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionDiagnosticCollection.cs` | Pass — diagnostic-source grouping |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionDiagnosticService.cs` | Pass — diagnostic collection responsibility |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionDiscoveryService.cs` | Corrected — CA-AUD-007 |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionInfoFactory.cs` | Corrected — CA-AUD-008 and CA-AUD-010; bounded and staged projection |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/DiscoveredCodeAction.cs` | Pass — internal discovery model |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/ICodeActionAnalyzerActivator.cs` | Pass — compatibility abstraction |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/ICodeActionBuiltInAnalyzerIndex.cs` | Pass — index abstraction |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/ICodeActionDiagnosticService.cs` | Pass — diagnostic service boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/ICodeActionDiscoveryService.cs` | Pass — discovery boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/ICodeActionInfoFactory.cs` | Pass — projection boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Execution/Application/CodeActionSolutionChangeCounter.cs` | Pass — candidate impact counting |
| `src/Roslyn.Workbench.Mcp.CodeActions/Execution/Application/ICodeActionSolutionChangeCounter.cs` | Pass — focused counting abstraction |
| `src/Roslyn.Workbench.Mcp.CodeActions/Execution/Contexts/CodeActionMutationContext.cs` | Pass — invocation-specific mutation state |
| `src/Roslyn.Workbench.Mcp.CodeActions/Execution/Contexts/CodeActionQueryContext.cs` | Pass — invocation-specific query state |
| `src/Roslyn.Workbench.Mcp.CodeActions/Execution/Contexts/ICodeActionExecutionContext.cs` | Pass — shared execution state |
| `src/Roslyn.Workbench.Mcp.CodeActions/Execution/ToolExecutionHelpers.cs` | Corrected — CA-AUD-009; request-limit convention |
| `src/Roslyn.Workbench.Mcp.CodeActions/Execution/FixAll/FixAllActionFactory.cs` | Pass — dedicated document, project and solution creation methods |
| `src/Roslyn.Workbench.Mcp.CodeActions/Execution/FixAll/IFixAllActionFactory.cs` | Pass — scope-targeted interface with no redundant prepared-action delegate |
| `src/Roslyn.Workbench.Mcp.CodeActions/Execution/Results/CodeActionExecutionResultFactory.cs` | Pass — result mapping |
| `src/Roslyn.Workbench.Mcp.CodeActions/GlobalUsings.cs` | Pass — owner-appropriate namespaces |
| `src/Roslyn.Workbench.Mcp.CodeActions/Policy/CodeActionExclusions.cs` | Pass — explicit exception policy data |
| `src/Roslyn.Workbench.Mcp.CodeActions/Policy/CodeActionPolicy.cs` | Pass — exception-based policy |
| `src/Roslyn.Workbench.Mcp.CodeActions/Policy/CodeActionPolicyDecision.cs` | Pass — policy result |
| `src/Roslyn.Workbench.Mcp.CodeActions/Policy/ICodeActionPolicy.cs` | Pass — provider and leaf decision boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/References/CodeActionDiagnosticIdentity.cs` | Corrected — CA-AUD-011; mandatory replay diagnostic identity |
| `src/Roslyn.Workbench.Mcp.CodeActions/References/CodeActionReferenceStore.cs` | Corrected — CA-AUD-004 |
| `src/Roslyn.Workbench.Mcp.CodeActions/References/CodeActionReplayRecipe.cs` | Corrected — CA-AUD-011; mandatory replay identity and target state |
| `src/Roslyn.Workbench.Mcp.CodeActions/References/ICodeActionReferenceStore.cs` | Pass — bounded reference-store boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Registration/BundledCodeActionToolRegistrar.cs` | Pass — Host-published internal catalogue registration |
| `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Replay/CodeActionResolutionFailureKind.cs` | Pass — typed replay failures |
| `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Replay/CodeActionResolver.cs` | Pass — ordinary replay invariant |
| `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Replay/ICodeActionResolver.cs` | Pass — ordinary resolution boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Replay/IPreparedFixAllResolver.cs` | Pass — prepared Fix All resolution boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Replay/PreparedFixAllResolver.cs` | Pass — targeted factory dispatch and unified stager path |
| `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Requests/CodeActionToolRequestResolver.cs` | Pass — request and snapshot resolution |
| `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Requests/ICodeActionToolRequestResolver.cs` | Pass — focused request-resolution boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Staging/CodeActionFixAllStager.cs` | Pass — retained pre-migration compatibility reviewed against batch boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Staging/CodeActionSelectionStager.cs` | Pass — retained pre-migration compatibility reviewed against batch boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Staging/CodeActionStager.cs` | Pass — renamed unified single/prepared-action stager |
| `src/Roslyn.Workbench.Mcp.CodeActions/Staging/ICodeActionReferenceStager.cs` | Removed — superseded staging abstraction |
| `src/Roslyn.Workbench.Mcp.CodeActions/Staging/ICodeActionStager.cs` | Pass — unified staging abstraction |
| `src/Roslyn.Workbench.Mcp.CodeActions/Staging/LocationCodeFixStager.cs` | Pass — retained pre-migration compatibility reviewed against batch boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Staging/ScopedCodeFixStager.cs` | Pass — retained pre-migration compatibility reviewed against batch boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Tools/DescribeCodeActionTool.cs` | Pass — retained pre-migration tool reviewed against batch boundary |
| `src/Roslyn.Workbench.Mcp.CodeActions/Tools/ListCodeActionsTool.cs` | Corrected — CA-AUD-003 |
| `src/Roslyn.Workbench.Mcp.CodeActions/Tools/PrepareFixAllTool.cs` | Corrected — CA-AUD-002 and CA-AUD-005 |
| `src/Roslyn.Workbench.Mcp.CodeActions/Tools/StageCodeActionTool.cs` | Pass — unified staging entry point |
| `src/Roslyn.Workbench.Mcp.CodeActions/Tools/StageCodeFixTool.cs` | Removed — superseded dedicated entry point |
| `src/Roslyn.Workbench.Mcp.Plugins.Analyzers/PluginInvocationAnalyzer.cs` | Corrected — CA-AUD-001 |
| `src/Roslyn.Workbench.Mcp.Workspace/Diagnostics/WorkbenchPerformanceEventSource.cs` | Pass — neutral performance events |
| `src/Roslyn.Workbench.Mcp.Workspace/ExecutionContexts/IWorkspaceExecutionContext.cs` | Pass — neutral Workspace execution boundary |
| `src/Roslyn.Workbench.Mcp.Workspace/ExecutionContexts/WorkspaceExecutionContext.cs` | Pass — Workspace-owned execution state |
| `src/Roslyn.Workbench.Mcp.Workspace/ExecutionContexts/WorkspaceExecutionContextFactory.cs` | Pass — context construction |
| `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs` | Pass — neutral lifecycle publication |
| `src/Roslyn.Workbench.Mcp.Workspace/State/IWorkspaceSessionStore.cs` | Pass — session-store boundary |
| `src/Roslyn.Workbench.Mcp.Workspace/State/IWorkspaceSnapshotLifecycleObserver.cs` | Pass — neutral snapshot invalidation abstraction |
| `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSessionSnapshot.cs` | Corrected — CA-AUD-012; stored immutable snapshot identity |
| `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSessionStore.cs` | Corrected — CA-AUD-007 |
| `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSnapshotId.cs` | Pass — stable snapshot identity value |
| `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSnapshotIdentity.cs` | Corrected — CA-AUD-012; central session-identity construction |
| `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceTransactionId.cs` | Pass — transaction identity value |
| `src/Roslyn.Workbench.Mcp.Workspace/Transactions/MutationStagingService.cs` | Corrected — CA-AUD-012 and CA-AUD-013; staging identity and explicit projection |
| `src/Roslyn.Workbench.Mcp.Workspace/Transactions/StagedMutation.cs` | Pass — staged mutation state |
| `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs` | Corrected — CA-AUD-012 and CA-AUD-013; commit identity and explicit recovery flow |
| `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionService.cs` | Corrected — CA-AUD-007, CA-AUD-012 and CA-AUD-013 |
| `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceTransaction.cs` | Pass — reachable-history semantics |
| `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceTransactionAppendResult.cs` | Pass — append outcome |
| `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceTransactionRevision.cs` | Corrected — CA-AUD-011; mandatory revision state |
| `src/Roslyn.Workbench.Mcp/GlobalUsings.cs` | Pass — Host composition namespaces |
| `src/Roslyn.Workbench.Mcp/Hosting/HostConfiguredMsBuildWorkspaceFactory.cs` | Pass — Host-owned Workspace configuration |
| `src/Roslyn.Workbench.Mcp/Hosting/RoslynWorkbenchServiceCollectionExtensions.cs` | Pass — composition and dependency direction |
| `src/Roslyn.Workbench.Mcp/Status/ServerStatusService.cs` | Pass — Code Action component status remains distinct from Plugins |

### Tests — 73 files

| File | Disposition |
| --- | --- |
| `test/Roslyn.Workbench.Mcp.CodeActions.AuditTest/BuiltInCodeActionAuditHarness.cs` | Corrected — CA-AUD-003, CA-AUD-011 and CA-AUD-013; compatibility-audit ownership retained |
| `test/Roslyn.Workbench.Mcp.CodeActions.AuditTest/BuiltInCodeActionInventoryTests.cs` | Pass — pinned provider inventory |
| `test/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest/BuiltInCodeActionStagingIntegrationTests.cs` | Pass — real built-in staging boundary |
| `test/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest/CodeActionDiagnosticSourcesIntegrationTests.cs` | Pass — compiler, built-in and project diagnostic sources |
| `test/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest/ControlledProviderWorkflowIntegrationTests.cs` | Corrected — CA-AUD-002, CA-AUD-003 and CA-AUD-005 workflow assertions |
| `test/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest/MefCodeActionCompositionIntegrationTests.cs` | Pass — renamed real-composition coverage |
| `test/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest/ProjectDiagnosticAnalyzer.cs` | Pass — controlled integration fixture |
| `test/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest/ProjectDiagnosticCodeFixProvider.cs` | Pass — controlled Fix All integration fixture |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Composition/CodeActionAssemblyResolverTests.cs` | Pass — resolution branches |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Composition/CodeActionProviderSelectionTests.cs` | Pass — provider policy branches |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Composition/MefCodeActionCompositionTests.cs` | Pass — renamed composition unit coverage |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Composition/MefHostExportReadResultTests.cs` | Pass — composition result branches |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Discovery/CodeActionAnalyzerActivatorTests.cs` | Pass — compatibility-adapter branches |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Discovery/CodeActionBuiltInAnalyzerIndexTests.cs` | Pass — index materialisation and warnings |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Discovery/CodeActionDiagnosticServiceTests.cs` | Pass — diagnostic-source branches |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Discovery/CodeActionDiscoveryServiceTests.cs` | Pass — discovery and policy branches |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Discovery/CodeActionInfoFactoryTests.cs` | Corrected — CA-AUD-008, CA-AUD-010 and CA-AUD-011; bounded projection and required recipe evidence |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Execution/Application/CodeActionSolutionChangeCounterTests.cs` | Pass — impact counts |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Execution/CodeActionExecutionTestFactory.cs` | Corrected — CA-AUD-011; required replay-recipe test data |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Execution/Contexts/CodeActionExecutionContextFactoryTests.cs` | Pass — context adaptation |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Execution/Contexts/CodeActionExecutionContextTests.cs` | Pass — invocation-state exposure |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Execution/FixAll/FixAllActionFactoryTests.cs` | Pass — document, project and solution-specific construction |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/GlobalUsings.cs` | Pass — test-project conventions |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Policy/CodeActionPolicyTests.cs` | Pass — exception policy |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/References/CodeActionReferenceStoreTests.cs` | Corrected — CA-AUD-004 concurrency and lifecycle evidence |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Registration/BundledCodeActionCatalogTests.cs` | Pass — published internal tool catalogue |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Resolution/Replay/CodeActionResolverTests.cs` | Pass — ordinary replay branches |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Resolution/Replay/PreparedFixAllResolverTests.cs` | Pass — targeted Fix All factory dispatch |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Resolution/Requests/CodeActionToolRequestResolverTests.cs` | Pass — request and stale-snapshot branches |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Staging/CodeActionFixAllStagerTests.cs` | Pass — retained compatibility coverage |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Staging/CodeActionSelectionStagerTests.cs` | Pass — retained compatibility coverage |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Staging/CodeActionStagerTests.cs` | Pass — unified stager coverage |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Staging/LocationCodeFixStagerTests.cs` | Pass — retained compatibility coverage |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Staging/ScopedCodeFixStagerTests.cs` | Pass — retained compatibility coverage |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Tools/DescribeCodeActionToolTests.cs` | Pass — retained pre-migration tool coverage |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Tools/ListCodeActionsToolTests.cs` | Corrected — CA-AUD-003, CA-AUD-009 and CA-AUD-013 |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Tools/PrepareFixAllToolTests.cs` | Corrected — CA-AUD-002, CA-AUD-005, CA-AUD-006, CA-AUD-009 and CA-AUD-013 |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Tools/StageCodeActionToolTests.cs` | Pass — unified staging branches |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Tools/StageCodeFixToolTests.cs` | Removed — tests for superseded dedicated tool |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test/Tools/StageFixAllToolTests.cs` | Corrected — CA-AUD-009; retained pre-migration effective-limit evidence |
| `test/Roslyn.Workbench.Mcp.IntegrationTest/GlobalUsings.cs` | Pass — Host integration namespaces |
| `test/Roslyn.Workbench.Mcp.IntegrationTest/HostCompositionIntegrationTests.cs` | Pass — real Host component composition |
| `test/Roslyn.Workbench.Mcp.IntegrationTest/Protocol/ToolSchemaFactoryIntegrationTests.cs` | Corrected — CA-AUD-002, CA-AUD-003, CA-AUD-006, CA-AUD-009 and CA-AUD-013 schema evidence |
| `test/Roslyn.Workbench.Mcp.IntegrationTest/ServerStatusRecoveryIntegrationTests.cs` | Pass — Code Action component recovery |
| `test/Roslyn.Workbench.Mcp.IntegrationTestSupport/BundledComponentWorkspaceFactory.cs` | Pass — shared real-boundary fixture |
| `test/Roslyn.Workbench.Mcp.IntegrationTestSupport/CodeActionComponentTestSession.cs` | Corrected — CA-AUD-003 consumer |
| `test/Roslyn.Workbench.Mcp.IntegrationTestSupport/CodeActionCompositionFactory.cs` | Pass — renamed composition fixture |
| `test/Roslyn.Workbench.Mcp.IntegrationTestSupport/CodeActionProviderCatalogFactory.cs` | Removed — superseded catalogue fixture |
| `test/Roslyn.Workbench.Mcp.IntegrationTestSupport/ComponentWorkspace.cs` | Pass — shared Workspace fixture |
| `test/Roslyn.Workbench.Mcp.Plugins.Analyzers.Test/AnalyzerSourcePrelude.cs` | Corrected — CA-AUD-001 metadata namespace |
| `test/Roslyn.Workbench.Mcp.Plugins.Analyzers.Test/PluginInvocationAnalyzerTests.cs` | Corrected — CA-AUD-001 analyser expectations |
| `test/Roslyn.Workbench.Mcp.Plugins.Test/Architecture/PluginPublicApiContractTests.cs` | Corrected — CA-AUD-001 public API ownership |
| `test/Roslyn.Workbench.Mcp.Plugins.Test/Execution/PluginExecutionContextFactoryTests.cs` | Pass — plugin context remains independent of CodeActions |
| `test/Roslyn.Workbench.Mcp.Plugins.Test/Execution/PluginExecutionContextTests.cs` | Pass — plugin execution-state contract |
| `test/Roslyn.Workbench.Mcp.Plugins.Test/GlobalUsings.cs` | Pass — namespace update after shared contract move |
| `test/Roslyn.Workbench.Mcp.Test/Contracts/Schema/ContractSchemaTestTools.cs` | Corrected — CA-AUD-006 schema fixture |
| `test/Roslyn.Workbench.Mcp.Test/Contracts/Schema/SchemaGenerationTests.cs` | Corrected — CA-AUD-002, CA-AUD-003, CA-AUD-006 and CA-AUD-008 |
| `test/Roslyn.Workbench.Mcp.Test/Hosting/HostConfiguredMsBuildWorkspaceFactoryTests.cs` | Pass — Host Workspace configuration |
| `test/Roslyn.Workbench.Mcp.Test/Protocol/ToolRequestBinderTests.cs` | Pass — new request binding |
| `test/Roslyn.Workbench.Mcp.Test/Status/ServerStatusServiceTests.cs` | Pass — component/plugin status separation |
| `test/Roslyn.Workbench.Mcp.Test/ToolExecution/CodeActions/CodeActionMutationMcpServerToolTests.cs` | Pass — Host Code Action mutation adapter |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/Contracts/Results/BoundedCollectionTests.cs` | Corrected — CA-AUD-001; renamed from Plugins tests to neutral contract owner coverage |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/ExecutionContexts/WorkspaceExecutionContextFactoryTests.cs` | Pass — neutral execution-context construction |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/Lifecycle/WorkspaceLifecycleServiceTests.cs` | Corrected — CA-AUD-012 and CA-AUD-013; lifecycle identity fixtures and explicit scenario branches |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/Selection/WorkspaceSelectorServiceTests.cs` | Pass — selector changes |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/State/WorkspaceSessionAcquirerTests.cs` | Pass — acquisition and snapshot state |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/State/WorkspaceSessionStoreTests.cs` | Corrected — CA-AUD-012 and CA-AUD-013; internally consistent immutable session replacements |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/State/WorkspaceStateTransitionsTests.cs` | Pass — lifecycle transitions |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/Transactions/MutationStagingServiceTests.cs` | Pass — revision and invalidation publication |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/Transactions/SnapshotGuardTests.cs` | Pass — stale-snapshot protection |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/Transactions/TransactionCommitServiceTests.cs` | Corrected — CA-AUD-012 and CA-AUD-013; commit lifecycle identity fixtures |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/Transactions/TransactionServiceTests.cs` | Corrected — CA-AUD-012 and CA-AUD-013; transaction, undo and redo lifecycle identity fixtures |
| `test/Roslyn.Workbench.Mcp.Workspace.Test/Transactions/WorkspaceTransactionTests.cs` | Pass — reachable revision history |

## Final Disposition

All 183 implementation files have an explicit disposition above. The seven audit findings are closed in the current worktree, the aggregate architecture remains inside the approved Batch 1–5 boundary, and the recorded non-acceptance validation is green.
