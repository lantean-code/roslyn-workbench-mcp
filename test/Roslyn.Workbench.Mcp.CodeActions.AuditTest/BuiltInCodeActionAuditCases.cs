namespace Roslyn.Workbench.Mcp.CodeActions.Test;

internal enum BuiltInCodeActionRuntimeAuditOutcome
{
    NotOffered,
    OfferedButNotReplayable,
    OfferedAndReplayable,
}

internal enum BuiltInCodeActionAuditKind
{
    Refactoring,
    CodeFix,
}

internal sealed record BuiltInCodeActionAuditCase
{
    public string? ToolName { get; init; }

    public BuiltInCodeActionAuditKind Kind { get; init; } = BuiltInCodeActionAuditKind.Refactoring;

    public string ProviderId { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? TitlePrefix { get; init; }

    public string SourceNote { get; init; } = string.Empty;

    public IReadOnlyList<int> ActionPath { get; init; } = [];

    public string? ExpectedDiagnosticId { get; init; }

    public string? ExpectedChangedText { get; init; }

    public string? UnexpectedChangedText { get; init; }

    public BuiltInCodeActionRuntimeAuditOutcome ExpectedRuntimeOutcome { get; init; }

    public Func<InspectionSampleFixture> FixtureFactory { get; init; } = InspectionSampleFixture.Create;

    public Func<InspectionSampleFixture, LocationSelector> LocationFactory { get; init; } = static fixture => fixture.GetLocation("GreetingFormatter");
}

internal static class BuiltInCodeActionAuditCases
{
    private static readonly IReadOnlyList<BuiltInCodeActionAuditCase> _replayMetadata =
    [
        CreateReplayMetadata("extract-method", "Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider", "Extract method"),
        CreateReplayMetadata("introduce-parameter", "Microsoft.CodeAnalysis.CSharp.IntroduceParameter.CSharpIntroduceParameterCodeRefactoringProvider", "Introduce parameter for value"),
        CreateReplayMetadata("inline-variable", "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineTemporary.CSharpInlineTemporaryCodeRefactoringProvider", "Inline temporary variable"),
        CreateReplayMetadata("encapsulate-field", "Microsoft.CodeAnalysis.EncapsulateField.EncapsulateFieldRefactoringProvider", "Encapsulate field: _backingField"),
        CreateReplayMetadata("convert-foreach-linq", "Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider", "Convert to LINQ"),
        CreateReplayMetadata("convert-foreach-linq", "Microsoft.CodeAnalysis.CSharp.ConvertLinq.CSharpConvertLinqQueryToForEachProvider", "Convert to foreach"),
        CreateReplayMetadata("introduce-variable", "Microsoft.CodeAnalysis.IntroduceVariable.IntroduceVariableCodeRefactoringProvider", "Introduce local for '1 + 1'"),
        CreateReplayMetadata("introduce-variable", "Microsoft.CodeAnalysis.CSharp.IntroduceVariable.CSharpIntroduceLocalForExpressionCodeRefactoringProvider", "Introduce constant for '1 + 1'"),
        CreateReplayMetadata("convert-to-interpolated-string", "Microsoft.CodeAnalysis.ConvertToInterpolatedString.ConvertRegularStringToInterpolatedStringRefactoringProvider", "Convert to interpolated string"),
        CreateReplayMetadata("convert-to-interpolated-string", "Microsoft.CodeAnalysis.CSharp.ConvertToInterpolatedString.CSharpConvertConcatenationToInterpolatedStringRefactoringProvider", "Convert to interpolated string"),
        CreateReplayMetadata("convert-to-interpolated-string", "Microsoft.CodeAnalysis.CSharp.ConvertToInterpolatedString.CSharpConvertPlaceholderToInterpolatedStringRefactoringProvider", "Convert to interpolated string"),
        CreateReplayMetadata("add-debugger-display", "Microsoft.CodeAnalysis.CSharp.AddDebuggerDisplay.CSharpAddDebuggerDisplayCodeRefactoringProvider", "Add 'DebuggerDisplay' attribute"),
        CreateReplayMetadata("add-import", "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider", "Add 'using System.Net.Http;'"),
        CreateReplayMetadata("convert-anonymous-type-to-class", "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider", "Convert to class"),
        CreateReplayMetadata("add-await", "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider", "Add 'await'"),
        CreateReplayMetadata("convert-between-regular-and-verbatim-interpolated-string", "Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimInterpolatedStringCodeRefactoringProvider", "Convert to verbatim string"),
        CreateReplayMetadata("convert-between-regular-and-verbatim-string", "Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimStringCodeRefactoringProvider", "Convert to verbatim string"),
        CreateReplayMetadata("convert-direct-cast-to-try-cast", "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertDirectCastToTryCastCodeRefactoringProvider", "Change to 'as' expression"),
        CreateReplayMetadata("convert-foreach-to-for", "Microsoft.CodeAnalysis.CSharp.ConvertForEachToFor.CSharpConvertForEachToForCodeRefactoringProvider", "Convert to 'for'"),
        CreateReplayMetadata("convert-for-to-foreach", "Microsoft.CodeAnalysis.CSharp.ConvertForToForEach.CSharpConvertForToForEachCodeRefactoringProvider", "Convert to 'foreach'"),
        CreateReplayMetadata("convert-if-to-switch", "Microsoft.CodeAnalysis.CSharp.ConvertIfToSwitch.CSharpConvertIfToSwitchCodeRefactoringProvider", "Convert to 'switch' statement"),
        CreateReplayMetadata("convert-local-function-to-method", "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ConvertLocalFunctionToMethod.CSharpConvertLocalFunctionToMethodCodeRefactoringProvider", "Convert to method"),
        CreateReplayMetadata("convert-primary-to-regular-constructor", "Microsoft.CodeAnalysis.CSharp.ConvertPrimaryToRegularConstructor.ConvertPrimaryToRegularConstructorCodeRefactoringProvider", "Convert to regular constructor"),
        CreateReplayMetadata("convert-try-cast-to-direct-cast", "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertTryCastToDirectCastCodeRefactoringProvider", "Change to cast"),
        CreateReplayMetadata("invert-conditional", "Microsoft.CodeAnalysis.CSharp.InvertConditional.CSharpInvertConditionalCodeRefactoringProvider", "Invert conditional"),
        CreateReplayMetadata("invert-if", "Microsoft.CodeAnalysis.CSharp.InvertIf.CSharpInvertIfCodeRefactoringProvider", "Invert if"),
        CreateReplayMetadata("invert-logical", "Microsoft.CodeAnalysis.CSharp.InvertLogical.CSharpInvertLogicalCodeRefactoringProvider", "Replace '&&' with '||' "),
        CreateReplayMetadata("introduce-using-statement", "Microsoft.CodeAnalysis.CSharp.IntroduceUsingStatement.CSharpIntroduceUsingStatementCodeRefactoringProvider", "Introduce 'using' statement"),
        CreateReplayMetadata("make-local-function-static", "Microsoft.CodeAnalysis.CSharp.MakeLocalFunctionStatic.MakeLocalFunctionStaticCodeRefactoringProvider", "Make local function static"),
        CreateReplayMetadata("move-declaration-near-reference", "Microsoft.CodeAnalysis.CSharp.MoveDeclarationNearReference.CSharpMoveDeclarationNearReferenceCodeRefactoringProvider", "Move declaration near reference"),
        CreateReplayMetadata("name-tuple-element", "Microsoft.CodeAnalysis.CSharp.NameTupleElement.CSharpNameTupleElementCodeRefactoringProvider", "Add tuple element name 'Sum'"),
        CreateReplayMetadata("replace-conditional-with-statements", "Microsoft.CodeAnalysis.CSharp.ReplaceConditionalWithStatements.CSharpReplaceConditionalWithStatementsCodeRefactoringProvider", "Replace conditional expression with statements"),
        CreateReplayMetadata("replace-doc-comment-text-with-tag", "Microsoft.CodeAnalysis.CSharp.ReplaceDocCommentTextWithTag.CSharpReplaceDocCommentTextWithTagCodeRefactoringProvider", "Use <see cref=\"System.IDisposable\"/>"),
        CreateReplayMetadata("reverse-for-statement", "Microsoft.CodeAnalysis.CSharp.ReverseForStatement.CSharpReverseForStatementCodeRefactoringProvider", "Reverse for statement"),
        CreateReplayMetadata("use-explicit-type", "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseExplicitType.UseExplicitTypeCodeRefactoringProvider", "Use explicit type"),
        CreateReplayMetadata("use-implicit-type", "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseImplicitType.UseImplicitTypeCodeRefactoringProvider", "Use implicit type"),
        CreateReplayMetadata("use-named-arguments", "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider", "Add argument name 'left'"),
        CreateReplayMetadata("use-recursive-patterns", "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseRecursivePatterns.UseRecursivePatternsCodeRefactoringProvider", "Use recursive patterns"),
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertNumericLiteral.CSharpConvertNumericLiteralCodeRefactoringProvider",
            Title = "Convert to hex",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertTupleToStruct.CSharpConvertTupleToStructCodeRefactoringProvider",
            Title = "updating usages in containing member",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementExplicitlyCodeRefactoringProvider",
            Title = "Implement 'Goo1' explicitly",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementImplicitlyCodeRefactoringProvider",
            Title = "Implement 'Goo1' implicitly",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpInitializeMemberFromParameterCodeRefactoringProvider",
            TitlePrefix = "Initialize field",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineMethod.CSharpInlineMethodRefactoringProvider",
            Title = "Inline 'AddOne(int value)'",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddFileBanner.CSharpAddFileBannerCodeRefactoringProvider",
            Title = "Add file header",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.EnableNullable.EnableNullableCodeRefactoringProvider",
            Title = "Enable nullable reference types in project",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.SyncNamespace.CSharpSyncNamespaceCodeRefactoringProvider",
            TitlePrefix = "Change namespace to ",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertNamespace.ConvertNamespaceCodeRefactoringProvider",
            TitlePrefix = "Convert to file-scoped namespace",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToProgramMainCodeRefactoringProvider",
            Title = "Convert to 'Program.Main' style program",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeRefactoringProvider",
            Title = "Convert to top-level statements",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToExtension.ConvertToExtensionCodeRefactoringProvider",
            TitlePrefix = "Convert ",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToRawString.ConvertStringToRawStringCodeRefactoringProvider",
            Title = "Convert to raw string",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpAddParameterCheckCodeRefactoringProvider",
            Title = "Add null check",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpInitializeMemberFromPrimaryConstructorParameterCodeRefactoringProvider",
            TitlePrefix = "Initialize field",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.Wrapping.CSharpWrappingCodeRefactoringProvider",
            Title = "Indent all arguments",
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FullyQualify.CSharpFullyQualifyCodeFixProvider",
            Title = "System.Threading.CancellationToken",
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToProgramMainCodeFixProvider",
            Title = "Convert to 'Program.Main' style program",
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeFixProvider",
            Title = "Convert to top-level statements",
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.EmbeddedLanguages.CSharpJsonDetectionCodeFixProvider",
            Title = "Enable all JSON editor features",
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.RemoveUnusedVariable.CSharpRemoveUnusedVariableCodeFixProvider",
            Title = "Remove unused variable",
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SimplifyThisOrMe.CSharpSimplifyThisOrMeCodeFixProvider",
            Title = "Remove 'this' qualification",
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SimplifyTypeNames.SimplifyTypeNamesCodeFixProvider",
            Title = "Simplify name 'System.Text.StringBuilder'",
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SpellCheck.CSharpSpellCheckCodeFixProvider",
            Title = "Change 'Lenght' to 'Length'.",
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UsePatternMatching.CSharpIsAndCastCheckWithoutNameCodeFixProvider",
            Title = "Use pattern matching",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpMergeConsecutiveIfStatementsCodeRefactoringProvider",
            TitlePrefix = "Merge with next",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpMergeNestedIfStatementsCodeRefactoringProvider",
            TitlePrefix = "Merge with nested",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpSplitIntoConsecutiveIfStatementsCodeRefactoringProvider",
            TitlePrefix = "Split into consecutive",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpSplitIntoNestedIfStatementsCodeRefactoringProvider",
            TitlePrefix = "Split into nested",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            TitlePrefix = "Use expression body",
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            TitlePrefix = "Use expression body",
        },
    ];

