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
| [-] | FindUnusedSymbolsTool | FindUnusedSymbolsToolTests | FindUnusedSymbolsToolIntegrationTests |
| [-] | GetApiSurfaceTool | GetApiSurfaceToolTests | GetApiSurfaceToolIntegrationTests |
| [x] | GetChangeImpactTool | GetChangeImpactToolTests | GetChangeImpactToolIntegrationTests |
| [-] | GetCodeContextTool | GetCodeContextToolTests | GetCodeContextToolIntegrationTests |
| [-] | GetCodeMetricsTool | GetCodeMetricsToolTests | GetCodeMetricsToolIntegrationTests |
| [-] | GetControlFlowGraphTool | GetControlFlowGraphToolTests | GetControlFlowGraphToolIntegrationTests |
| [x] | GetDependencyGraphTool | GetDependencyGraphToolTests | GetDependencyGraphToolIntegrationTests |
| [-] | GetDiagnosticsTool | GetDiagnosticsToolTests | GetDiagnosticsToolIntegrationTests |
| [-] | GetDocumentOptionsTool | GetDocumentOptionsToolTests | GetDocumentOptionsToolIntegrationTests |
| [x] | GetDocumentOutlineTool | GetDocumentOutlineToolTests | GetDocumentOutlineToolIntegrationTests |
| [-] | GetOperationTreeTool | GetOperationTreeToolTests | GetOperationTreeToolIntegrationTests |
| [x] | GetPartialDeclarationsTool | GetPartialDeclarationsToolTests | GetPartialDeclarationsToolIntegrationTests |
| [x] | GetProjectDetailsTool | GetProjectDetailsToolTests | GetProjectDetailsToolIntegrationTests |
| [x] | GetSolutionStructureTool | GetSolutionStructureToolTests | GetSolutionStructureToolIntegrationTests |
| [x] | GetSymbolAttributesTool | GetSymbolAttributesToolTests | GetSymbolAttributesToolIntegrationTests |
| [x] | GetSymbolDependenciesTool | GetSymbolDependenciesToolTests | GetSymbolDependenciesToolIntegrationTests |
| [x] | GetSymbolDependentsTool | GetSymbolDependentsToolTests | GetSymbolDependentsToolIntegrationTests |
| [x] | GetSymbolInfoTool | GetSymbolInfoToolTests | GetSymbolInfoToolIntegrationTests |
| [x] | GetSymbolMembersTool | GetSymbolMembersToolTests | GetSymbolMembersToolIntegrationTests |
| [x] | GetTestImpactTool | GetTestImpactToolTests | GetTestImpactToolIntegrationTests |
| [ ] | GetTypeHierarchyTool | GetTypeHierarchyToolTests | GetTypeHierarchyToolIntegrationTests |
| [ ] | GoToDefinitionTool | GoToDefinitionToolTests | GoToDefinitionToolIntegrationTests |
| [ ] | ResolveSymbolTool | ResolveSymbolToolTests | ResolveSymbolToolIntegrationTests |
| [ ] | SearchSymbolsTool | SearchSymbolsToolTests | SearchSymbolsToolIntegrationTests |

## Refactoring Tools

