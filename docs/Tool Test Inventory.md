# Tool Test Inventory

Checkbox key: `[ ]` not started, `[-]` partial, `[x]` complete.

## Inspection Tools

Coverage note: `FindCalleesTool` and `FindDuplicateCodeTool` are marked complete for this sweep with approved exceptions for defensive Roslyn branches that are not reachable through the real public tool flow. Those guards remain to reduce production risk.

Current sweep note:
- `FindUnusedSymbolsTool` now has unit and integration coverage in place, and the dead `IRangeVariableSymbol` branch has been removed. The remaining partial coverage is in reachable accessibility-filter combinations within `ShouldIncludeSymbol(...)`, so strict 100% now needs additional unit scenarios rather than a production refactor.
- `GetApiSurfaceTool` now has branch-first unit coverage for the reachable `ExecuteAsync(...)` flow, and the dead default arm in `MeetsAccessibilityThreshold(...)` plus the unreachable namespace path in `GetAccessibilityChain(...)` have been removed. The remaining partial coverage is in reachable helper branches such as additional `GetDeclaredSymbol(...)` and accessibility/attribute combinations, so strict 100% now needs more unit scenarios rather than another dead-branch cleanup.
- `GetCodeMetricsTool` now has unit and integration coverage in place, but strict class-level 100% still needs an implementation decision. The document-scope declaration walk never reaches the `DelegateDeclarationSyntax` arm in `GetDeclaredSymbol(...)`, `GetMaxNestingDepthCore(...)` cannot recurse from the current declaration entry points, and the defensive `sourceLocation is null` guard in `TryCreateMetricTarget(...)` is not reachable through the real public flow.
- `GetControlFlowGraphTool` now has branch-first unit coverage for the reachable `ExecuteAsync(...)` flow, and the dead-entry `selector is null` helper branch has been removed. The remaining partial coverage is the defensive `syntaxRoot is null || semanticModel is null` guard, which would require an unsupported Roslyn document that still resolves through the normal source-tree location flow.
- `GetDiagnosticsTool` now has branch-first unit coverage for the reachable `ExecuteAsync(...)` flow, but strict class-level 100% still needs an explicit exception decision for `DiagnosticComparer.Equals(...)` when one side is null. The current public flow only produces non-null Roslyn diagnostics, so that guard cannot be exercised without artificial test-only inputs.
- `GetDocumentOptionsTool` now has unit and integration coverage in place, but one parse-options branch remains open. Current tests cover the C# branch and the unsupported-document null branch; strict class-level 100% still needs a dedicated non-C# Roslyn document fixture to drive the non-null, non-C# parse-options path.
- `GetOperationTreeTool` now has branch-first unit coverage for the reachable `ExecuteAsync(...)` flow, but `ResolveSyntaxNodeAsync(...)` still contains an approved defensive guard that is not reachable through the real public flow. Hitting `syntaxRoot is null || semanticModel is null` would require an unsupported Roslyn document that still resolves through the normal source-tree location flow.

