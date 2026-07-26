namespace Roslyn.Workbench.Mcp.CodeActions.Catalog;

internal static class BuiltInCodeActionLedger
{
    private static readonly IReadOnlyList<BuiltInCodeActionFamily> _families =
    [
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider",
            ToolName = "extract-method",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.IntroduceParameter.CSharpIntroduceParameterCodeRefactoringProvider",
            ToolName = "introduce-parameter",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineTemporary.CSharpInlineTemporaryCodeRefactoringProvider",
            ToolName = "inline-variable",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.EncapsulateField.EncapsulateFieldRefactoringProvider",
            ToolName = "encapsulate-field",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider",
            ToolName = "convert-foreach-linq",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertLinq.CSharpConvertLinqQueryToForEachProvider",
            ToolName = "convert-foreach-linq",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.IntroduceVariable.IntroduceVariableCodeRefactoringProvider",
            ToolName = "introduce-variable",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.IntroduceVariable.CSharpIntroduceLocalForExpressionCodeRefactoringProvider",
            ToolName = "introduce-variable",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.ConvertToInterpolatedString.ConvertRegularStringToInterpolatedStringRefactoringProvider",
            ToolName = "convert-to-interpolated-string",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToInterpolatedString.CSharpConvertConcatenationToInterpolatedStringRefactoringProvider",
            ToolName = "convert-to-interpolated-string",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToInterpolatedString.CSharpConvertPlaceholderToInterpolatedStringRefactoringProvider",
            ToolName = "convert-to-interpolated-string",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "add-missing-usings",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddAnonymousTypeMemberName.CSharpAddAnonymousTypeMemberNameCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "add-anonymous-type-member-name",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddExplicitCast.CSharpAddExplicitCastCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "add-explicit-cast",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddInheritdoc.AddInheritdocCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "add-inheritdoc",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddObsoleteAttribute.CSharpAddObsoleteAttributeCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "add-obsolete-attribute",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AssignOutParameters.AssignOutParametersAboveReturnCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "assign-out-parameters",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AssignOutParameters.AssignOutParametersAtStartCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "assign-out-parameters",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.DeclareAsNullable.CSharpDeclareAsNullableCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "declare-as-nullable",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FixIncorrectConstraint.CSharpFixIncorrectConstraintCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "fix-incorrect-constraint",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FixReturnType.CSharpFixReturnTypeCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "fix-return-type",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FullyQualify.CSharpFullyQualifyCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToProgramMainCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.RemoveNewModifier.RemoveNewModifierCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "remove-new-modifier",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConditionalExpressionInStringInterpolation.CSharpAddParenthesesAroundConditionalExpressionInInterpolatedStringCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "add-conditional-interpolation-parentheses",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.RemoveInKeyword.RemoveInKeywordCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "remove-in-keyword",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.RemoveUnusedVariable.CSharpRemoveUnusedVariableCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ReplaceDefaultLiteral.CSharpReplaceDefaultLiteralCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "replace-default-literal",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SpellCheck.CSharpSpellCheckCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseAutoProperty.CSharpUseAutoPropertyCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "convert-property",
            ExecutorTool = "convert-property",
            ExecutionMode = CodeActionExecutionMode.Parameterised,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseExplicitTypeForConst.UseExplicitTypeForConstCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "use-explicit-type-for-const",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.RemoveUnnecessaryImports.CSharpRemoveUnnecessaryImportsCodeFixProvider",
            Kind = BuiltInCodeActionFamilyKind.CodeFix,
            ToolName = "remove-unused-usings",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider",
            ToolName = "convert-anonymous-type-to-class",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToTupleCodeRefactoringProvider",
            ToolName = "convert-anonymous-type-to-tuple",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider",
            ToolName = "convert-auto-property-to-full-property",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertDirectCastToTryCastCodeRefactoringProvider",
            ToolName = "convert-direct-cast-to-try-cast",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ConvertLocalFunctionToMethod.CSharpConvertLocalFunctionToMethodCodeRefactoringProvider",
            ToolName = "convert-local-function-to-method",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertPrimaryToRegularConstructor.ConvertPrimaryToRegularConstructorCodeRefactoringProvider",
            ToolName = "convert-primary-to-regular-constructor",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToRecord.CSharpConvertToRecordRefactoringProvider",
            ToolName = "convert-to-record",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertTryCastToDirectCastCodeRefactoringProvider",
            ToolName = "convert-try-cast-to-direct-cast",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InvertConditional.CSharpInvertConditionalCodeRefactoringProvider",
            ToolName = "invert-conditional",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InvertIf.CSharpInvertIfCodeRefactoringProvider",
            ToolName = "invert-if",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MoveDeclarationNearReference.CSharpMoveDeclarationNearReferenceCodeRefactoringProvider",
            ToolName = "move-declaration-near-reference",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CodeRefactorings.MoveType.MoveTypeCodeRefactoringProvider",
            ToolName = "move-type-to-file",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.NameTupleElement.CSharpNameTupleElementCodeRefactoringProvider",
            ToolName = "name-tuple-element",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ReplaceDocCommentTextWithTag.CSharpReplaceDocCommentTextWithTagCodeRefactoringProvider",
            ToolName = "replace-doc-comment-text-with-tag",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddDebuggerDisplay.CSharpAddDebuggerDisplayCodeRefactoringProvider",
            ToolName = "add-debugger-display",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
            ToolName = "add-import",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
            ToolName = "add-await",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimInterpolatedStringCodeRefactoringProvider",
            ToolName = "convert-between-regular-and-verbatim-interpolated-string",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimStringCodeRefactoringProvider",
            ToolName = "convert-between-regular-and-verbatim-string",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertForEachToFor.CSharpConvertForEachToForCodeRefactoringProvider",
            ToolName = "convert-foreach-to-for",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertForToForEach.CSharpConvertForToForEachCodeRefactoringProvider",
            ToolName = "convert-for-to-foreach",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertIfToSwitch.CSharpConvertIfToSwitchCodeRefactoringProvider",
            ToolName = "convert-if-to-switch",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InvertLogical.CSharpInvertLogicalCodeRefactoringProvider",
            ToolName = "invert-logical",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.IntroduceUsingStatement.CSharpIntroduceUsingStatementCodeRefactoringProvider",
            ToolName = "introduce-using-statement",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeLocalFunctionStatic.MakeLocalFunctionStaticCodeRefactoringProvider",
            ToolName = "make-local-function-static",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ReplaceConditionalWithStatements.CSharpReplaceConditionalWithStatementsCodeRefactoringProvider",
            ToolName = "replace-conditional-with-statements",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ReverseForStatement.CSharpReverseForStatementCodeRefactoringProvider",
            ToolName = "reverse-for-statement",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseExplicitType.UseExplicitTypeCodeRefactoringProvider",
            ToolName = "use-explicit-type",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseImplicitType.UseImplicitTypeCodeRefactoringProvider",
            ToolName = "use-implicit-type",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
            ToolName = "use-named-arguments",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseRecursivePatterns.UseRecursivePatternsCodeRefactoringProvider",
            ToolName = "use-recursive-patterns",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertNumericLiteral.CSharpConvertNumericLiteralCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertTupleToStruct.CSharpConvertTupleToStructCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementExplicitlyCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementImplicitlyCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpInitializeMemberFromParameterCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineMethod.CSharpInlineMethodRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddFileBanner.CSharpAddFileBannerCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.EnableNullable.EnableNullableCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.SyncNamespace.CSharpSyncNamespaceCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertNamespace.ConvertNamespaceCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToProgramMainCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToExtension.ConvertToExtensionCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToRawString.ConvertStringToRawStringCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpAddParameterCheckCodeRefactoringProvider",
            ToolName = "add-null-checks",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpInitializeMemberFromPrimaryConstructorParameterCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.Wrapping.CSharpWrappingCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpMergeConsecutiveIfStatementsCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpMergeNestedIfStatementsCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpSplitIntoConsecutiveIfStatementsCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpSplitIntoNestedIfStatementsCodeRefactoringProvider",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            ToolName = "convert-expression-body",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            ToolName = "convert-expression-body",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.AddConstructorParametersFromMembers.AddConstructorParametersFromMembersCodeRefactoringProvider",
            ToolName = "add-constructor-parameters",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.GenerateComparisonOperators.GenerateComparisonOperatorsCodeRefactoringProvider",
            ToolName = "generate-comparison-operators",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.ImplementInterface.ImplementInterfaceCodeRefactoringProvider",
            ToolName = "implement-interface",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.OrganizeImports.OrganizeImportsCodeRefactoringProvider",
            ToolName = "organize-imports",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.ReplaceMethodWithProperty.ReplaceMethodWithPropertyCodeRefactoringProvider",
            ToolName = "replace-method-with-property",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.ReplacePropertyWithMethods.ReplacePropertyWithMethodsCodeRefactoringProvider",
            ToolName = "replace-property-with-methods",
            ExecutionMode = CodeActionExecutionMode.Replay,
        },
    ];

    public static IReadOnlyList<BuiltInCodeActionFamily> Families => _families;
}