| Done | Tool Class | Unit Test | Integration Test |
| --- | --- | --- | --- |
| [ ] | AddAwaitTool | AddAwaitToolTests | AddAwaitToolIntegrationTests |
| [ ] | AddDebuggerDisplayTool | AddDebuggerDisplayToolTests | AddDebuggerDisplayToolIntegrationTests |
| [ ] | AddImportTool | AddImportToolTests | AddImportToolIntegrationTests |
| [ ] | AddMissingUsingsTool | AddMissingUsingsToolTests | AddMissingUsingsToolIntegrationTests |
| [ ] | AddNullChecksTool | AddNullChecksToolTests | AddNullChecksToolIntegrationTests |
| [ ] | ConvertAnonymousTypeToClassTool | ConvertAnonymousTypeToClassToolTests | ConvertAnonymousTypeToClassToolIntegrationTests |
| [ ] | ConvertAnonymousTypeToTupleTool | ConvertAnonymousTypeToTupleToolTests | ConvertAnonymousTypeToTupleToolIntegrationTests |
| [ ] | ConvertAutoPropertyToFullPropertyTool | ConvertAutoPropertyToFullPropertyToolTests | ConvertAutoPropertyToFullPropertyToolIntegrationTests |
| [ ] | ConvertBetweenRegularAndVerbatimInterpolatedStringTool | ConvertBetweenRegularAndVerbatimInterpolatedStringToolTests | ConvertBetweenRegularAndVerbatimInterpolatedStringToolIntegrationTests |
| [ ] | ConvertBetweenRegularAndVerbatimStringTool | ConvertBetweenRegularAndVerbatimStringToolTests | ConvertBetweenRegularAndVerbatimStringToolIntegrationTests |
| [ ] | ConvertDirectCastToTryCastTool | ConvertDirectCastToTryCastToolTests | ConvertDirectCastToTryCastToolIntegrationTests |
| [ ] | ConvertExpressionBodyTool | ConvertExpressionBodyToolTests | ConvertExpressionBodyToolIntegrationTests |
| [ ] | ConvertForEachToForTool | ConvertForEachToForToolTests | ConvertForEachToForToolIntegrationTests |
| [ ] | ConvertForToForeachTool | ConvertForToForeachToolTests | ConvertForToForeachToolIntegrationTests |
| [ ] | ConvertForeachLinqTool | ConvertForeachLinqToolTests | ConvertForeachLinqToolIntegrationTests |
| [ ] | ConvertIfToSwitchTool | ConvertIfToSwitchToolTests | ConvertIfToSwitchToolIntegrationTests |
| [ ] | ConvertLocalFunctionToMethodTool | ConvertLocalFunctionToMethodToolTests | ConvertLocalFunctionToMethodToolIntegrationTests |
| [ ] | ConvertPrimaryToRegularConstructorTool | ConvertPrimaryToRegularConstructorToolTests | ConvertPrimaryToRegularConstructorToolIntegrationTests |
| [ ] | ConvertPropertyTool | ConvertPropertyToolTests | ConvertPropertyToolIntegrationTests |
| [ ] | ConvertToInterpolatedStringTool | ConvertToInterpolatedStringToolTests | ConvertToInterpolatedStringToolIntegrationTests |
| [ ] | ConvertToRecordTool | ConvertToRecordToolTests | ConvertToRecordToolIntegrationTests |
| [ ] | ConvertTryCastToDirectCastTool | ConvertTryCastToDirectCastToolTests | ConvertTryCastToDirectCastToolIntegrationTests |
| [ ] | EncapsulateFieldTool | EncapsulateFieldToolTests | EncapsulateFieldToolIntegrationTests |
| [ ] | ExtractMethodTool | ExtractMethodToolTests | ExtractMethodToolIntegrationTests |
| [ ] | FormatDocumentTool | FormatDocumentToolTests | FormatDocumentToolIntegrationTests |
| [ ] | InlineVariableTool | InlineVariableToolTests | InlineVariableToolIntegrationTests |
| [ ] | IntroduceParameterTool | IntroduceParameterToolTests | IntroduceParameterToolIntegrationTests |
| [ ] | IntroduceUsingStatementTool | IntroduceUsingStatementToolTests | IntroduceUsingStatementToolIntegrationTests |
| [ ] | IntroduceVariableTool | IntroduceVariableToolTests | IntroduceVariableToolIntegrationTests |
| [ ] | InvertConditionalTool | InvertConditionalToolTests | InvertConditionalToolIntegrationTests |
| [ ] | InvertIfTool | InvertIfToolTests | InvertIfToolIntegrationTests |
| [ ] | InvertLogicalTool | InvertLogicalToolTests | InvertLogicalToolIntegrationTests |
| [ ] | MakeLocalFunctionStaticTool | MakeLocalFunctionStaticToolTests | MakeLocalFunctionStaticToolIntegrationTests |
| [ ] | MoveDeclarationNearReferenceTool | MoveDeclarationNearReferenceToolTests | MoveDeclarationNearReferenceToolIntegrationTests |
| [ ] | MoveTypeToFileTool | MoveTypeToFileToolTests | MoveTypeToFileToolIntegrationTests |
| [ ] | NameTupleElementTool | NameTupleElementToolTests | NameTupleElementToolIntegrationTests |
| [ ] | RemoveUnusedUsingsTool | RemoveUnusedUsingsToolTests | RemoveUnusedUsingsToolIntegrationTests |
| [ ] | RenameSymbolTool | RenameSymbolToolTests | RenameSymbolToolIntegrationTests |
| [ ] | ReplaceConditionalWithStatementsTool | ReplaceConditionalWithStatementsToolTests | ReplaceConditionalWithStatementsToolIntegrationTests |
| [ ] | ReplaceDocCommentTextWithTagTool | ReplaceDocCommentTextWithTagToolTests | ReplaceDocCommentTextWithTagToolIntegrationTests |
| [ ] | ReverseForStatementTool | ReverseForStatementToolTests | ReverseForStatementToolIntegrationTests |
| [ ] | SortUsingsTool | SortUsingsToolTests | SortUsingsToolIntegrationTests |
| [ ] | UseExplicitTypeTool | UseExplicitTypeToolTests | UseExplicitTypeToolIntegrationTests |
| [ ] | UseImplicitTypeTool | UseImplicitTypeToolTests | UseImplicitTypeToolIntegrationTests |
| [ ] | UseNamedArgumentsTool | UseNamedArgumentsToolTests | UseNamedArgumentsToolIntegrationTests |
| [ ] | UseRecursivePatternsTool | UseRecursivePatternsToolTests | UseRecursivePatternsToolIntegrationTests |

## Code-Action Tools

| Done | Tool Class | Unit Test | Integration Test |
| --- | --- | --- | --- |
| [ ] | DescribeCodeActionTool | DescribeCodeActionToolTests | DescribeCodeActionToolIntegrationTests |
| [ ] | ListCodeActionsTool | ListCodeActionsToolTests | ListCodeActionsToolIntegrationTests |
| [ ] | StageCodeActionTool | StageCodeActionToolTests | StageCodeActionToolIntegrationTests |
| [ ] | StageCodeFixTool | StageCodeFixToolTests | StageCodeFixToolIntegrationTests |
| [ ] | StageFixAllTool | StageFixAllToolTests | StageFixAllToolIntegrationTests |
