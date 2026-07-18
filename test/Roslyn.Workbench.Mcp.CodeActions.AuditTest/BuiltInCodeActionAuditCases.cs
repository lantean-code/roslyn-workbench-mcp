namespace Roslyn.Workbench.Mcp.CodeActions.Test;

public enum BuiltInCodeActionRuntimeAuditOutcome
{
    NotOffered,
    OfferedButNotReplayable,
    OfferedAndReplayable,
}

public enum BuiltInCodeActionAuditKind
{
    Refactoring,
    CodeFix,
}

public sealed record BuiltInCodeActionAuditCase
{
    public string? ToolName { get; init; }

    public BuiltInCodeActionAuditKind Kind { get; init; } = BuiltInCodeActionAuditKind.Refactoring;

    public string ProviderId { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? TitlePrefix { get; init; }

    public string SourceNote { get; init; } = string.Empty;

    public IReadOnlyList<int> ActionPath { get; init; } = [];

    public BuiltInCodeActionRuntimeAuditOutcome ExpectedRuntimeOutcome { get; init; }

    public Func<Task<InspectionSampleFixture>> FixtureFactory { get; init; } = InspectionSampleFixture.CreateAsync;

    public Func<InspectionSampleFixture, LocationSelector> LocationFactory { get; init; } = static fixture => fixture.GetLocation("GreetingFormatter");
}

public static class BuiltInCodeActionAuditCases
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

    private static readonly IReadOnlyDictionary<string, BuiltInCodeActionAuditCase> _replayMetadataByProviderId =
        _replayMetadata.ToDictionary(static item => item.ProviderId, StringComparer.Ordinal);

    public static IReadOnlyList<string> VisibleDedicatedToolNames { get; } = BuiltInCodeActionLedger.Families
        .Where(static family => family.IsDedicatedToolVisible)
        .Select(static family => family.ToolName!)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(static toolName => toolName, StringComparer.Ordinal)
        .ToArray();

    public static IReadOnlyList<BuiltInCodeActionAuditCase> VisibleReplayFamilies { get; } = BuiltInCodeActionLedger.Families
        .Where(static family => family.ExecutionMode == CodeActionExecutionMode.Replay)
        .Where(static family => family.IsVisible)
        .Select(static family => family.ProviderId)
        .Where(_replayMetadataByProviderId.ContainsKey)
        .Select(static providerId => _replayMetadataByProviderId[providerId])
        .ToArray();

    public static IReadOnlyList<BuiltInCodeActionAuditCase> SupportedCompatibilityCases { get; } =
    [
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
            FixtureFactory = static () => InspectionSampleFixture.CreateAsync(InspectionSampleProfile.NullableDisabled),
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
            FixtureFactory = static () => InspectionSampleFixture.CreateAsync(InspectionSampleProfile.BlockScopedNamespaces),
            LocationFactory = static fixture => fixture.GetCursorInDocument("NamespaceConversion.cs", "namespace Sample.Nested"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToProgramMainCodeRefactoringProvider",
            Title = "Convert to 'Program.Main' style program",
            SourceNote = "ConsoleTopLevel.cs top-level statement in console application",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            FixtureFactory = static () => InspectionSampleFixture.CreateAsync(InspectionSampleProfile.TopLevelToProgramMainRefactoring),
            LocationFactory = static fixture => fixture.GetCursorInDocument("ConsoleTopLevel.cs", "System.Console.WriteLine(0);"),
        },
        new()
        {
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeRefactoringProvider",
            Title = "Convert to top-level statements",
            SourceNote = "ConsoleProgramMain.cs Main method in console application",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            FixtureFactory = static () => InspectionSampleFixture.CreateAsync(InspectionSampleProfile.ProgramMainToTopLevelRefactoring),
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
            FixtureFactory = static () => InspectionSampleFixture.CreateAsync(InspectionSampleProfile.TopLevelToProgramMainCodeFix),
            LocationFactory = static fixture => fixture.GetCursorInDocument("ConsoleTopLevel.cs", "System.Console.WriteLine(0);"),
        },
        new()
        {
            Kind = BuiltInCodeActionAuditKind.CodeFix,
            ProviderId = "Microsoft.CodeAnalysis.CSharp.ConvertProgram.ConvertToTopLevelStatementsCodeFixProvider",
            Title = "Convert to top-level statements",
            SourceNote = "ConsoleProgramMain.cs Program.Main when top-level statements are preferred",
            ExpectedRuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable,
            FixtureFactory = static () => InspectionSampleFixture.CreateAsync(InspectionSampleProfile.ProgramMainToTopLevelCodeFix),
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
}
