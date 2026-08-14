# Roslyn Code Action Source Analysis — 2026-07-27

## Purpose

This document records the source analysis and provider-by-provider classification of every C# Code Action provider composed by Roslyn Workbench against the exact Roslyn source used by `Microsoft.CodeAnalysis.Features` and `Microsoft.CodeAnalysis.CSharp.Features` 5.6.0. It is retained development evidence, not a runtime catalogue: availability records the repository state observed during the analysis, while the architecture plan defines the replacement runtime model.

Roslyn action titles and concrete leaf counts depend on the document, span, diagnostics and semantic state supplied at discovery time. The exhaustive stable unit is therefore the provider identity. A provider classified as mixed must still be assessed at the individual action-leaf level before it can be exposed.

## Authority and Scope

The source classification uses Roslyn commit [`c0573ed0a7dc3e3b4d2e70da47f97cc51a35524f`](https://github.com/dotnet/roslyn/tree/c0573ed0a7dc3e3b4d2e70da47f97cc51a35524f), identified by the 5.6.0 package metadata and SourceLink data. The provider set is the 250 C# providers produced by the server's real MEF composition: 169 code-fix providers and 81 refactoring providers.

The production ledger and development-only provider assessments are the authoritative local provider set. The source checkout determines execution category. Runtime compatibility, diagnostic reachability and acceptance evidence determine availability. These dimensions are intentionally separate.

## Category Definitions

| Category | Meaning |
| --- | --- |
| Ordinary replay | The provider produces ordinary deterministic Code Actions that can use the existing discovery, opaque-reference and transaction-staging pipeline. A provider can remain unavailable because a diagnostic or compatibility prerequisite has not yet been completed. |
| Mixed provider | The provider can produce both safe ordinary actions and unsupported, externally effectful or option-backed actions. It requires leaf-level classification before broader exposure. |
| Option-backed only | The provider's useful route is a `CodeActionWithOptions` workflow. It cannot be replayed headlessly without a deliberately designed, strongly typed Workbench contract and implementation. |
| Internal-service dependent | The provider requires transient editor or host state that does not exist in the Workbench runtime. |
| Product-boundary exclusion | The provider requires project/package mutation, external intelligence or another effect outside the staged source-document transaction boundary. |
| Custom semantic implementation | No composed provider belongs here. This category is reserved for a Workbench-owned replacement built from public Roslyn primitives when the original provider cannot be replayed. |

## Summary

| Category | Composed providers |
| --- | ---: |
| Ordinary replay | 231 |
| Mixed provider | 5 |
| Option-backed only | 6 |
| Internal-service dependent | 1 |
| Product-boundary exclusion | 7 |
| Custom semantic implementation | 0 |
| **Total** | **250** |

Availability is not another execution category. For example, a provider can be ordinary replay while remaining hidden until Workbench activates its built-in diagnostic, or until a diagnostic-specific compatibility case proves deterministic selection and staging.

## Material Discoveries

### Runtime composition corrected the original source inventory

The original source-folder audit did not include language-neutral Core providers exported for C#. Comparing the production ledger and manual assessment data with the Host's real MEF composition established the authoritative total of 250 providers. This found 151 Code Fix providers that had not been assessed by the earlier curated source scan.

The 151 additional providers divided into:

| Availability finding | Count |
| --- | ---: |
| Compiler-backed replay candidates | 47 |
| Roslyn built-in diagnostic activation required | 94 |
| Equivalent behaviour covered by an existing dedicated tool | 8 |
| Project-setting mutation exclusions | 2 |
| **Total** | **151** |

No additional `CodeActionWithOptions` implementation belonged to those 151 providers. The 94 built-in-diagnostic providers already have composed Code Fix implementations; the missing capability is production activation of the Roslyn `IDE` analysers that create their triggering diagnostics.

### Option-backed implementations are concentrated in ten families

Source inspection found ten `CodeActionWithOptions` families in the loaded Features assemblies:

| Provider family | Source finding |
| --- | --- |
| `GenerateType` | Mixed: ordinary placement leaves coexist with a configurable options leaf. |
| `GenerateConstructorFromMembers` | Mixed: selected-member and deterministic base-constructor leaves coexist with a caret-only member-picker leaf. |
| `GenerateEqualsAndGetHashCodeFromMembers` | Mixed: selected-member generation coexists with a caret-only member-picker leaf. |
| `PullMemberUp` | Mixed assessment: the options-backed member and destination workflow must be excluded; any ordinary leaves require exact compatibility evidence. |
| `ChangeSignature` | Option-backed only. |
| `GenerateOverrides` | Option-backed only. |
| `ExtractInterface` | Option-backed only. |
| `ExtractClass` | Option-backed only. |
| `MoveStaticMembers` | Option-backed only. |
| `MoveToNamespace` | Option-backed only. |

`AddImport` is an additional mixed provider for a different reason: ordinary source-import leaves coexist with project, assembly and package-reference effects. It is not another `CodeActionWithOptions` family.

### Internal and external dependencies are narrow

`AddMissingImports` is the only composed provider classified as internal-service dependent. It requires paste-tracking state designed for an editor paste operation. Rename Tracking has a similar editor-state dependency, but its provider is source-only EditorFeatures code and is not part of the Features assemblies composed by Workbench.

The seven product-boundary exclusions are `AddMissingReference`, `AddPackage`, `EnableNullable`, `UpdateProjectToAllowUnsafe`, `UpgradeProject` and the two Copilot-backed providers. They require project/package mutation, external intelligence or another effect outside the source-document transaction contract.

### Ordinary replay is the dominant architecture

The remaining 231 composed providers produce ordinary actions compatible with opaque reference replay. Their individual availability during the analysis varied because some diagnostics were not activated or compatibility evidence was not complete, but those are discovery and validation prerequisites rather than different execution architectures.

This is the evidence behind replacing the positive supported-provider ledger with allow-by-default ordinary replay, a small exclusion policy, generic structural checks and final Workspace mutation validation.

## Ordinary replay

| Kind | Provider | Availability |
| --- | --- | --- |
| Refactoring | `Microsoft.CodeAnalysis.AddConstructorParametersFromMembers.AddConstructorParametersFromMembersCodeRefactoringProvider` | Supported as `add-constructor-parameters` |
| Code fix | `Microsoft.CodeAnalysis.AddRequiredParentheses.AddRequiredParenthesesCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CodeFixes.NamingStyles.NamingStyleCodeFixProvider` | Built-in diagnostic activation required |
| Refactoring | `Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider` | Supported as `extract-method` |
| Refactoring | `Microsoft.CodeAnalysis.CodeRefactorings.MoveType.MoveTypeCodeRefactoringProvider` | Supported as `move-type-to-file` |
| Code fix | `Microsoft.CodeAnalysis.CodeStyle.CSharpFormattingCodeFixProvider` | Built-in diagnostic activation required |
| Refactoring | `Microsoft.CodeAnalysis.ConvertToInterpolatedString.ConvertRegularStringToInterpolatedStringRefactoringProvider` | Supported as `convert-to-interpolated-string` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.AddAnonymousTypeMemberName.CSharpAddAnonymousTypeMemberNameCodeFixProvider` | Supported as `add-anonymous-type-member-name` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.AddDebuggerDisplay.CSharpAddDebuggerDisplayCodeRefactoringProvider` | Supported as `add-debugger-display` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.AddFileBanner.CSharpAddFileBannerCodeRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider` | Supported as `add-import` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.AddObsoleteAttribute.CSharpAddObsoleteAttributeCodeFixProvider` | Supported as `add-obsolete-attribute` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.AddOrRemoveAccessibilityModifiers.CSharpAddOrRemoveAccessibilityModifiersCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.AddParameter.CSharpAddParameterCodeFixProvider` | Pending replay validation |
| Code fix | `Microsoft.CodeAnalysis.CSharp.AliasAmbiguousType.CSharpAliasAmbiguousTypeCodeFixProvider` | Pending replay validation |
| Code fix | `Microsoft.CodeAnalysis.CSharp.AssignOutParameters.AssignOutParametersAboveReturnCodeFixProvider` | Supported as `assign-out-parameters` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.AssignOutParameters.AssignOutParametersAtStartCodeFixProvider` | Supported as `assign-out-parameters` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.AddExplicitCast.CSharpAddExplicitCastCodeFixProvider` | Supported as `add-explicit-cast` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.AddInheritdoc.AddInheritdocCodeFixProvider` | Supported as `add-inheritdoc` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.ConvertToAsync.CSharpConvertToAsyncMethodCodeFixProvider` | Pending replay validation |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.DeclareAsNullable.CSharpDeclareAsNullableCodeFixProvider` | Supported as `declare-as-nullable` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.FixIncorrectConstraint.CSharpFixIncorrectConstraintCodeFixProvider` | Supported as `fix-incorrect-constraint` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.FixReturnType.CSharpFixReturnTypeCodeFixProvider` | Supported as `fix-return-type` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.ForEachCast.CSharpForEachCastCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.FullyQualify.CSharpFullyQualifyCodeFixProvider` | Supported |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.GenerateDeconstructMethod.GenerateDeconstructMethodCodeFixProvider` | Pending replay validation |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.GenerateEnumMember.GenerateEnumMemberCodeFixProvider` | Pending replay validation |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.GenerateMethod.GenerateConversionCodeFixProvider` | Pending replay validation |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.GenerateMethod.GenerateMethodCodeFixProvider` | Pending replay validation |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.HiddenExplicitCast.CSharpHiddenExplicitCastCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.HideBase.HideBaseCodeFixProvider` | Supported as `hide-base-member` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpAddYieldCodeFixProvider` | Supported as `add-yield` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpChangeToIEnumerableCodeFixProvider` | Supported as `change-iterator-return-type` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.MakeMemberRequired.CSharpMakeMemberRequiredCodeFixProvider` | Supported as `make-member-required` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.MakeStatementAsynchronous.CSharpMakeStatementAsynchronousCodeFixProvider` | Supported as `make-statement-asynchronous` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.MatchFolderAndNamespace.CSharpChangeNamespaceToMatchFolderCodeFixProvider` | Covered by dedicated tool |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.RemoveNewModifier.RemoveNewModifierCodeFixProvider` | Supported as `remove-new-modifier` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.RemoveUnnecessaryNullableDirective.CSharpRemoveUnnecessaryNullableDirectiveCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.TransposeRecordKeyword.CSharpTransposeRecordKeywordCodeFixProvider` | Supported as `transpose-record-keyword` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.UseNameofInAttribute.CSharpUseNameofInAttributeCodeFixProvider` | Built-in diagnostic activation required |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider` | Supported as `add-await` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ConvertLocalFunctionToMethod.CSharpConvertLocalFunctionToMethodCodeRefactoringProvider` | Supported as `convert-local-function-to-method` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineMethod.CSharpInlineMethodRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineTemporary.CSharpInlineTemporaryCodeRefactoringProvider` | Supported as `inline-variable` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.SyncNamespace.CSharpSyncNamespaceCodeRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseExplicitType.UseExplicitTypeCodeRefactoringProvider` | Supported as `use-explicit-type` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseImplicitType.UseImplicitTypeCodeRefactoringProvider` | Supported as `use-implicit-type` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseRecursivePatterns.UseRecursivePatternsCodeRefactoringProvider` | Supported as `use-recursive-patterns` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.ConditionalExpressionInStringInterpolation.CSharpAddParenthesesAroundConditionalExpressionInInterpolatedStringCodeFixProvider` | Supported as `add-conditional-interpolation-parentheses` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.ConflictMarkerResolution.CSharpResolveConflictMarkerCodeFixProvider` | Pending replay validation |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider` | Supported as `convert-anonymous-type-to-class` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToTupleCodeRefactoringProvider` | Supported as `convert-anonymous-type-to-tuple` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider` | Supported as `convert-auto-property-to-full-property` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimInterpolatedStringCodeRefactoringProvider` | Supported as `convert-between-regular-and-verbatim-interpolated-string` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimStringCodeRefactoringProvider` | Supported as `convert-between-regular-and-verbatim-string` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertDirectCastToTryCastCodeRefactoringProvider` | Supported as `convert-direct-cast-to-try-cast` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertTryCastToDirectCastCodeRefactoringProvider` | Supported as `convert-try-cast-to-direct-cast` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertForEachToFor.CSharpConvertForEachToForCodeRefactoringProvider` | Supported as `convert-foreach-to-for` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertForToForEach.CSharpConvertForToForEachCodeRefactoringProvider` | Supported as `convert-for-to-foreach` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertIfToSwitch.CSharpConvertIfToSwitchCodeRefactoringProvider` | Supported as `convert-if-to-switch` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider` | Supported as `convert-foreach-linq` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertLinq.CSharpConvertLinqQueryToForEachProvider` | Supported as `convert-foreach-linq` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.ConvertNamespace.ConvertNamespaceCodeFixProvider` | Covered by dedicated tool |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertNamespace.ConvertNamespaceCodeRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertNumericLiteral.CSharpConvertNumericLiteralCodeRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertPrimaryToRegularConstructor.ConvertPrimaryToRegularConstructorCodeRefactoringProvider` | Supported as `convert-primary-to-regular-constructor` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToProgramMainCodeFixProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToProgramMainCodeRefactoringProvider` | Supported |
| Code fix | `Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeFixProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeRefactoringProvider` | Supported |
| Code fix | `Microsoft.CodeAnalysis.CSharp.ConvertSwitchStatementToExpression.ConvertSwitchStatementToExpressionCodeFixProvider` | Built-in diagnostic activation required |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertToExtension.ConvertToExtensionCodeRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertToInterpolatedString.CSharpConvertConcatenationToInterpolatedStringRefactoringProvider` | Supported as `convert-to-interpolated-string` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertToInterpolatedString.CSharpConvertPlaceholderToInterpolatedStringRefactoringProvider` | Supported as `convert-to-interpolated-string` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertToRawString.ConvertStringToRawStringCodeRefactoringProvider` | Supported |
| Code fix | `Microsoft.CodeAnalysis.CSharp.ConvertToRecord.CSharpConvertToRecordCodeFixProvider` | Covered by dedicated tool |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertToRecord.CSharpConvertToRecordRefactoringProvider` | Supported as `convert-to-record` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ConvertTupleToStruct.CSharpConvertTupleToStructCodeRefactoringProvider` | Supported |
| Code fix | `Microsoft.CodeAnalysis.CSharp.ConvertTypeOfToNameOf.CSharpConvertTypeOfToNameOfCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.Diagnostics.AddBraces.CSharpAddBracesCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.DisambiguateSameVariable.CSharpDisambiguateSameVariableCodeFixProvider` | Supported as `disambiguate-same-variable` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.DocumentationComments.CSharpAddDocCommentNodesCodeFixProvider` | Supported as `add-documentation-comment-nodes` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.DocumentationComments.CSharpRemoveDocCommentNodeCodeFixProvider` | Supported as `remove-documentation-comment-node` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.EmbeddedLanguages.CSharpJsonDetectionCodeFixProvider` | Dedicated implementation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.FileHeaders.CSharpFileHeaderCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.GenerateConstructor.GenerateConstructorCodeFixProvider` | Pending replay validation |
| Code fix | `Microsoft.CodeAnalysis.CSharp.GenerateDefaultConstructors.CSharpGenerateDefaultConstructorsCodeFixProvider` | Pending replay validation |
| Code fix | `Microsoft.CodeAnalysis.CSharp.GenerateVariable.CSharpGenerateVariableCodeFixProvider` | Pending replay validation |
| Code fix | `Microsoft.CodeAnalysis.CSharp.ImplementAbstractClass.CSharpImplementAbstractClassCodeFixProvider` | Pending replay validation |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementExplicitlyCodeRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementImplicitlyCodeRefactoringProvider` | Supported |
| Code fix | `Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementInterfaceCodeFixProvider` | Pending replay validation |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpAddParameterCheckCodeRefactoringProvider` | Supported as `add-null-checks` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpInitializeMemberFromParameterCodeRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpInitializeMemberFromPrimaryConstructorParameterCodeRefactoringProvider` | Supported |
| Code fix | `Microsoft.CodeAnalysis.CSharp.InlineDeclaration.CSharpInlineDeclarationCodeFixProvider` | Built-in diagnostic activation required |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.IntroduceParameter.CSharpIntroduceParameterCodeRefactoringProvider` | Supported as `introduce-parameter` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.IntroduceUsingStatement.CSharpIntroduceUsingStatementCodeRefactoringProvider` | Supported as `introduce-using-statement` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.IntroduceVariable.CSharpIntroduceLocalForExpressionCodeRefactoringProvider` | Supported as `introduce-variable` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.InvertConditional.CSharpInvertConditionalCodeRefactoringProvider` | Supported as `invert-conditional` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.InvertIf.CSharpInvertIfCodeRefactoringProvider` | Supported as `invert-if` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.InvertLogical.CSharpInvertLogicalCodeRefactoringProvider` | Supported as `invert-logical` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.InvokeDelegateWithConditionalAccess.InvokeDelegateWithConditionalAccessCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeAnonymousFunctionStatic.CSharpMakeAnonymousFunctionStaticCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeFieldReadonly.CSharpMakeFieldReadonlyCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeLocalFunctionStatic.MakeLocalFunctionStaticCodeFixProvider` | Covered by dedicated tool |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.MakeLocalFunctionStatic.MakeLocalFunctionStaticCodeRefactoringProvider` | Supported as `make-local-function-static` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeLocalFunctionStatic.PassInCapturedVariablesAsArgumentsCodeFixProvider` | Supported as `pass-captured-variables-as-arguments` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeMemberStatic.CSharpMakeMemberStaticCodeFixProvider` | Supported as `make-member-static` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeMethodAsynchronous.CSharpMakeMethodAsynchronousCodeFixProvider` | Supported as `make-method-asynchronous` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeMethodSynchronous.CSharpMakeMethodSynchronousCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeRefStruct.MakeRefStructCodeFixProvider` | Supported as `make-ref-struct` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeStructFieldsWritable.CSharpMakeStructFieldsWritableCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeStructMemberReadOnly.CSharpMakeStructMemberReadOnlyCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeStructReadOnly.CSharpMakeStructReadOnlyCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeTypeAbstract.CSharpMakeTypeAbstractCodeFixProvider` | Supported as `make-type-abstract` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MakeTypePartial.CSharpMakeTypePartialCodeFixProvider` | Supported as `make-type-partial` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.MisplacedUsingDirectives.MisplacedUsingDirectivesCodeFixProvider` | Built-in diagnostic activation required |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.MoveDeclarationNearReference.CSharpMoveDeclarationNearReferenceCodeRefactoringProvider` | Supported as `move-declaration-near-reference` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.NameTupleElement.CSharpNameTupleElementCodeRefactoringProvider` | Supported as `name-tuple-element` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.NewLines.ArrowExpressionClausePlacement.ArrowExpressionClausePlacementCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.NewLines.ConditionalExpressionPlacement.ConditionalExpressionPlacementCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.NewLines.ConsecutiveBracePlacement.ConsecutiveBracePlacementCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.NewLines.ConstructorInitializerPlacement.ConstructorInitializerPlacementCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.NewLines.EmbeddedStatementPlacement.EmbeddedStatementPlacementCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.OrderModifiers.CSharpOrderModifiersCodeFixProvider` | Supported as `order-modifiers` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.PopulateSwitch.CSharpPopulateSwitchExpressionCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.PopulateSwitch.CSharpPopulateSwitchStatementCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.QualifyMemberAccess.CSharpQualifyMemberAccessCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveAsyncModifier.CSharpRemoveAsyncModifierCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveConfusingSuppression.CSharpRemoveConfusingSuppressionCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveInKeyword.RemoveInKeywordCodeFixProvider` | Supported as `remove-in-keyword` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnnecessaryCast.CSharpRemoveUnnecessaryCastCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnnecessaryDiscardDesignation.CSharpRemoveUnnecessaryDiscardDesignationCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnnecessaryImports.CSharpRemoveUnnecessaryImportsCodeFixProvider` | Supported as `remove-unused-usings` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnnecessaryLambdaExpression.CSharpRemoveUnnecessaryLambdaExpressionCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnnecessaryParentheses.CSharpRemoveUnnecessaryParenthesesCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnnecessarySuppressions.CSharpRemoveUnnecessaryNullableWarningSuppressionsCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnnecessaryUnsafeModifier.CSharpRemoveUnnecessaryUnsafeModifierCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnreachableCode.CSharpRemoveUnreachableCodeCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnusedLocalFunction.CSharpRemoveUnusedLocalFunctionCodeFixProvider` | Supported as `remove-unused-local-function` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnusedMembers.CSharpRemoveUnusedMembersCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnusedParametersAndValues.CSharpRemoveUnusedValuesCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.RemoveUnusedVariable.CSharpRemoveUnusedVariableCodeFixProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ReplaceConditionalWithStatements.CSharpReplaceConditionalWithStatementsCodeRefactoringProvider` | Supported as `replace-conditional-with-statements` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.ReplaceDefaultLiteral.CSharpReplaceDefaultLiteralCodeFixProvider` | Supported as `replace-default-literal` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ReplaceDocCommentTextWithTag.CSharpReplaceDocCommentTextWithTagCodeRefactoringProvider` | Supported as `replace-doc-comment-text-with-tag` |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.ReverseForStatement.CSharpReverseForStatementCodeRefactoringProvider` | Supported as `reverse-for-statement` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.SimplifyInterpolation.CSharpSimplifyInterpolationCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.SimplifyLinqExpression.CSharpSimplifyLinqTypeCheckAndCastCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.SimplifyPropertyAccessor.CSharpSimplifyPropertyAccessorCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.SimplifyPropertyPattern.CSharpSimplifyPropertyPatternCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.SimplifyThisOrMe.CSharpSimplifyThisOrMeCodeFixProvider` | Dedicated implementation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.SimplifyTypeNames.SimplifyTypeNamesCodeFixProvider` | Dedicated implementation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.SpellCheck.CSharpSpellCheckCodeFixProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpMergeConsecutiveIfStatementsCodeRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpMergeNestedIfStatementsCodeRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpSplitIntoConsecutiveIfStatementsCodeRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpSplitIntoNestedIfStatementsCodeRefactoringProvider` | Supported |
| Code fix | `Microsoft.CodeAnalysis.CSharp.TypeStyle.UseExplicitTypeCodeFixProvider` | Covered by dedicated tool |
| Code fix | `Microsoft.CodeAnalysis.CSharp.TypeStyle.UseImplicitTypeCodeFixProvider` | Covered by dedicated tool |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UnsealClass.CSharpUnsealClassCodeFixProvider` | Supported as `unseal-class` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider` | Supported as `convert-property` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseCoalesceExpression.CSharpUseCoalesceExpressionForIfNullStatementCheckCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseCollectionExpression.CSharpUseCollectionExpressionForArrayCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseCollectionExpression.CSharpUseCollectionExpressionForBuilderCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseCollectionExpression.CSharpUseCollectionExpressionForCreateCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseCollectionExpression.CSharpUseCollectionExpressionForEmptyCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseCollectionExpression.CSharpUseCollectionExpressionForFluentCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseCollectionExpression.CSharpUseCollectionExpressionForNewCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseCollectionExpression.CSharpUseCollectionExpressionForStackAllocCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseCollectionInitializer.CSharpUseCollectionInitializerCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseCompoundAssignment.CSharpUseCompoundAssignmentCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseCompoundAssignment.CSharpUseCompoundCoalesceAssignmentCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseConditionalExpression.CSharpUseConditionalExpressionForAssignmentCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseConditionalExpression.CSharpUseConditionalExpressionForReturnCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseDeconstruction.CSharpUseDeconstructionCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseDefaultLiteral.CSharpUseDefaultLiteralCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseExplicitArrayInExpressionTree.CSharpUseExplicitArrayInExpressionTreeCodeFixProvider` | Supported as `use-explicit-array-in-expression-tree` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseExplicitTypeForConst.UseExplicitTypeForConstCodeFixProvider` | Supported as `use-explicit-type-for-const` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeFixProvider` | Covered by dedicated tool |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider` | Supported as `convert-expression-body` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeFixProvider` | Covered by dedicated tool |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider` | Supported as `convert-expression-body` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseImplicitlyTypedLambdaExpression.CSharpUseImplicitlyTypedLambdaExpressionCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseImplicitObjectCreation.CSharpUseImplicitObjectCreationCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseIndexOrRangeOperator.CSharpUseIndexOperatorCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseIndexOrRangeOperator.CSharpUseRangeOperatorCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseInferredMemberName.CSharpUseInferredMemberNameCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseInterpolatedVerbatimString.CSharpUseInterpolatedVerbatimStringCodeFixProvider` | Supported as `use-interpolated-verbatim-string` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseIsNullCheck.CSharpUseIsNullCheckForCastAndEqualityOperatorCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseIsNullCheck.CSharpUseIsNullCheckForReferenceEqualsCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseIsNullCheck.CSharpUseNullCheckOverTypeCheckCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseLocalFunction.CSharpUseLocalFunctionCodeFixProvider` | Built-in diagnostic activation required |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider` | Supported as `use-named-arguments` |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseNullPropagation.CSharpUseNullPropagationCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseObjectInitializer.CSharpUseObjectInitializerCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UsePatternCombinators.CSharpUsePatternCombinatorsCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UsePatternMatching.CSharpAsAndMemberAccessCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UsePatternMatching.CSharpAsAndNullCheckCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UsePatternMatching.CSharpIsAndCastCheckCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UsePatternMatching.CSharpIsAndCastCheckWithoutNameCodeFixProvider` | Dedicated implementation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UsePatternMatching.CSharpUseNotPatternCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UsePrimaryConstructor.CSharpUsePrimaryConstructorCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseSimpleUsingStatement.UseSimpleUsingStatementCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseSystemThreadingLock.CSharpUseSystemThreadingLockCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseThrowExpression.UseThrowExpressionCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseTupleSwap.CSharpUseTupleSwapCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseUnboundGenericTypeInNameOf.CSharpUseUnboundGenericTypeInNameOfCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UseUtf8StringLiteral.UseUtf8StringLiteralCodeFixProvider` | Built-in diagnostic activation required |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.Wrapping.CSharpWrappingCodeRefactoringProvider` | Supported |
| Refactoring | `Microsoft.CodeAnalysis.EncapsulateField.EncapsulateFieldRefactoringProvider` | Supported as `encapsulate-field` |
| Refactoring | `Microsoft.CodeAnalysis.GenerateComparisonOperators.GenerateComparisonOperatorsCodeRefactoringProvider` | Supported as `generate-comparison-operators` |
| Refactoring | `Microsoft.CodeAnalysis.ImplementInterface.ImplementInterfaceCodeRefactoringProvider` | Supported as `implement-interface` |
| Refactoring | `Microsoft.CodeAnalysis.IntroduceVariable.IntroduceVariableCodeRefactoringProvider` | Supported as `introduce-variable` |
| Code fix | `Microsoft.CodeAnalysis.NewLines.ConsecutiveStatementPlacement.ConsecutiveStatementPlacementCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.NewLines.MultipleBlankLines.MultipleBlankLinesCodeFixProvider` | Built-in diagnostic activation required |
| Refactoring | `Microsoft.CodeAnalysis.OrganizeImports.OrganizeImportsCodeRefactoringProvider` | Supported as `organize-imports` |
| Code fix | `Microsoft.CodeAnalysis.PreferFrameworkType.PreferFrameworkTypeCodeFixProvider` | Dedicated implementation required |
| Code fix | `Microsoft.CodeAnalysis.RemoveRedundantEquality.RemoveRedundantEqualityCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.RemoveUnnecessarySuppressions.RemoveUnnecessaryAttributeSuppressionsCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.RemoveUnnecessarySuppressions.RemoveUnnecessaryInlineSuppressionsCodeFixProvider` | Built-in diagnostic activation required |
| Refactoring | `Microsoft.CodeAnalysis.ReplaceMethodWithProperty.ReplaceMethodWithPropertyCodeRefactoringProvider` | Supported as `replace-method-with-property` |
| Refactoring | `Microsoft.CodeAnalysis.ReplacePropertyWithMethods.ReplacePropertyWithMethodsCodeRefactoringProvider` | Supported as `replace-property-with-methods` |
| Code fix | `Microsoft.CodeAnalysis.SimplifyBooleanExpression.SimplifyConditionalCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.SimplifyLinqExpression.SimplifyLinqExpressionCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.UpdateLegacySuppressions.UpdateLegacySuppressionsCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.UseCoalesceExpression.UseCoalesceExpressionForNullableTernaryConditionalCheckCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.UseCoalesceExpression.UseCoalesceExpressionForTernaryConditionalCheckCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.UseExplicitTupleName.UseExplicitTupleNameCodeFixProvider` | Built-in diagnostic activation required |
| Code fix | `Microsoft.CodeAnalysis.UseSystemHashCode.UseSystemHashCodeCodeFixProvider` | Built-in diagnostic activation required |

## Mixed provider

| Kind | Provider | Availability | Assessment |
| --- | --- | --- | --- |
| Code fix | `Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider` | Supported as `add-missing-usings` | Ordinary source-import leaves coexist with project, assembly and package-reference leaves. Workbench must retain only source-transaction-safe leaves. |
| Code fix | `Microsoft.CodeAnalysis.CSharp.CodeFixes.GenerateType.GenerateTypeCodeFixProvider` | Action-level classification required | Offers ordinary deterministic placement leaves and a configurable `CodeActionWithOptions` leaf. |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.PullMemberUp.CSharpPullMemberUpCodeRefactoringProvider` | Dedicated implementation required | Source contains an option-backed member/destination route; any safe ordinary leaves require action-level validation. |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.GenerateConstructors.CSharpGenerateConstructorsCodeRefactoringProvider` | Action-level classification required | Selected-member and missing-base-constructor routes are ordinary; the caret-only member-picker route is option-backed. |
| Refactoring | `Microsoft.CodeAnalysis.GenerateEqualsAndGetHashCodeFromMembers.GenerateEqualsAndGetHashCodeFromMembersCodeRefactoringProvider` | Action-level classification required | Selected-member generation is ordinary; the caret-only member-picker route is option-backed. |

## Option-backed only

| Kind | Provider | Availability | Assessment |
| --- | --- | --- | --- |
| Refactoring | `Microsoft.CodeAnalysis.ChangeSignature.ChangeSignatureCodeRefactoringProvider` | Dedicated implementation required | The provider exposes only an options-backed signature editor. |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ExtractClass.CSharpExtractClassCodeRefactoringProvider` | Dedicated implementation required | The provider requires an options-backed member-selection workflow. |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveStaticMembers.CSharpMoveStaticMembersRefactoringProvider` | Dedicated implementation required | The provider requires an options-backed destination and member-selection workflow. |
| Refactoring | `Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider` | Dedicated implementation required | The provider requires extraction options and selected members. |
| Refactoring | `Microsoft.CodeAnalysis.GenerateOverrides.GenerateOverridesCodeRefactoringProvider` | Dedicated implementation required | The provider exposes only an options-backed member picker. |
| Refactoring | `Microsoft.CodeAnalysis.MoveToNamespace.MoveToNamespaceCodeActionProvider` | Dedicated implementation required | The provider requires an options-backed target namespace. |

## Internal-service dependent

| Kind | Provider | Availability | Assessment |
| --- | --- | --- | --- |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddMissingImports.CSharpAddMissingImportsRefactoringProvider` | Covered by dedicated tool | Depends on editor paste-tracking state. The dedicated Workbench import workflow is the appropriate headless route. |

## Product-boundary exclusion

| Kind | Provider | Availability | Assessment |
| --- | --- | --- | --- |
| Code fix | `Microsoft.CodeAnalysis.CSharp.AddMissingReference.CSharpAddMissingReferenceCodeFixProvider` | Excluded | Mutates project references rather than staged source documents. |
| Code fix | `Microsoft.CodeAnalysis.CSharp.AddPackage.CSharpAddSpecificPackageCodeFixProvider` | Excluded | Mutates package/project state and may require network and package-source policy. |
| Code fix | `Microsoft.CodeAnalysis.CSharp.Copilot.CSharpCopilotCodeFixProvider` | Excluded | Depends on an external Copilot service and editor options. |
| Code fix | `Microsoft.CodeAnalysis.CSharp.Copilot.CSharpImplementNotImplementedExceptionFixProvider` | Excluded | Depends on an external Copilot service and editor options. |
| Refactoring | `Microsoft.CodeAnalysis.CSharp.CodeRefactorings.EnableNullable.EnableNullableCodeRefactoringProvider` | Excluded | Mutates project compilation settings rather than staged source documents. |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UpdateProjectToAllowUnsafe.CSharpUpdateProjectToAllowUnsafeCodeFixProvider` | Excluded | Mutates project compilation settings rather than staged source documents. |
| Code fix | `Microsoft.CodeAnalysis.CSharp.UpgradeProject.CSharpUpgradeProjectCodeFixProvider` | Excluded | Mutates the project language version rather than staged source documents. |

## Custom semantic implementation

No provider in the composed Roslyn 5.6 runtime has this category. Any future entry would be a Workbench-owned replacement rather than direct replay of a composed provider.

## Source-Only Editor Provider

`Microsoft.CodeAnalysis.CSharp.RenameTracking.CSharpRenameTrackingCodeRefactoringProvider` exists in Roslyn EditorFeatures source but is not present in the Features assemblies composed by Workbench. It depends on editor text-buffer change tracking and undo history. It is therefore an internal-service-dependent editor provider outside the 250-provider runtime total; the explicit `rename-symbol` tool is the supported headless operation.

## Interpretation

Ordinary replay means the provider architecture is compatible with the existing Workbench replay pipeline; it does not automatically mean every diagnostic route or action leaf has been validated. Promotion still requires the provider's diagnostics to be obtainable, every advertised diagnostic route to have compatibility evidence, deterministic disambiguation where multiple actions can coexist, supported `ApplyChangesOperation` output and published-host staging, preview and rollback coverage.

Mixed providers require action-level classification. Workbench must never make a provider visible merely because one leaf is safe when another leaf opens an options workflow or performs a non-document effect.

Option-backed providers are not waiting for a general-purpose decoder. Supporting one requires an explicit Workbench-owned request model and semantic implementation. That implementation would be classified as custom semantic implementation and must receive architecture review because Workbench would own correctness that Roslyn currently supplies through internal IDE services.

For the replacement architecture, migration order and validation requirements, see [Code Action Architecture Plan — 2026-07-27](CodeActionArchitecturePlan-2026-07-27.md).

