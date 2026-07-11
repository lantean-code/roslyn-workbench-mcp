using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Catalog;

internal static class BuiltInCodeActionLedger
{
    private static readonly IReadOnlyList<BuiltInCodeActionFamily> _families =
    [
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider",
            ToolName = "extract-method",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.IntroduceParameter.CSharpIntroduceParameterCodeRefactoringProvider",
            ToolName = "introduce-parameter",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineTemporary.CSharpInlineTemporaryCodeRefactoringProvider",
            ToolName = "inline-variable",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.EncapsulateField.EncapsulateFieldRefactoringProvider",
            ToolName = "encapsulate-field",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider",
            ToolName = "convert-foreach-linq",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertLinq.CSharpConvertLinqQueryToForEachProvider",
            ToolName = "convert-foreach-linq",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.IntroduceVariable.IntroduceVariableCodeRefactoringProvider",
            ToolName = "introduce-variable",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.IntroduceVariable.CSharpIntroduceLocalForExpressionCodeRefactoringProvider",
            ToolName = "introduce-variable",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.ConvertToInterpolatedString.ConvertRegularStringToInterpolatedStringRefactoringProvider",
            ToolName = "convert-to-interpolated-string",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToInterpolatedString.CSharpConvertConcatenationToInterpolatedStringRefactoringProvider",
            ToolName = "convert-to-interpolated-string",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToInterpolatedString.CSharpConvertPlaceholderToInterpolatedStringRefactoringProvider",
            ToolName = "convert-to-interpolated-string",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "add-missing-usings",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FullyQualify.CSharpFullyQualifyCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToProgramMainCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.RemoveUnusedVariable.CSharpRemoveUnusedVariableCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SpellCheck.CSharpSpellCheckCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "convert-property",
            ExecutorTool = "convert-property",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedParameterised,
        },
        new()
        {
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "remove-unused-usings",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider",
            ToolName = "convert-anonymous-type-to-class",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToTupleCodeRefactoringProvider",
            ToolName = "convert-anonymous-type-to-tuple",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider",
            ToolName = "convert-auto-property-to-full-property",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertDirectCastToTryCastCodeRefactoringProvider",
            ToolName = "convert-direct-cast-to-try-cast",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ConvertLocalFunctionToMethod.CSharpConvertLocalFunctionToMethodCodeRefactoringProvider",
            ToolName = "convert-local-function-to-method",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertPrimaryToRegularConstructor.ConvertPrimaryToRegularConstructorCodeRefactoringProvider",
            ToolName = "convert-primary-to-regular-constructor",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToRecord.CSharpConvertToRecordRefactoringProvider",
            ToolName = "convert-to-record",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertTryCastToDirectCastCodeRefactoringProvider",
            ToolName = "convert-try-cast-to-direct-cast",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InvertConditional.CSharpInvertConditionalCodeRefactoringProvider",
            ToolName = "invert-conditional",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InvertIf.CSharpInvertIfCodeRefactoringProvider",
            ToolName = "invert-if",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MoveDeclarationNearReference.CSharpMoveDeclarationNearReferenceCodeRefactoringProvider",
            ToolName = "move-declaration-near-reference",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CodeRefactorings.MoveType.MoveTypeCodeRefactoringProvider",
            ToolName = "move-type-to-file",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.NameTupleElement.CSharpNameTupleElementCodeRefactoringProvider",
            ToolName = "name-tuple-element",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ReplaceDocCommentTextWithTag.CSharpReplaceDocCommentTextWithTagCodeRefactoringProvider",
            ToolName = "replace-doc-comment-text-with-tag",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddDebuggerDisplay.CSharpAddDebuggerDisplayCodeRefactoringProvider",
            ToolName = "add-debugger-display",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
            ToolName = "add-import",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
            ToolName = "add-await",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimInterpolatedStringCodeRefactoringProvider",
            ToolName = "convert-between-regular-and-verbatim-interpolated-string",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimStringCodeRefactoringProvider",
            ToolName = "convert-between-regular-and-verbatim-string",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertForEachToFor.CSharpConvertForEachToForCodeRefactoringProvider",
            ToolName = "convert-foreach-to-for",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertForToForEach.CSharpConvertForToForEachCodeRefactoringProvider",
            ToolName = "convert-for-to-foreach",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertIfToSwitch.CSharpConvertIfToSwitchCodeRefactoringProvider",
            ToolName = "convert-if-to-switch",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InvertLogical.CSharpInvertLogicalCodeRefactoringProvider",
            ToolName = "invert-logical",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.IntroduceUsingStatement.CSharpIntroduceUsingStatementCodeRefactoringProvider",
            ToolName = "introduce-using-statement",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeLocalFunctionStatic.MakeLocalFunctionStaticCodeRefactoringProvider",
            ToolName = "make-local-function-static",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ReplaceConditionalWithStatements.CSharpReplaceConditionalWithStatementsCodeRefactoringProvider",
            ToolName = "replace-conditional-with-statements",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ReverseForStatement.CSharpReverseForStatementCodeRefactoringProvider",
            ToolName = "reverse-for-statement",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseExplicitType.UseExplicitTypeCodeRefactoringProvider",
            ToolName = "use-explicit-type",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseImplicitType.UseImplicitTypeCodeRefactoringProvider",
            ToolName = "use-implicit-type",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
            ToolName = "use-named-arguments",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseRecursivePatterns.UseRecursivePatternsCodeRefactoringProvider",
            ToolName = "use-recursive-patterns",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddMissingImports.CSharpAddMissingImportsRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertNumericLiteral.CSharpConvertNumericLiteralCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertTupleToStruct.CSharpConvertTupleToStructCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementExplicitlyCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementImplicitlyCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpInitializeMemberFromParameterCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineMethod.CSharpInlineMethodRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddFileBanner.CSharpAddFileBannerCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.EnableNullable.EnableNullableCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.SyncNamespace.CSharpSyncNamespaceCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertNamespace.ConvertNamespaceCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToProgramMainCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToExtension.ConvertToExtensionCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToRawString.ConvertStringToRawStringCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpAddParameterCheckCodeRefactoringProvider",
            ToolName = "add-null-checks",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpInitializeMemberFromPrimaryConstructorParameterCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.Wrapping.CSharpWrappingCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpMergeConsecutiveIfStatementsCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpMergeNestedIfStatementsCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpSplitIntoConsecutiveIfStatementsCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpSplitIntoNestedIfStatementsCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            ToolName = "convert-expression-body",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            ToolName = "convert-expression-body",
            AuditStatus = BuiltInCodeActionAuditStatus.ValidatedSupported,
            State = BuiltInCodeActionSupportState.SupportedReplay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.ChangeSignature.ChangeSignatureCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.ExtractInterface.ExtractInterfaceCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ExtractClass.CSharpExtractClassCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.GenerateConstructors.CSharpGenerateConstructorsCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.MoveStaticMembers.CSharpMoveStaticMembersRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.PullMemberUp.CSharpPullMemberUpCodeRefactoringProvider",
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddMissingReference.CSharpAddMissingReferenceCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddPackage.CSharpAddSpecificPackageCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.GenerateType.GenerateTypeCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.EmbeddedLanguages.CSharpJsonDetectionCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SimplifyThisOrMe.CSharpSimplifyThisOrMeCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SimplifyTypeNames.SimplifyTypeNamesCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UsePatternMatching.CSharpIsAndCastCheckWithoutNameCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.Copilot.CSharpCopilotCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.Copilot.CSharpImplementNotImplementedExceptionFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            AuditStatus = BuiltInCodeActionAuditStatus.ImpossibleUnderCurrentRules,
            HideReason = BuiltInCodeActionHideReason.ImpossibleUnderCurrentRules,
            State = BuiltInCodeActionSupportState.HiddenImpossibleUnderCurrentRules,
        },
    ];

    public static IReadOnlyList<BuiltInCodeActionFamily> Families => _families;

    public static bool IsDedicatedToolVisible(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        return _families.Any(family => family.IsDedicatedToolVisible && string.Equals(family.ToolName, toolName, StringComparison.Ordinal));
    }

    public static bool TryGetFamily(string providerId, [NotNullWhen(true)] out BuiltInCodeActionFamily? family)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        family = _families.FirstOrDefault(candidate => string.Equals(candidate.ProviderId, providerId, StringComparison.Ordinal));
        return family is not null;
    }
}