    public static IReadOnlyList<BuiltInCodeActionAuditCase> VisibleReplayFamilies { get; } = _replayMetadata;

    private static readonly IReadOnlyList<BuiltInCodeActionAuditCase> _validatedCodeFixCompatibilityCases =
    [
        new()
        {
            ToolName = "add-anonymous-type-member-name",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddAnonymousTypeMemberName.CSharpAddAnonymousTypeMemberNameCodeFixProvider",
            SourceNote = "CandidateCodeFixes.CreateAnonymousMember invalid anonymous member declarator",
            ExpectedDiagnosticId = "CS0746",
            ExpectedChangedText = " = value + 1",
            UnexpectedChangedText = "new { value + 1 }",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "value + 1"),
        },
        new()
        {
            ToolName = "add-explicit-cast",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddExplicitCast.CSharpAddExplicitCastCodeFixProvider",
            SourceNote = "CandidateCodeFixes.AddExplicitCast implicit long-to-int conversion",
            ExpectedDiagnosticId = "CS0266",
            ExpectedChangedText = "return (int)value;",
            UnexpectedChangedText = "return value;",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "value;", 0),
        },
        new()
        {
            ToolName = "add-inheritdoc",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddInheritdoc.AddInheritdocCodeFixProvider",
            SourceNote = "CandidateDerived.DocumentedMember undocumented override",
            ExpectedDiagnosticId = "CS1591",
            ExpectedChangedText = "/// <inheritdoc/>",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "DocumentedMember", 1),
        },
        new()
        {
            ToolName = "remove-new-modifier",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.RemoveNewModifier.RemoveNewModifierCodeFixProvider",
            SourceNote = "CandidateNewModifier.RemoveNewModifier unnecessary new modifier",
            ExpectedDiagnosticId = "CS0109",
            ExpectedChangedText = "internal void RemoveNewModifier()",
            UnexpectedChangedText = "internal new void RemoveNewModifier()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "internal new void RemoveNewModifier"),
        },
        new()
        {
            ToolName = "add-conditional-interpolation-parentheses",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConditionalExpressionInStringInterpolation.CSharpAddParenthesesAroundConditionalExpressionInInterpolatedStringCodeFixProvider",
            SourceNote = "CandidateCodeFixes.FormatConditional unparenthesised conditional interpolation",
            ExpectedDiagnosticId = "CS8361",
            ExpectedChangedText = "{(enabled ? \"enabled\" : \"disabled\")}",
            UnexpectedChangedText = "{enabled ? \"enabled\" : \"disabled\"}",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "enabled ? \"enabled\" : \"disabled\""),
        },
        new()
        {
            ToolName = "remove-in-keyword",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.RemoveInKeyword.RemoveInKeywordCodeFixProvider",
            SourceNote = "CandidateCodeFixes.RemoveInKeyword invalid in argument",
            ExpectedDiagnosticId = "CS1615",
            ExpectedChangedText = "AcceptValue(value);",
            UnexpectedChangedText = "AcceptValue(in value);",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "in value"),
        },
        new()
        {
            ToolName = "replace-default-literal",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ReplaceDefaultLiteral.CSharpReplaceDefaultLiteralCodeFixProvider",
            SourceNote = "CandidateCodeFixes.ReplaceDefaultLiteral invalid default pattern",
            ExpectedDiagnosticId = "CS8505",
            ExpectedChangedText = "value is 0",
            UnexpectedChangedText = "value is default",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "default"),
        },
        new()
        {
            ToolName = "use-explicit-type-for-const",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseExplicitTypeForConst.UseExplicitTypeForConstCodeFixProvider",
            SourceNote = "CandidateCodeFixes.UseExplicitTypeForConst invalid const var declaration",
            ExpectedDiagnosticId = "CS0822",
            ExpectedChangedText = "const int value = 1;",
            UnexpectedChangedText = "const var value = 1;",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "const var"),
        },
    ];

    private static readonly IReadOnlyList<BuiltInCodeActionAuditCase> _additionalValidatedCodeFixCompatibilityCases =
    [
        new()
        {
            ToolName = "add-obsolete-attribute",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddObsoleteAttribute.CSharpAddObsoleteAttributeCodeFixProvider",
            SourceNote = "CandidateObsoleteDerived inherits an obsolete base type",
            ExpectedDiagnosticId = "CS0612",
            ExpectedChangedText = "[System.Obsolete]\ninternal sealed class CandidateObsoleteDerived",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "CandidateObsoleteBase", occurrenceIndex: 1),
        },
        new()
        {
            ToolName = "add-obsolete-attribute",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddObsoleteAttribute.CSharpAddObsoleteAttributeCodeFixProvider",
            SourceNote = "CandidateObsoleteMessageDerived inherits an obsolete base type with a message",
            ExpectedDiagnosticId = "CS0618",
            ExpectedChangedText = "[System.Obsolete]\ninternal sealed class CandidateObsoleteMessageDerived",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "CandidateObsoleteMessageBase", occurrenceIndex: 1),
        },
        new()
        {
            ToolName = "add-obsolete-attribute",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddObsoleteAttribute.CSharpAddObsoleteAttributeCodeFixProvider",
            SourceNote = "CandidateObsoleteOverrideDerived overrides an obsolete method",
            ExpectedDiagnosticId = "CS0672",
            ExpectedChangedText = "[System.Obsolete]\n    internal override void ObsoleteOverride()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "override void ObsoleteOverride()"),
        },
        new()
        {
            ToolName = "add-obsolete-attribute",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddObsoleteAttribute.CSharpAddObsoleteAttributeCodeFixProvider",
            SourceNote = "CreateObsoleteMessageCollection invokes an obsolete collection Add method with a message",
            ExpectedDiagnosticId = "CS1062",
            ExpectedChangedText = "[System.Obsolete]\n    internal static void CreateObsoleteMessageCollection()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "{ 1 }", occurrenceIndex: 1),
        },
        new()
        {
            ToolName = "add-obsolete-attribute",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddObsoleteAttribute.CSharpAddObsoleteAttributeCodeFixProvider",
            SourceNote = "CreateObsoleteCollection invokes an obsolete collection Add method without a message",
            ExpectedDiagnosticId = "CS1064",
            ExpectedChangedText = "[System.Obsolete]\n    internal static void CreateObsoleteCollection()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "{ 1 }", occurrenceIndex: 0),
        },
        new()
        {
            ToolName = "assign-out-parameters",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AssignOutParameters.AssignOutParametersAboveReturnCodeFixProvider",
            SourceNote = "AssignOutParameterAboveReturn leaves an out parameter unassigned",
            ExpectedDiagnosticId = "CS0177",
            ExpectedChangedText = "value = 0;",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "return 'a';", occurrenceIndex: 0),
        },
        new()
        {
            ToolName = "assign-out-parameters",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AssignOutParameters.AssignOutParametersAtStartCodeFixProvider",
            SourceNote = "AssignOutParameterAtStart assigns an out parameter on only one path",
            ExpectedDiagnosticId = "CS0177",
            ExpectedChangedText = "value = 0;",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "return 'a';", occurrenceIndex: 1),
        },
        new()
        {
            ToolName = "declare-as-nullable",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.DeclareAsNullable.CSharpDeclareAsNullableCodeFixProvider",
            SourceNote = "DeclareAsNullable returns null from a non-nullable method",
            ExpectedDiagnosticId = "CS8603",
            ExpectedChangedText = "internal static string? DeclareAsNullable()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "null"),
        },
        new()
        {
            ToolName = "declare-as-nullable",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.DeclareAsNullable.CSharpDeclareAsNullableCodeFixProvider",
            SourceNote = "DeclareLocalAsNullable converts a possibly null value to a non-nullable local",
            ExpectedDiagnosticId = "CS8600",
            ExpectedChangedText = "string? value = null;",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "string value = null;"),
        },
        new()
        {
            ToolName = "declare-as-nullable",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.DeclareAsNullable.CSharpDeclareAsNullableCodeFixProvider",
            SourceNote = "DeclareParameterAsNullable passes null to a non-nullable parameter",
            ExpectedDiagnosticId = "CS8625",
            ExpectedChangedText = "AcceptNullableValue(string? value)",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "AcceptNullableValue(null)", occurrenceIndex: 0),
        },
        new()
        {
            ToolName = "declare-as-nullable",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.DeclareAsNullable.CSharpDeclareAsNullableCodeFixProvider",
            SourceNote = "CandidateUninitializedNullable leaves a non-nullable property uninitialized",
            ExpectedDiagnosticId = "CS8618",
            ExpectedChangedText = "internal string? Value { get; }",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "Value { get; }"),
        },
        new()
        {
            ToolName = "fix-incorrect-constraint",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FixIncorrectConstraint.CSharpFixIncorrectConstraintCodeFixProvider",
            SourceNote = "CandidateEnumConstraint uses the invalid enum constraint keyword",
            ExpectedDiagnosticId = "CS9010",
            ExpectedChangedText = "where T : struct, System.Enum",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "enum"),
        },
        new()
        {
            ToolName = "fix-incorrect-constraint",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FixIncorrectConstraint.CSharpFixIncorrectConstraintCodeFixProvider",
            SourceNote = "CandidateDelegateConstraint uses the invalid delegate constraint keyword",
            ExpectedDiagnosticId = "CS9011",
            ExpectedChangedText = "where T : System.Delegate",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "delegate"),
        },
        new()
        {
            ToolName = "fix-return-type",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FixReturnType.CSharpFixReturnTypeCodeFixProvider",
            SourceNote = "FixReturnType returns an integer from a void method",
            ExpectedDiagnosticId = "CS0127",
            ExpectedChangedText = "internal static int FixReturnType()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "return 1;"),
        },
        new()
        {
            ToolName = "fix-return-type",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FixReturnType.CSharpFixReturnTypeCodeFixProvider",
            SourceNote = "FixAsyncReturnType returns an integer from an async Task method",
            ExpectedDiagnosticId = "CS1997",
            ExpectedChangedText = "internal static async System.Threading.Tasks.Task<int> FixAsyncReturnType()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "return 1;", occurrenceIndex: 1),
        },
        new()
        {
            ToolName = "fix-return-type",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FixReturnType.CSharpFixReturnTypeCodeFixProvider",
            SourceNote = "FixExpressionBodyReturnType uses an integer expression for a void expression body",
            ExpectedDiagnosticId = "CS0201",
            ExpectedChangedText = "internal static int FixExpressionBodyReturnType() => 1;",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "=> 1;"),
        },
    ];

    private static readonly IReadOnlyList<BuiltInCodeActionAuditCase> _promotedCodeFixCompatibilityCases =
    [
        new()
        {
            ToolName = "hide-base-member",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.HideBase.HideBaseCodeFixProvider",
            SourceNote = "CandidateHideBaseDerived.HiddenMember hides an inherited member without the new modifier",
            ExpectedDiagnosticId = "CS0108",
            ExpectedChangedText = "internal new void HiddenMember()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "HiddenMember", occurrenceIndex: 1),
        },
        new()
        {
            ToolName = "add-yield",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpAddYieldCodeFixProvider",
            SourceNote = "AddYieldForImplicitConversion returns a value convertible to the iterator element type",
            ExpectedDiagnosticId = "CS0029",
            ExpectedChangedText = "yield return \"value\";",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "return \"value\";"),
        },
        new()
        {
            ToolName = "add-yield",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpAddYieldCodeFixProvider",
            SourceNote = "AddYieldForExplicitConversion returns a value requiring an explicit conversion to the iterator type",
            ExpectedDiagnosticId = "CS0266",
            ExpectedChangedText = "yield return new object();",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "return new object();"),
        },
        new()
        {
            ToolName = "change-iterator-return-type",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpChangeToIEnumerableCodeFixProvider",
            SourceNote = "ChangeIteratorReturnType uses yield with a non-iterator return type",
            ExpectedDiagnosticId = "CS1624",
            ExpectedChangedText = "System.Collections.Generic.IEnumerable<object> ChangeIteratorReturnType()",
            UnexpectedChangedText = "object ChangeIteratorReturnType()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "ChangeIteratorReturnType"),
        },
        new()
        {
            ToolName = "make-member-required",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.MakeMemberRequired.CSharpMakeMemberRequiredCodeFixProvider",
            SourceNote = "CandidateRequiredMember.RequiredValue is an uninitialised settable non-nullable property",
            ExpectedDiagnosticId = "CS8618",
            ExpectedChangedText = "internal required string RequiredValue",
            UnexpectedChangedText = "internal string RequiredValue",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "RequiredValue"),
        },
        new()
        {
            ToolName = "transpose-record-keyword",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.TransposeRecordKeyword.CSharpTransposeRecordKeywordCodeFixProvider",
            SourceNote = "CandidateRecordKeyword places struct before the record keyword",
            ExpectedDiagnosticId = "CS9012",
            ExpectedChangedText = "internal record struct CandidateRecordKeyword",
            UnexpectedChangedText = "internal struct record CandidateRecordKeyword",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "record"),
        },
        new()
        {
            ToolName = "order-modifiers",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.OrderModifiers.CSharpOrderModifiersCodeFixProvider",
            SourceNote = "CandidateModifierOrder places partial before its accessibility modifier",
            ExpectedDiagnosticId = "CS0267",
            ExpectedChangedText = "public partial class CandidateModifierOrder",
            UnexpectedChangedText = "partial public class CandidateModifierOrder",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "partial public"),
        },
        new()
        {
            ToolName = "remove-unused-local-function",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.RemoveUnusedLocalFunction.CSharpRemoveUnusedLocalFunctionCodeFixProvider",
            SourceNote = "CandidateUnusedLocalFunction declares an unreferenced local function",
            ExpectedDiagnosticId = "CS8321",
            UnexpectedChangedText = "static void UnusedLocalFunction()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "UnusedLocalFunction"),
        },
        new()
        {
            ToolName = "use-explicit-array-in-expression-tree",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseExplicitArrayInExpressionTree.CSharpUseExplicitArrayInExpressionTreeCodeFixProvider",
            SourceNote = "CandidateExplicitArray uses an expanded non-array params collection in an expression tree",
            ExpectedDiagnosticId = "CS9226",
            ExpectedChangedText = "Format(System.Array.Empty<char>())",
            UnexpectedChangedText = "Format()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCodeFixes.cs", "Format()", occurrenceIndex: 0),
        },
    ];

    private static readonly IReadOnlyList<BuiltInCodeActionAuditCase> _promotedBatchOneCodeFixCompatibilityCases =
    [
        new()
        {
            ToolName = "make-statement-asynchronous",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.MakeStatementAsynchronous.CSharpMakeStatementAsynchronousCodeFixProvider",
            SourceNote = "CandidateStatementAsynchronous synchronously enumerates an asynchronous sequence",
            ExpectedDiagnosticId = "CS8414",
            ExpectedChangedText = "await foreach (var value in values)",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument(
                "CandidateLocalCodeFixes.cs",
                "values",
                occurrenceIndex: 1),
        },
        new()
        {
            ToolName = "make-statement-asynchronous",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.MakeStatementAsynchronous.CSharpMakeStatementAsynchronousCodeFixProvider",
            SourceNote = "CandidateStatementAsynchronous synchronously disposes an asynchronous disposable",
            ExpectedDiagnosticId = "CS8418",
            ExpectedChangedText = "await using (resource)",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateLocalCodeFixes.cs", "using (resource)"),
        },
        new()
        {
            ToolName = "disambiguate-same-variable",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.DisambiguateSameVariable.CSharpDisambiguateSameVariableCodeFixProvider",
            SourceNote = "CandidateSameVariable assigns a parameter to itself instead of the matching property",
            ExpectedDiagnosticId = "CS1717",
            ExpectedChangedText = "Value = value;",
            UnexpectedChangedText = "value = value;",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateLocalCodeFixes.cs", "value = value"),
        },
        new()
        {
            ToolName = "disambiguate-same-variable",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.DisambiguateSameVariable.CSharpDisambiguateSameVariableCodeFixProvider",
            SourceNote = "CandidateSameVariable compares a parameter with itself instead of the matching property",
            ExpectedDiagnosticId = "CS1718",
            ExpectedChangedText = "Value == value",
            UnexpectedChangedText = "value == value",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateLocalCodeFixes.cs", "value == value"),
        },
        new()
        {
            ToolName = "add-documentation-comment-nodes",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.DocumentationComments.CSharpAddDocCommentNodesCodeFixProvider",
            SourceNote = "CandidateDocumentationComments omits one parameter node from an existing parameter list",
            ExpectedDiagnosticId = "CS1573",
            ExpectedChangedText = "<param name=\"missing\"></param>",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateLocalCodeFixes.cs", "int missing"),
        },
        new()
        {
            ToolName = "remove-documentation-comment-node",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.DocumentationComments.CSharpRemoveDocCommentNodeCodeFixProvider",
            SourceNote = "CandidateDocumentationComments contains a duplicate parameter node",
            ExpectedDiagnosticId = "CS1571",
            UnexpectedChangedText = "<param name=\"value\">The duplicate value.</param>",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument(
                "CandidateLocalCodeFixes.cs",
                "<param name=\"value\">The duplicate value.</param>"),
        },
        new()
        {
            ToolName = "remove-documentation-comment-node",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.DocumentationComments.CSharpRemoveDocCommentNodeCodeFixProvider",
            SourceNote = "CandidateDocumentationComments contains an unmatched parameter node",
            ExpectedDiagnosticId = "CS1572",
            UnexpectedChangedText = "<param name=\"missing\">The missing value.</param>",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument(
                "CandidateLocalCodeFixes.cs",
                "<param name=\"missing\">The missing value.</param>"),
        },
        new()
        {
            ToolName = "remove-documentation-comment-node",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.DocumentationComments.CSharpRemoveDocCommentNodeCodeFixProvider",
            SourceNote = "CandidateDuplicateTypeParameter contains a duplicate type-parameter node",
            ExpectedDiagnosticId = "CS1710",
            UnexpectedChangedText = "<typeparam name=\"T\">The duplicate value type.</typeparam>",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument(
                "CandidateLocalCodeFixes.cs",
                "<typeparam name=\"T\">The duplicate value type.</typeparam>"),
        },
        new()
        {
            ToolName = "pass-captured-variables-as-arguments",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeLocalFunctionStatic.PassInCapturedVariablesAsArgumentsCodeFixProvider",
            SourceNote = "CandidateCapturedLocalFunction captures a parameter from a static local function",
            ExpectedDiagnosticId = "CS8421",
            ExpectedChangedText = "static void LocalFunction(int captured)",
            UnexpectedChangedText = "static void LocalFunction()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateLocalCodeFixes.cs", "captured;"),
        },
        new()
        {
            ToolName = "make-member-static",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeMemberStatic.CSharpMakeMemberStaticCodeFixProvider",
            SourceNote = "CandidateStaticType declares an instance method in a static type",
            ExpectedDiagnosticId = "CS0708",
            ExpectedChangedText = "internal static void MakeMemberStatic()",
            UnexpectedChangedText = "internal void MakeMemberStatic()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateLocalCodeFixes.cs", "MakeMemberStatic"),
        },
        new()
        {
            ToolName = "make-method-asynchronous",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeMethodAsynchronous.CSharpMakeMethodAsynchronousCodeFixProvider",
            SourceNote = "CandidateMethodAsynchronous awaits in a value-returning synchronous method",
            ExpectedDiagnosticId = "CS4032",
            ExpectedChangedText = "ReturnValueAsync()",
            UnexpectedChangedText = "ReturnValue()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument(
                "CandidateLocalCodeFixes.cs",
                "await System.Threading.Tasks.Task.Yield();",
                occurrenceIndex: 1),
        },
        new()
        {
            ToolName = "make-method-asynchronous",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeMethodAsynchronous.CSharpMakeMethodAsynchronousCodeFixProvider",
            Title = "Make method async",
            SourceNote = "CandidateMethodAsynchronous converts a void method to a task-returning async method",
            ExpectedDiagnosticId = "CS4033",
            ExpectedChangedText = "ReturnVoidAsync()",
            UnexpectedChangedText = "ReturnVoid()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument(
                "CandidateLocalCodeFixes.cs",
                "await System.Threading.Tasks.Task.Yield();",
                occurrenceIndex: 2),
        },
        new()
        {
            ToolName = "make-method-asynchronous",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeMethodAsynchronous.CSharpMakeMethodAsynchronousCodeFixProvider",
            Title = "Make method async (stay void)",
            SourceNote = "CandidateMethodAsynchronous retains void while making the method asynchronous",
            ExpectedDiagnosticId = "CS4033",
            ExpectedChangedText = "internal static async void ReturnVoid()",
            UnexpectedChangedText = "internal static void ReturnVoid()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument(
                "CandidateLocalCodeFixes.cs",
                "await System.Threading.Tasks.Task.Yield();",
                occurrenceIndex: 2),
        },
        new()
        {
            ToolName = "make-method-asynchronous",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeMethodAsynchronous.CSharpMakeMethodAsynchronousCodeFixProvider",
            SourceNote = "CandidateMethodAsynchronous awaits in a synchronous anonymous function",
            ExpectedDiagnosticId = "CS4034",
            ExpectedChangedText = "System.Action action = async () =>",
            UnexpectedChangedText = "System.Action action = () =>",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument(
                "CandidateLocalCodeFixes.cs",
                "await System.Threading.Tasks.Task.Yield();",
                occurrenceIndex: 3),
        },
        new()
        {
            ToolName = "make-method-asynchronous",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeMethodAsynchronous.CSharpMakeMethodAsynchronousCodeFixProvider",
            SourceNote = "CandidateCSharp4 parses await as an identifier before async language support",
            ExpectedDiagnosticId = "CS0246",
            ExpectedChangedText = "MakeMethodAsynchronousAsync(Task operation)",
            UnexpectedChangedText = "MakeMethodAsynchronous(Task operation)",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            FixtureFactory = static () => InspectionSampleFixture.Create(InspectionSampleProfile.CSharp4),
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCSharp4.cs", "await"),
        },
        new()
        {
            ToolName = "make-ref-struct",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeRefStruct.MakeRefStructCodeFixProvider",
            SourceNote = "CandidateRefStruct stores a span in a non-ref struct",
            ExpectedDiagnosticId = "CS8345",
            ExpectedChangedText = "internal ref struct CandidateRefStruct",
            UnexpectedChangedText = "internal struct CandidateRefStruct",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateLocalCodeFixes.cs", "System.Span<int>"),
        },
        new()
        {
            ToolName = "make-type-abstract",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeTypeAbstract.CSharpMakeTypeAbstractCodeFixProvider",
            SourceNote = "CandidateAbstractType declares an abstract member in a non-abstract type",
            ExpectedDiagnosticId = "CS0513",
            ExpectedChangedText = "internal abstract class CandidateAbstractType",
            UnexpectedChangedText = "internal class CandidateAbstractType",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateLocalCodeFixes.cs", "RequiredMember"),
        },
        new()
        {
            ToolName = "make-type-partial",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeTypePartial.CSharpMakeTypePartialCodeFixProvider",
            SourceNote = "CandidatePartialType has one declaration without the partial modifier",
            ExpectedDiagnosticId = "CS0260",
            ExpectedChangedText = "internal partial class CandidatePartialType",
            UnexpectedChangedText = "internal class CandidatePartialType",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument(
                "CandidateLocalCodeFixes.cs",
                "CandidatePartialType",
                occurrenceIndex: 1),
        },
        new()
        {
            ToolName = "unseal-class",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UnsealClass.CSharpUnsealClassCodeFixProvider",
            SourceNote = "CandidateSealedDerived inherits from a sealed base type",
            ExpectedDiagnosticId = "CS0509",
            ExpectedChangedText = "internal class CandidateSealedBase",
            UnexpectedChangedText = "internal sealed class CandidateSealedBase",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocationInDocument(
                "CandidateLocalCodeFixes.cs",
                "CandidateSealedBase",
                occurrenceIndex: 1),
        },
        new()
        {
            ToolName = "use-interpolated-verbatim-string",
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseInterpolatedVerbatimString.CSharpUseInterpolatedVerbatimStringCodeFixProvider",
            SourceNote = "CandidateCSharp73 uses the C# 8 interpolated-verbatim prefix order",
            ExpectedDiagnosticId = "CS8401",
            ExpectedChangedText = "return $@\"{value}\";",
            UnexpectedChangedText = "return @$\"{value}\";",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            FixtureFactory = static () => InspectionSampleFixture.Create(InspectionSampleProfile.CSharp73),
            LocationFactory = static fixture => fixture.GetLocationInDocument("CandidateCSharp73.cs", "@$\"{value}\""),
        },
    ];

    public static IReadOnlyList<BuiltInCodeActionAuditCase> CandidateCompatibilityCases { get; } = CreateCandidateCompatibilityCases();

    public static IReadOnlyList<BuiltInCodeActionAuditCase> SupportedCompatibilityCases { get; } =
    [
        .. _validatedCodeFixCompatibilityCases,
        .. _additionalValidatedCodeFixCompatibilityCases,
        .. _promotedCodeFixCompatibilityCases,
        .. _promotedBatchOneCodeFixCompatibilityCases,
        new()
        {
            ToolName = "add-constructor-parameters",
            ProviderId = "Microsoft.CodeAnalysis.AddConstructorParametersFromMembers.AddConstructorParametersFromMembersCodeRefactoringProvider",
            Title = "Add parameters to 'ConstructorParameterCandidate()'",
            SourceNote = "ConstructorParameterCandidate selected fields",
            ExpectedChangedText = "ConstructorParameterCandidate(int count, string name)",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetSelectionInDocument(
                "CandidateRefactorings.cs",
                "private readonly int _count;\r\n    private readonly string _name;"),
        },
        new()
        {
            ToolName = "generate-comparison-operators",
            ProviderId = "Microsoft.CodeAnalysis.GenerateComparisonOperators.GenerateComparisonOperatorsCodeRefactoringProvider",
            Title = "Generate comparison operators",
            SourceNote = "ComparisonOperatorCandidate type header",
            ExpectedChangedText = "operator <",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument(
                "CandidateRefactorings.cs",
                "ComparisonOperatorCandidate"),
        },
        new()
        {
            ToolName = "implement-interface",
            ProviderId = "Microsoft.CodeAnalysis.ImplementInterface.ImplementInterfaceCodeRefactoringProvider",
            Title = "Implement interface",
            SourceNote = "InterfaceImplementationCandidate empty body",
            ExpectedChangedText = "public string Format(int value)",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument(
                "CandidateRefactorings.cs",
                "InterfaceImplementationCandidate",
                0,
                "InterfaceImplementationCandidate : ICandidateFormatter\r\n{\r\n".Length),
        },
        new()
        {
            ToolName = "organize-imports",
            ProviderId = "Microsoft.CodeAnalysis.OrganizeImports.OrganizeImportsCodeRefactoringProvider",
            Title = "Sort Usings",
            SourceNote = "CandidateRefactorings unsorted using directives",
            ExpectedChangedText = "using System;\r\nusing System.Text;",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument(
                "CandidateRefactorings.cs",
                "using System.Text;"),
        },
        new()
        {
            ToolName = "replace-method-with-property",
            ProviderId = "Microsoft.CodeAnalysis.ReplaceMethodWithProperty.ReplaceMethodWithPropertyCodeRefactoringProvider",
            Title = "Replace 'GetValue' and 'SetValue' with property",
            SourceNote = "MethodPropertyCandidate getter and setter methods",
            ExpectedChangedText = "public int Value",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument(
                "CandidateRefactorings.cs",
                "GetValue"),
        },
        new()
        {
            ToolName = "replace-property-with-methods",
            ProviderId = "Microsoft.CodeAnalysis.ReplacePropertyWithMethods.ReplacePropertyWithMethodsCodeRefactoringProvider",
            Title = "Replace 'Value' with methods",
            SourceNote = "PropertyMethodCandidate property",
            ExpectedChangedText = "public int GetValue()",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument(
                "CandidateRefactorings.cs",
                "public int Value { get; set; }",
                0,
                "public int ".Length),
        },
        new()
        {
            ToolName = "convert-between-regular-and-verbatim-interpolated-string",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimInterpolatedStringCodeRefactoringProvider",
            Title = "Convert to verbatim string",
            SourceNote = "StringLiteralSamples.BuildInterpolated",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("$\"C:\\\\temp\\\\{value}\""),
        },
        new()
        {
            ToolName = "convert-between-regular-and-verbatim-string",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimStringCodeRefactoringProvider",
            Title = "Convert to verbatim string",
            SourceNote = "StringLiteralSamples.BuildRegular",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("\"C:\\\\temp\\\\logs\""),
        },
        new()
        {
            ToolName = "convert-foreach-to-for",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertForEachToFor.CSharpConvertForEachToForCodeRefactoringProvider",
            Title = "Convert to 'for'",
            SourceNote = "LoopSamples.SumForeach foreach statement",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("foreach (var value in values)"),
        },
        new()
        {
            ToolName = "convert-for-to-foreach",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertForToForEach.CSharpConvertForToForEachCodeRefactoringProvider",
            Title = "Convert to 'foreach'",
            SourceNote = "LoopSamples.SumFor for statement",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("for (var i = 0; i < values.Length; i++)", 0),
        },
        new()
        {
            ToolName = "convert-anonymous-type-to-tuple",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToTupleCodeRefactoringProvider",
            Title = "Convert to tuple",
            SourceNote = "AnonymousTypeSamples.Build anonymous object",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("new { Name = \"Alpha\", Count = 1 }"),
        },
        new()
        {
            ToolName = "convert-anonymous-type-to-class",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider",
            Title = "Convert to class",
            SourceNote = "AnonymousTypeSamples.Build anonymous object",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("new { Name = \"Alpha\", Count = 1 }"),
        },
        new()
        {
            ToolName = "convert-auto-property-to-full-property",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider",
            Title = "Convert to full property",
            SourceNote = "AutoPropertySamples.Goo property declaration",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("Goo"),
        },
        new()
        {
            ToolName = "convert-to-record",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToRecord.CSharpConvertToRecordRefactoringProvider",
            Title = "Convert to positional record",
            SourceNote = "ConvertibleToRecord class declaration",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("ConvertibleToRecord"),
        },
        new()
        {
            ToolName = "convert-direct-cast-to-try-cast",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertDirectCastToTryCastCodeRefactoringProvider",
            Title = "Change to 'as' expression",
            SourceNote = "CastSamples.Box cast expression",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("(object)1"),
        },
        new()
        {
            ToolName = "convert-local-function-to-method",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ConvertLocalFunctionToMethod.CSharpConvertLocalFunctionToMethodCodeRefactoringProvider",
            Title = "Convert to method",
            SourceNote = "LocalFunctionSamples.Local declaration",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("Local", 0),
        },
        new()
        {
            ToolName = "convert-primary-to-regular-constructor",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertPrimaryToRegularConstructor.ConvertPrimaryToRegularConstructorCodeRefactoringProvider",
            Title = "Convert to regular constructor",
            SourceNote = "PrimaryConstructorSamples primary constructor declaration",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetSelection("PrimaryConstructorSamples(int value)"),
        },
        new()
        {
            ToolName = "convert-try-cast-to-direct-cast",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertTryCastToDirectCastCodeRefactoringProvider",
            Title = "Change to cast",
            SourceNote = "CastSamples.Unbox as expression",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetSelection("value as string"),
        },
        new()
        {
            ToolName = "invert-conditional",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InvertConditional.CSharpInvertConditionalCodeRefactoringProvider",
            Title = "Invert conditional",
            SourceNote = "ConditionalSamples.DescribeCount conditional expression",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetSelection("count == 0 ? \"zero\" : \"non-zero\""),
        },
        new()
        {
            ToolName = "invert-if",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InvertIf.CSharpInvertIfCodeRefactoringProvider",
            Title = "Invert if",
            SourceNote = "ConditionalSamples.GuardedAdd if statement",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("if (left > 0)"),
        },
        new()
        {
            ToolName = "make-local-function-static",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MakeLocalFunctionStatic.MakeLocalFunctionStaticCodeRefactoringProvider",
            Title = "Make local function 'static'",
            SourceNote = "LocalFunctionSamples.Local declaration with captured local",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("Local", 0),
        },
        new()
        {
            ToolName = "move-declaration-near-reference",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.MoveDeclarationNearReference.CSharpMoveDeclarationNearReferenceCodeRefactoringProvider",
            Title = "Move declaration near reference",
            SourceNote = "MoveDeclarationSamples.BuildNearest local declaration",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("int moved;"),
        },
        new()
        {
            ToolName = "name-tuple-element",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.NameTupleElement.CSharpNameTupleElementCodeRefactoringProvider",
            Title = "Add tuple element name 'Sum'",
            SourceNote = "TupleSamples.Build returned tuple expression",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("return (1 + 1, 2);", 0, "return (".Length),
        },
        new()
        {
            ToolName = "replace-doc-comment-text-with-tag",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ReplaceDocCommentTextWithTag.CSharpReplaceDocCommentTextWithTagCodeRefactoringProvider",
            Title = "Use <see cref=\"System.IDisposable\"/>",
            SourceNote = "DocCommentSamples summary text",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("System.IDisposable"),
        },
        new()
        {
            ToolName = "reverse-for-statement",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ReverseForStatement.CSharpReverseForStatementCodeRefactoringProvider",
            Title = "Reverse 'for' statement",
            SourceNote = "LoopSamples.SumFor for statement",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("for (var i = 0; i < values.Length; i++)", 0),
        },
        new()
        {
            ToolName = "add-await",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
            Title = "Add 'await'",
            SourceNote = "AwaitSamples.BuildAssignmentAsync assignment initializer",
            ActionPath = [0],
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorAfter("GetValueAsync()", 2),
        },
        new()
        {
            ToolName = "add-debugger-display",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddDebuggerDisplay.CSharpAddDebuggerDisplayCodeRefactoringProvider",
            Title = "Add 'DebuggerDisplay' attribute",
            SourceNote = "GreetingFormatter declaration",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("GreetingFormatter", 0),
        },
        new()
        {
            ToolName = "add-import",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
            Title = "Add 'using System.Net.Http;'",
            SourceNote = "QualifiedTypeSamples fully qualified HttpClient",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("System.Net.Http.HttpClient"),
        },
        new()
        {
            ToolName = "add-null-checks",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpAddParameterCheckCodeRefactoringProvider",
            Title = "Add null check",
            SourceNote = "AddParameterCheck.cs constructor parameter",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument("AddParameterCheck.cs", "object value"),
        },
        new()
        {
            ToolName = "convert-if-to-switch",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertIfToSwitch.CSharpConvertIfToSwitchCodeRefactoringProvider",
            Title = "Convert to 'switch' statement",
            SourceNote = "ConditionalSamples.DescribeValue leading if statement",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("if (value == 0)"),
        },
        new()
        {
            ToolName = "convert-expression-body",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            TitlePrefix = "Use expression body",
            SourceNote = "ExpressionBodySamples.Square method",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("Square"),
        },
        new()
        {
            ToolName = "invert-logical",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InvertLogical.CSharpInvertLogicalCodeRefactoringProvider",
            Title = "Replace '&&' with '||' ",
            SourceNote = "ConditionalSamples.IsInRange logical operator",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("&&"),
        },
        new()
        {
            ToolName = "introduce-using-statement",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.IntroduceUsingStatement.CSharpIntroduceUsingStatementCodeRefactoringProvider",
            Title = "Introduce 'using' statement",
            SourceNote = "DisposableSamples.Build local declaration",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetSelection("var stream = new MemoryStream();"),
        },
        new()
        {
            ToolName = "replace-conditional-with-statements",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ReplaceConditionalWithStatements.CSharpReplaceConditionalWithStatementsCodeRefactoringProvider",
            Title = "Replace conditional expression with statements",
            SourceNote = "ConditionalRewriteSamples.BuildAssignment assignment statement",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("value = enabled ? 1 : 2;"),
        },
        new()
        {
            ToolName = "use-explicit-type",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseExplicitType.UseExplicitTypeCodeRefactoringProvider",
            Title = "Use explicit type",
            SourceNote = "TypeStyleSamples.UseExplicit local declaration type token",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("var explicitBuilder", 0, "var".Length),
        },
        new()
        {
            ToolName = "use-implicit-type",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseImplicitType.UseImplicitTypeCodeRefactoringProvider",
            Title = "Use implicit type",
            SourceNote = "TypeStyleSamples.UseImplicit local declaration type token",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("StringBuilder implicitBuilder", 0, "StringBuilder".Length),
        },
        new()
        {
            ToolName = "use-named-arguments",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
            Title = "Add argument name 'left'",
            SourceNote = "NamedArgumentSamples.Build first positional argument",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("Sum(1, 2)", 0, 4),
        },
        new()
        {
            ToolName = "use-recursive-patterns",
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseRecursivePatterns.UseRecursivePatternsCodeRefactoringProvider",
            Title = "Use recursive patterns",
            SourceNote = "PatternFieldHolder.HasNonZeroCount logical and expression",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("cf != null && cf.C != 0", 0, "cf != null ".Length),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertNumericLiteral.CSharpConvertNumericLiteralCodeRefactoringProvider",
            Title = "Convert to hex",
            SourceNote = "NumericLiteralSamples.Build decimal literal",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("42"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertTupleToStruct.CSharpConvertTupleToStructCodeRefactoringProvider",
            Title = "updating usages in containing member",
            SourceNote = "TupleSamples.Build tuple return type",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("(int Sum, int Count)"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementExplicitlyCodeRefactoringProvider",
            Title = "Implement 'Goo1' explicitly",
            SourceNote = "ExplicitInterfaceSamples.Goo1 implicit implementation",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("public void Goo1", 0, "public void ".Length),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ImplementInterface.CSharpImplementImplicitlyCodeRefactoringProvider",
            Title = "Implement 'Goo1' implicitly",
            SourceNote = "ImplicitInterfaceSamples.Goo1 explicit implementation",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("void IGoo.Goo1", 0, "void IGoo.".Length),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpInitializeMemberFromParameterCodeRefactoringProvider",
            TitlePrefix = "Initialize field",
            SourceNote = "ParameterInitializationSamples constructor parameter",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("string name"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineMethod.CSharpInlineMethodRefactoringProvider",
            Title = "Inline 'AddOne(int value)'",
            SourceNote = "InlineMethodSamples.Caller invocation",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("AddOne(1)"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.AddFileBanner.CSharpAddFileBannerCodeRefactoringProvider",
            Title = "Add file header",
            SourceNote = "BannerTarget.cs start of document with sibling banner reference",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument("BannerTarget.cs", "using System"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.EnableNullable.EnableNullableCodeRefactoringProvider",
            Title = "Enable nullable reference types in project",
            SourceNote = "EnableNullable.cs nullable directive in nullable-disabled project",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            FixtureFactory = static () => InspectionSampleFixture.Create(InspectionSampleProfile.NullableDisabled),
            LocationFactory = static fixture => fixture.GetCursorInDocument("EnableNullable.cs", "#nullable enable"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.SyncNamespace.CSharpSyncNamespaceCodeRefactoringProvider",
            TitlePrefix = "Change namespace to ",
            SourceNote = "FolderSync/NamespaceSyncSample.cs namespace name in mismatched folder",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument("FolderSync/NamespaceSyncSample.cs", "Sample"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertNamespace.ConvertNamespaceCodeRefactoringProvider",
            TitlePrefix = "Convert to file-scoped namespace",
            SourceNote = "NamespaceConversion.cs block-scoped namespace declaration",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            FixtureFactory = static () => InspectionSampleFixture.Create(InspectionSampleProfile.BlockScopedNamespaces),
            LocationFactory = static fixture => fixture.GetCursorInDocument("NamespaceConversion.cs", "namespace Sample.Nested"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToProgramMainCodeRefactoringProvider",
            Title = "Convert to 'Program.Main' style program",
            SourceNote = "ConsoleTopLevel.cs top-level statement in console application",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            FixtureFactory = static () => InspectionSampleFixture.Create(InspectionSampleProfile.TopLevelToProgramMainRefactoring),
            LocationFactory = static fixture => fixture.GetCursorInDocument("ConsoleTopLevel.cs", "System.Console.WriteLine(0);"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeRefactoringProvider",
            Title = "Convert to top-level statements",
            SourceNote = "ConsoleProgramMain.cs Main method in console application",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            FixtureFactory = static () => InspectionSampleFixture.Create(InspectionSampleProfile.ProgramMainToTopLevelRefactoring),
            LocationFactory = static fixture => fixture.GetCursorInDocument("ConsoleProgramMain.cs", "Main"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToExtension.ConvertToExtensionCodeRefactoringProvider",
            TitlePrefix = "Convert ",
            SourceNote = "ExtensionMethods.cs extension method declaration",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument("ExtensionMethods.cs", "ToGreeting"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertToRawString.ConvertStringToRawStringCodeRefactoringProvider",
            Title = "Convert to raw string",
            SourceNote = "RawString.cs regular string literal",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument("RawString.cs", "\"raw\""),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpInitializeMemberFromPrimaryConstructorParameterCodeRefactoringProvider",
            TitlePrefix = "Initialize field",
            SourceNote = "PrimaryConstructorInitialization.cs primary constructor parameter",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument("PrimaryConstructorInitialization.cs", "string s"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.Wrapping.CSharpWrappingCodeRefactoringProvider",
            Title = "Indent all arguments",
            SourceNote = "Wrapping.cs invocation expression header",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument("Wrapping.cs", "Goobar(left, right)"),
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeFixes.FullyQualify.CSharpFullyQualifyCodeFixProvider",
            Title = "System.Threading.CancellationToken",
            SourceNote = "FullyQualify.cs unresolved CancellationToken type",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument("FullyQualify.cs", "CancellationToken"),
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToProgramMainCodeFixProvider",
            Title = "Convert to 'Program.Main' style program",
            SourceNote = "ConsoleTopLevel.cs top-level statements when Program.Main is preferred",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            FixtureFactory = static () => InspectionSampleFixture.Create(InspectionSampleProfile.TopLevelToProgramMainCodeFix),
            LocationFactory = static fixture => fixture.GetCursorInDocument("ConsoleTopLevel.cs", "System.Console.WriteLine(0);"),
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeFixProvider",
            Title = "Convert to top-level statements",
            SourceNote = "ConsoleProgramMain.cs Program.Main when top-level statements are preferred",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            FixtureFactory = static () => InspectionSampleFixture.Create(InspectionSampleProfile.ProgramMainToTopLevelCodeFix),
            LocationFactory = static fixture => fixture.GetCursorInDocument("ConsoleProgramMain.cs", "Main"),
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.RemoveUnusedVariable.CSharpRemoveUnusedVariableCodeFixProvider",
            Title = "Remove unused variable",
            SourceNote = "RemoveUnusedVariable.cs unused local declaration",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument("RemoveUnusedVariable.cs", "unused"),
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SpellCheck.CSharpSpellCheckCodeFixProvider",
            Title = "Change 'Lenght' to 'Length'.",
            SourceNote = "SpellCheck.cs misspelt Length member access",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursorInDocument("SpellCheck.cs", "Lenght"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpMergeConsecutiveIfStatementsCodeRefactoringProvider",
            TitlePrefix = "Merge with next",
            SourceNote = "IfRewriteSamples.MergeConsecutive first if statement",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("if (left)", 1, 0),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpMergeNestedIfStatementsCodeRefactoringProvider",
            TitlePrefix = "Merge with nested",
            SourceNote = "IfRewriteSamples.MergeNested outer if statement",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("if (left)", 0, 0),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpSplitIntoConsecutiveIfStatementsCodeRefactoringProvider",
            TitlePrefix = "Split into consecutive",
            SourceNote = "IfRewriteSamples.SplitConsecutive logical-or token",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("||"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.SplitOrMergeIfStatements.CSharpSplitIntoNestedIfStatementsCodeRefactoringProvider",
            TitlePrefix = "Split into nested",
            SourceNote = "IfRewriteSamples.SplitNested logical-and token",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetCursor("left && right", 0, "left ".Length),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            TitlePrefix = "Use expression body",
            SourceNote = "ExpressionBodySamples.CreateLambda lambda body",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            LocationFactory = static fixture => fixture.GetLocation("value =>"),
        },
    ];

    private static BuiltInCodeActionAuditCase CreateReplayMetadata(string? toolName, string providerId, string title)
    {
        return new BuiltInCodeActionAuditCase
        {
            ToolName = toolName,
            ProviderId = providerId,
            Title = title,
        };
    }

    private static List<BuiltInCodeActionAuditCase> CreateCandidateCompatibilityCases()
    {
        var candidates = new List<BuiltInCodeActionAuditCase>();
        foreach (var providerId in BuiltInCodeFixProviderAssessment.ProviderIds)
        {
            var auditStatus = BuiltInCodeFixProviderAssessment.GetAuditStatus(providerId);
            if (auditStatus is not BuiltInCodeActionAuditStatus.PendingReplayValidation)
            {
                continue;
            }

            if (candidates.Any(candidate => candidate.ProviderId == providerId))
            {
                continue;
            }

            candidates.Add(new BuiltInCodeActionAuditCase
            {
                Kind = BuiltInCodeActionAuditKind.CodeFix,
                ProviderId = providerId,
            });
        }

        return candidates;
    }
}