| Done | Tool Class | Unit Test | Integration Test |
| --- | --- | --- | --- |
| [x] | AnalyzeAsyncTool | AnalyzeAsyncToolTests | AnalyzeAsyncToolIntegrationTests |
| [x] | AnalyzeControlFlowTool | AnalyzeControlFlowToolTests | AnalyzeControlFlowToolIntegrationTests |
| [x] | AnalyzeDataFlowTool | AnalyzeDataFlowToolTests | AnalyzeDataFlowToolIntegrationTests |
| [x] | AnalyzeDisposablesTool | AnalyzeDisposablesToolTests | AnalyzeDisposablesToolIntegrationTests |
| [x] | AnalyzeNullabilityTool | AnalyzeNullabilityToolTests | AnalyzeNullabilityToolIntegrationTests |
| [x] | FindCalleesTool | FindCalleesToolTests | FindCalleesToolIntegrationTests |
| [x] | FindCallersTool | FindCallersToolTests | FindCallersToolIntegrationTests |
| [x] | FindDependencyCyclesTool | FindDependencyCyclesToolTests | FindDependencyCyclesToolIntegrationTests |
| [x] | FindDerivedTypesTool | FindDerivedTypesToolTests | FindDerivedTypesToolIntegrationTests |
| [x] | FindDuplicateCodeTool | FindDuplicateCodeToolTests | FindDuplicateCodeToolIntegrationTests |
| [x] | FindImplementationsTool | FindImplementationsToolTests | FindImplementationsToolIntegrationTests |
| [x] | FindOverloadsTool | FindOverloadsToolTests | FindOverloadsToolIntegrationTests |
| [x] | FindOverridesTool | FindOverridesToolTests | FindOverridesToolIntegrationTests |
| [x] | FindReferencesTool | FindReferencesToolTests | FindReferencesToolIntegrationTests |
| [x] | FindUnusedSymbolsTool | FindUnusedSymbolsToolTests | FindUnusedSymbolsToolIntegrationTests |
| [-] | GetApiSurfaceTool | GetApiSurfaceToolTests | GetApiSurfaceToolIntegrationTests |
| [x] | GetChangeImpactTool | GetChangeImpactToolTests | GetChangeImpactToolIntegrationTests |
| [-] | GetCodeContextTool | GetCodeContextToolTests | GetCodeContextToolIntegrationTests |
| [x] | GetCodeMetricsTool | GetCodeMetricsToolTests | GetCodeMetricsToolIntegrationTests |
| [x] | GetControlFlowGraphTool | GetControlFlowGraphToolTests | GetControlFlowGraphToolIntegrationTests |
| [x] | GetDependencyGraphTool | GetDependencyGraphToolTests | GetDependencyGraphToolIntegrationTests |
| [x] | GetDiagnosticsTool | GetDiagnosticsToolTests | GetDiagnosticsToolIntegrationTests |
| [x] | GetDocumentOptionsTool | GetDocumentOptionsToolTests | GetDocumentOptionsToolIntegrationTests |
| [x] | GetDocumentOutlineTool | GetDocumentOutlineToolTests | GetDocumentOutlineToolIntegrationTests |
| [x] | GetOperationTreeTool | GetOperationTreeToolTests | GetOperationTreeToolIntegrationTests |
| [x] | GetPartialDeclarationsTool | GetPartialDeclarationsToolTests | GetPartialDeclarationsToolIntegrationTests |
| [x] | GetProjectDetailsTool | GetProjectDetailsToolTests | GetProjectDetailsToolIntegrationTests |
| [x] | GetSolutionStructureTool | GetSolutionStructureToolTests | GetSolutionStructureToolIntegrationTests |
| [x] | GetSymbolAttributesTool | GetSymbolAttributesToolTests | GetSymbolAttributesToolIntegrationTests |
| [x] | GetSymbolDependenciesTool | GetSymbolDependenciesToolTests | GetSymbolDependenciesToolIntegrationTests |
| [x] | GetSymbolDependentsTool | GetSymbolDependentsToolTests | GetSymbolDependentsToolIntegrationTests |
| [x] | GetSymbolInfoTool | GetSymbolInfoToolTests | GetSymbolInfoToolIntegrationTests |
| [x] | GetSymbolMembersTool | GetSymbolMembersToolTests | GetSymbolMembersToolIntegrationTests |
| [x] | GetTestImpactTool | GetTestImpactToolTests | GetTestImpactToolIntegrationTests |
| [x] | GetTypeHierarchyTool | GetTypeHierarchyToolTests | GetTypeHierarchyToolIntegrationTests |
| [x] | GoToDefinitionTool | GoToDefinitionToolTests | GoToDefinitionToolIntegrationTests |
| [x] | ResolveSymbolTool | ResolveSymbolToolTests | ResolveSymbolToolIntegrationTests |
| [x] | SearchSymbolsTool | SearchSymbolsToolTests | SearchSymbolsToolIntegrationTests |

## Refactoring Tools

Coverage note: `[x]` means the tool has current dedicated unit coverage, matching integration coverage, and no unresolved coverage gap. `[-]` means coverage is missing or an unreachable branch still needs an implementation decision. `[ ]` means neither unit nor integration coverage exists.

Current sweep note:
- `RenameSymbolTool` has branch-first unit coverage for every reachable public flow. The remaining `ReferenceEquals(candidateSolution, context.CurrentSolution)` no-change branch cannot be reached with a valid Roslyn source symbol: a valid rename returns a new solution, while symbols outside the current source solution are rejected by Roslyn before this branch.
- `SortUsingsTool` has 100% line and branch coverage for `ExecuteCoreAsync(...)`. The remaining class-level branches are defensive null handling for `UsingDirectiveSyntax.Name`; parsed and factory-created using directives expose a non-null name node, including malformed directives where Roslyn supplies a missing node.

| Done | Tool Class | Unit Test | Integration Test |
| --- | --- | --- | --- |
| [x] | FormatDocumentTool | FormatDocumentToolTests | FormatDocumentToolIntegrationTests |
| [-] | RenameSymbolTool | RenameSymbolToolTests | RenameSymbolToolIntegrationTests |
| [-] | SortUsingsTool | SortUsingsToolTests | SortUsingsToolIntegrationTests |

## Code Action Tools

Coverage note: `[x]` means the tool has current dedicated unit coverage and matching integration coverage. `[-]` means unit coverage is missing, old-pattern, or integration coverage is missing. `[ ]` means neither unit nor integration coverage exists.

