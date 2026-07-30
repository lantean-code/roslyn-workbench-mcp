# Code Action Batch 6 Handler Disposition

Date: 2026-07-29

## Decision

All 83 dedicated Code Action handlers were adapters over Roslyn `CodeFixProvider` or `CodeRefactoringProvider` actions. None implemented a Workbench-owned semantic transformation, and none justified migration to Plugins.Core. The callable handlers and their dedicated request contracts were therefore removed. Ordinary leaves now use `list-code-actions` followed by `stage-code-action`; supported provider Fix All operations use `prepare-fix-all` followed by `stage-code-action`.

The existing Workbench-owned `rename-symbol` and `format-document` operations already live in Plugins.Core and were not part of the removed dedicated surface.

## Diagnostic Code Fix wrappers

The following 36 handlers selected or replayed diagnostic-driven Roslyn Code Fix leaves. Their disposition is ordinary diagnostic wrapper removal. `AddMissingUsingsTool` and `RemoveUnusedUsingsTool` also supplied custom scoped aggregation; their ordinary document leaves remain discoverable, while provider-supported wider aggregation uses the generic Fix All workflow. No separate Workbench-owned aggregation was approved.

- `AddAnonymousTypeMemberNameTool`
- `AddConditionalInterpolationParenthesesTool`
- `AddDocumentationCommentNodesTool`
- `AddExplicitCastTool`
- `AddInheritdocTool`
- `AddMissingUsingsTool`
- `AddObsoleteAttributeTool`
- `AddYieldTool`
- `AssignOutParametersTool`
- `ChangeIteratorReturnTypeTool`
- `DeclareAsNullableTool`
- `DisambiguateSameVariableTool`
- `FixIncorrectConstraintTool`
- `FixReturnTypeTool`
- `FixedCompilerCodeFixTool`
- `HideBaseMemberTool`
- `MakeMemberRequiredTool`
- `MakeMemberStaticTool`
- `MakeMethodAsynchronousTool`
- `MakeRefStructTool`
- `MakeStatementAsynchronousTool`
- `MakeTypeAbstractTool`
- `MakeTypePartialTool`
- `OrderModifiersTool`
- `PassCapturedVariablesAsArgumentsTool`
- `RemoveDocumentationCommentNodeTool`
- `RemoveInKeywordTool`
- `RemoveNewModifierTool`
- `RemoveUnusedLocalFunctionTool`
- `RemoveUnusedUsingsTool`
- `ReplaceDefaultLiteralTool`
- `TransposeRecordKeywordTool`
- `UnsealClassTool`
- `UseExplicitArrayInExpressionTreeTool`
- `UseExplicitTypeForConstTool`
- `UseInterpolatedVerbatimStringTool`

## Refactoring leaf selectors

The following 46 handlers selected one of the ordinary leaves returned by a Roslyn refactoring provider. Their disposition is leaf-selector removal: every eligible leaf receives its own opaque reference from `list-code-actions`.

- `AddAwaitTool`
- `AddConstructorParametersTool`
- `AddDebuggerDisplayTool`
- `AddImportTool`
- `AddNullChecksTool`
- `ConvertAnonymousTypeToClassTool`
- `ConvertAnonymousTypeToTupleTool`
- `ConvertAutoPropertyToFullPropertyTool`
- `ConvertBetweenRegularAndVerbatimInterpolatedStringTool`
- `ConvertBetweenRegularAndVerbatimStringTool`
- `ConvertDirectCastToTryCastTool`
- `ConvertExpressionBodyTool`
- `ConvertForEachToForTool`
- `ConvertForToForeachTool`
- `ConvertForeachLinqTool`
- `ConvertIfToSwitchTool`
- `ConvertLocalFunctionToMethodTool`
- `ConvertPrimaryToRegularConstructorTool`
- `ConvertToInterpolatedStringTool`
- `ConvertToRecordTool`
- `ConvertTryCastToDirectCastTool`
- `EncapsulateFieldTool`
- `ExtractMethodTool`
- `GenerateComparisonOperatorsTool`
- `ImplementInterfaceTool`
- `InlineVariableTool`
- `IntroduceParameterTool`
- `IntroduceUsingStatementTool`
- `IntroduceVariableTool`
- `InvertConditionalTool`
- `InvertIfTool`
- `InvertLogicalTool`
- `MakeLocalFunctionStaticTool`
- `MoveDeclarationNearReferenceTool`
- `MoveTypeToFileTool`
- `NameTupleElementTool`
- `OrganizeImportsTool`
- `ReplaceConditionalWithStatementsTool`
- `ReplaceDocCommentTextWithTagTool`
- `ReplaceMethodWithPropertyTool`
- `ReplacePropertyWithMethodsTool`
- `ReverseForStatementTool`
- `UseExplicitTypeTool`
- `UseImplicitTypeTool`
- `UseNamedArgumentsTool`
- `UseRecursivePatternsTool`

## Mixed location and leaf selector

- `ConvertPropertyTool` selected an ordinary Roslyn conversion leaf through either a location or a named direction. Its disposition is leaf-selector removal; the eligible conversion leaves are listed independently and staged by opaque reference.

## Plugins.Core and exclusions

No removed handler was approved for Plugins.Core migration. Provider or action shapes that require options, external UI, package/reference installation, or another unsupported interaction remain governed by the exception policy and are omitted from generic discovery; they do not receive dedicated MCP registrations.
