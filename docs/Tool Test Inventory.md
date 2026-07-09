# Tool Test Inventory

Checkbox key: `[ ]` not started, `[-]` partial, `[x]` complete.

## Inspection Tools

Coverage note: `FindCalleesTool` and `FindDuplicateCodeTool` are marked complete for this sweep with approved exceptions for defensive Roslyn branches that are not reachable through the real public tool flow. Those guards remain to reduce production risk.

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
| [ ] | FindReferencesTool | FindReferencesToolTests | FindReferencesToolIntegrationTests |
| [ ] | FindUnusedSymbolsTool | FindUnusedSymbolsToolTests | FindUnusedSymbolsToolIntegrationTests |
| [ ] | GetApiSurfaceTool | GetApiSurfaceToolTests | GetApiSurfaceToolIntegrationTests |
| [ ] | GetChangeImpactTool | GetChangeImpactToolTests | GetChangeImpactToolIntegrationTests |
| [ ] | GetCodeContextTool | GetCodeContextToolTests | GetCodeContextToolIntegrationTests |
| [ ] | GetCodeMetricsTool | GetCodeMetricsToolTests | GetCodeMetricsToolIntegrationTests |
| [ ] | GetControlFlowGraphTool | GetControlFlowGraphToolTests | GetControlFlowGraphToolIntegrationTests |
| [ ] | GetDependencyGraphTool | GetDependencyGraphToolTests | GetDependencyGraphToolIntegrationTests |
| [ ] | GetDiagnosticsTool | GetDiagnosticsToolTests | GetDiagnosticsToolIntegrationTests |
| [ ] | GetDocumentOptionsTool | GetDocumentOptionsToolTests | GetDocumentOptionsToolIntegrationTests |
| [ ] | GetDocumentOutlineTool | GetDocumentOutlineToolTests | GetDocumentOutlineToolIntegrationTests |
| [ ] | GetOperationTreeTool | GetOperationTreeToolTests | GetOperationTreeToolIntegrationTests |
| [ ] | GetPartialDeclarationsTool | GetPartialDeclarationsToolTests | GetPartialDeclarationsToolIntegrationTests |
| [ ] | GetProjectDetailsTool | GetProjectDetailsToolTests | GetProjectDetailsToolIntegrationTests |
| [ ] | GetSolutionStructureTool | GetSolutionStructureToolTests | GetSolutionStructureToolIntegrationTests |
| [ ] | GetSymbolAttributesTool | GetSymbolAttributesToolTests | GetSymbolAttributesToolIntegrationTests |
| [ ] | GetSymbolDependenciesTool | GetSymbolDependenciesToolTests | GetSymbolDependenciesToolIntegrationTests |
| [ ] | GetSymbolDependentsTool | GetSymbolDependentsToolTests | GetSymbolDependentsToolIntegrationTests |
| [ ] | GetSymbolInfoTool | GetSymbolInfoToolTests | GetSymbolInfoToolIntegrationTests |
| [ ] | GetSymbolMembersTool | GetSymbolMembersToolTests | GetSymbolMembersToolIntegrationTests |
| [ ] | GetTestImpactTool | GetTestImpactToolTests | GetTestImpactToolIntegrationTests |
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