| Done | Category | Tool Class | Unit Test | Integration Test |
| --- | --- | --- | --- | --- |
| [x] | core code action tool | DescribeCodeActionTool | DescribeCodeActionToolTests | DescribeCodeActionToolIntegrationTests |
| [x] | core code action tool | ListCodeActionsTool | ListCodeActionsToolTests | ListCodeActionsToolIntegrationTests |
| [x] | core code action tool | StageCodeActionTool | StageCodeActionToolTests | StageCodeActionToolIntegrationTests |
| [x] | core code action tool | StageCodeFixTool | StageCodeFixToolTests | StageCodeFixToolIntegrationTests |
| [x] | core code action tool | StageFixAllTool | StageFixAllToolTests | StageFixAllToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | AddAwaitTool | AddAwaitToolTests | AddAwaitToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | AddDebuggerDisplayTool | AddDebuggerDisplayToolTests | AddDebuggerDisplayToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | AddImportTool | AddImportToolTests | AddImportToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | AddMissingUsingsTool | AddMissingUsingsToolTests | AddMissingUsingsToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | AddNullChecksTool | AddNullChecksToolTests | AddNullChecksToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertAnonymousTypeToClassTool | ConvertAnonymousTypeToClassToolTests | ConvertAnonymousTypeToClassToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertAnonymousTypeToTupleTool | ConvertAnonymousTypeToTupleToolTests | ConvertAnonymousTypeToTupleToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertAutoPropertyToFullPropertyTool | ConvertAutoPropertyToFullPropertyToolTests | ConvertAutoPropertyToFullPropertyToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertBetweenRegularAndVerbatimInterpolatedStringTool | ConvertBetweenRegularAndVerbatimInterpolatedStringToolTests | ConvertBetweenRegularAndVerbatimInterpolatedStringToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertBetweenRegularAndVerbatimStringTool | ConvertBetweenRegularAndVerbatimStringToolTests | ConvertBetweenRegularAndVerbatimStringToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertDirectCastToTryCastTool | ConvertDirectCastToTryCastToolTests | ConvertDirectCastToTryCastToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertForeachLinqTool | ConvertForeachLinqToolTests | ConvertForeachLinqToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertForEachToForTool | ConvertForEachToForToolTests | ConvertForEachToForToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertForToForeachTool | ConvertForToForeachToolTests | ConvertForToForeachToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertIfToSwitchTool | ConvertIfToSwitchToolTests | ConvertIfToSwitchToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertLocalFunctionToMethodTool | ConvertLocalFunctionToMethodToolTests | ConvertLocalFunctionToMethodToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertPrimaryToRegularConstructorTool | ConvertPrimaryToRegularConstructorToolTests | ConvertPrimaryToRegularConstructorToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertPropertyTool | ConvertPropertyToolTests | ConvertPropertyToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertToRecordTool | ConvertToRecordToolTests | ConvertToRecordToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ConvertTryCastToDirectCastTool | ConvertTryCastToDirectCastToolTests | ConvertTryCastToDirectCastToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ExtractMethodTool | ExtractMethodToolTests | ExtractMethodToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | IntroduceParameterTool | IntroduceParameterToolTests | IntroduceParameterToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | IntroduceUsingStatementTool | IntroduceUsingStatementToolTests | IntroduceUsingStatementToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | IntroduceVariableTool | IntroduceVariableToolTests | IntroduceVariableToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | InvertConditionalTool | InvertConditionalToolTests | InvertConditionalToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | InvertIfTool | InvertIfToolTests | InvertIfToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | InvertLogicalTool | InvertLogicalToolTests | InvertLogicalToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | MakeLocalFunctionStaticTool | MakeLocalFunctionStaticToolTests | MakeLocalFunctionStaticToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | MoveDeclarationNearReferenceTool | MoveDeclarationNearReferenceToolTests | MoveDeclarationNearReferenceToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | NameTupleElementTool | NameTupleElementToolTests | NameTupleElementToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | RemoveUnusedUsingsTool | RemoveUnusedUsingsToolTests | RemoveUnusedUsingsToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ReplaceConditionalWithStatementsTool | ReplaceConditionalWithStatementsToolTests | ReplaceConditionalWithStatementsToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ReplaceDocCommentTextWithTagTool | ReplaceDocCommentTextWithTagToolTests | ReplaceDocCommentTextWithTagToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | ReverseForStatementTool | ReverseForStatementToolTests | ReverseForStatementToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | UseExplicitTypeTool | UseExplicitTypeToolTests | UseExplicitTypeToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | UseImplicitTypeTool | UseImplicitTypeToolTests | UseImplicitTypeToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | UseNamedArgumentsTool | UseNamedArgumentsToolTests | UseNamedArgumentsToolIntegrationTests |
| [x] | refactor, simple redirection service call tool | UseRecursivePatternsTool | UseRecursivePatternsToolTests | UseRecursivePatternsToolIntegrationTests |
| [x] | refactor, complex tool | ConvertExpressionBodyTool | ConvertExpressionBodyToolTests | ConvertExpressionBodyToolIntegrationTests |
| [x] | refactor, complex tool | ConvertToInterpolatedStringTool | ConvertToInterpolatedStringToolTests | ConvertToInterpolatedStringToolIntegrationTests |
| [x] | refactor, complex tool | EncapsulateFieldTool | EncapsulateFieldToolTests | EncapsulateFieldToolIntegrationTests |
| [x] | refactor, complex tool | InlineVariableTool | InlineVariableToolTests | InlineVariableToolIntegrationTests |
| [x] | refactor, complex tool | MoveTypeToFileTool | MoveTypeToFileToolTests | MoveTypeToFileToolIntegrationTests |
