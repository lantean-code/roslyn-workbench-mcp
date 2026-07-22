using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

internal static class BundledCodeActionToolRegistrar
{
    public static void RegisterAll(ICodeActionToolRegistry registry)
    {
        RegisterInfrastructureTools(registry);
        RegisterAddTools(registry);
        RegisterConvertTools(registry);
        RegisterExtractAndIntroduceTools(registry);
        RegisterInvertAndMoveTools(registry);
        RegisterRemainingRefactoringTools(registry);
    }

    private static void RegisterInfrastructureTools(ICodeActionToolRegistry registry)
    {
        registry.RegisterQueryTool<DescribeCodeActionTool, DescribeCodeActionRequest, DescribeCodeActionData>(
            CreateMetadata(
                "describe-code-action",
                "Describe Code Action",
                "Revalidates one discovered code action and returns its execution descriptor and preflight context."));
        registry.RegisterQueryTool<ListCodeActionsTool, ListCodeActionsRequest, CodeActionListData>(
            CreateMetadata(
                "list-code-actions",
                "List Code Actions",
                "Lists applicable code actions and code fixes at a target location."));
        registry.RegisterMutationTool<StageCodeActionTool, StageCodeActionRequest>(
            CreateMutationMetadata(
                "stage-code-action",
                "Stage Code Action",
                "Revalidates and stages one selected refactoring action into the active transaction."));
        registry.RegisterMutationTool<StageCodeFixTool, StageCodeFixRequest>(
            CreateMutationMetadata(
                "stage-code-fix",
                "Stage Code Fix",
                "Revalidates and stages one selected code fix into the active transaction."));
        registry.RegisterMutationTool<StageFixAllTool, StageFixAllRequest>(
            CreateMutationMetadata(
                "stage-fix-all",
                "Stage Fix All",
                "Revalidates one selected code fix and stages its fix-all variant into the active transaction."));
    }

    private static void RegisterAddTools(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool<AddAwaitTool, AddAwaitRequest>(
            CreateMutationMetadata(
                "add-await",
                "Add Await",
                "Stages one supported add-await refactoring through Roslyn refactoring composition."));
        registry.RegisterMutationTool<AddDebuggerDisplayTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "add-debugger-display",
                "Add Debugger Display",
                "Adds a DebuggerDisplay attribute through Roslyn refactoring composition."));
        registry.RegisterMutationTool<AddImportTool, AddImportRequest>(
            CreateMutationMetadata(
                "add-import",
                "Add Import",
                "Adds a supported using directive through Roslyn refactoring composition."));
        registry.RegisterMutationTool<AddMissingUsingsTool, AddMissingUsingsRequest>(
            CreateMutationMetadata(
                "add-missing-usings",
                "Add Missing Usings",
                "Adds missing using directives across a selected scope through Roslyn code-fix composition."));
        registry.RegisterMutationTool<AddNullChecksTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "add-null-checks",
                "Add Null Checks",
                "Stages the supported Roslyn parameter null-check refactoring at the selected parameter location."));
    }

    private static void RegisterConvertTools(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool<ConvertAnonymousTypeToClassTool, ConvertAnonymousTypeToClassRequest>(
            CreateMutationMetadata(
                "convert-anonymous-type-to-class",
                "Convert Anonymous Type To Class",
                "Converts a supported anonymous type to a generated class or record through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertAnonymousTypeToTupleTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "convert-anonymous-type-to-tuple",
                "Convert Anonymous Type To Tuple",
                "Converts a supported anonymous type to a tuple through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertAutoPropertyToFullPropertyTool, ConvertAutoPropertyToFullPropertyRequest>(
            CreateMutationMetadata(
                "convert-auto-property-to-full-property",
                "Convert Auto Property To Full Property",
                "Converts a supported auto-property to a full property through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertBetweenRegularAndVerbatimInterpolatedStringTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "convert-between-regular-and-verbatim-interpolated-string",
                "Convert Between Regular And Verbatim Interpolated String",
                "Converts a supported interpolated string between regular and verbatim forms through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertBetweenRegularAndVerbatimStringTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "convert-between-regular-and-verbatim-string",
                "Convert Between Regular And Verbatim String",
                "Converts a supported string literal between regular and verbatim forms through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertDirectCastToTryCastTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "convert-direct-cast-to-try-cast",
                "Convert Direct Cast To Try Cast",
                "Converts a supported cast expression to an as-expression through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertExpressionBodyTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "convert-expression-body",
                "Convert Expression Body",
                "Stages a supported Roslyn block-body or expression-body conversion at the selected declaration."));
        registry.RegisterMutationTool<ConvertForEachToForTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "convert-foreach-to-for",
                "Convert Foreach To For",
                "Converts a supported foreach loop to a for loop through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertForToForeachTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "convert-for-to-foreach",
                "Convert For To Foreach",
                "Converts a supported for loop to a foreach loop through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertForeachLinqTool, ConvertForeachLinqRequest>(
            CreateMutationMetadata(
                "convert-foreach-linq",
                "Convert Foreach LINQ",
                "Stages one supported Roslyn foreach or LINQ conversion through refactoring composition."));
        registry.RegisterMutationTool<ConvertIfToSwitchTool, ConvertIfToSwitchRequest>(
            CreateMutationMetadata(
                "convert-if-to-switch",
                "Convert If To Switch",
                "Converts a supported if-chain to a switch statement or switch expression through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertLocalFunctionToMethodTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "convert-local-function-to-method",
                "Convert Local Function To Method",
                "Converts a supported local function to a method through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertPrimaryToRegularConstructorTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "convert-primary-to-regular-constructor",
                "Convert Primary To Regular Constructor",
                "Converts a supported primary constructor to a regular constructor through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertPropertyTool, ConvertPropertyRequest>(
            CreateMutationMetadata(
                "convert-property",
                "Convert Property",
                "Converts one selected property between supported auto-property and full-property forms through Roslyn composition."));
        registry.RegisterMutationTool<ConvertToInterpolatedStringTool, ConvertToInterpolatedStringRequest>(
            CreateMutationMetadata(
                "convert-to-interpolated-string",
                "Convert To Interpolated String",
                "Converts a supported string expression to an interpolated string through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertToRecordTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "convert-to-record",
                "Convert To Record",
                "Converts a supported class declaration to a record through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ConvertTryCastToDirectCastTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "convert-try-cast-to-direct-cast",
                "Convert Try Cast To Direct Cast",
                "Converts a supported as-expression to a cast expression through Roslyn refactoring composition."));
    }

    private static void RegisterExtractAndIntroduceTools(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool<EncapsulateFieldTool, EncapsulateFieldRequest>(
            CreateMutationMetadata(
                "encapsulate-field",
                "Encapsulate Field",
                "Encapsulates one field through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ExtractMethodTool, ExtractMethodRequest>(
            CreateMutationMetadata(
                "extract-method",
                "Extract Method",
                "Extracts a selected statement or expression block through Roslyn refactoring composition."));
        registry.RegisterMutationTool<InlineVariableTool, InlineVariableRequest>(
            CreateMutationMetadata(
                "inline-variable",
                "Inline Variable",
                "Inlines a local variable through Roslyn refactoring composition."));
        registry.RegisterMutationTool<IntroduceParameterTool, IntroduceParameterRequest>(
            CreateMutationMetadata(
                "introduce-parameter",
                "Introduce Parameter",
                "Promotes a selected expression to a parameter through Roslyn refactoring composition."));
        registry.RegisterMutationTool<IntroduceUsingStatementTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "introduce-using-statement",
                "Introduce Using Statement",
                "Introduces a supported using statement or declaration through Roslyn refactoring composition."));
        registry.RegisterMutationTool<IntroduceVariableTool, IntroduceVariableRequest>(
            CreateMutationMetadata(
                "introduce-variable",
                "Introduce Variable",
                "Stages one supported Roslyn introduce-variable leaf action through refactoring composition."));
    }

    private static void RegisterInvertAndMoveTools(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool<InvertConditionalTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "invert-conditional",
                "Invert Conditional",
                "Inverts a supported conditional expression through Roslyn refactoring composition."));
        registry.RegisterMutationTool<InvertIfTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "invert-if",
                "Invert If",
                "Inverts a supported if statement through Roslyn refactoring composition."));
        registry.RegisterMutationTool<InvertLogicalTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "invert-logical",
                "Invert Logical",
                "Inverts a supported logical expression through Roslyn refactoring composition."));
        registry.RegisterMutationTool<MakeLocalFunctionStaticTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "make-local-function-static",
                "Make Local Function Static",
                "Marks a supported local function as static through Roslyn refactoring composition."));
        registry.RegisterMutationTool<MoveDeclarationNearReferenceTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "move-declaration-near-reference",
                "Move Declaration Near Reference",
                "Moves a supported local declaration nearer to its first use through Roslyn refactoring composition."));
        registry.RegisterMutationTool<MoveTypeToFileTool, MoveTypeToFileRequest>(
            CreateMutationMetadata(
                "move-type-to-file",
                "Move Type To File",
                "Moves one selected type into its own Roslyn-chosen file within the current project."));
    }

    private static void RegisterRemainingRefactoringTools(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool<NameTupleElementTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "name-tuple-element",
                "Name Tuple Element",
                "Adds a supported tuple element name through Roslyn refactoring composition."));
        registry.RegisterMutationTool<RemoveUnusedUsingsTool, RemoveUnusedUsingsRequest>(
            CreateMutationMetadata(
                "remove-unused-usings",
                "Remove Unused Usings",
                "Removes unused using directives across a selected scope through Roslyn code-fix composition."));
        registry.RegisterMutationTool<ReplaceConditionalWithStatementsTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "replace-conditional-with-statements",
                "Replace Conditional With Statements",
                "Rewrites a supported conditional expression into statements through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ReplaceDocCommentTextWithTagTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "replace-doc-comment-text-with-tag",
                "Replace Doc Comment Text With Tag",
                "Replaces supported XML doc comment text with a documentation tag through Roslyn refactoring composition."));
        registry.RegisterMutationTool<ReverseForStatementTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "reverse-for-statement",
                "Reverse For Statement",
                "Reverses a supported for-statement loop through Roslyn refactoring composition."));
        registry.RegisterMutationTool<UseExplicitTypeTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "use-explicit-type",
                "Use Explicit Type",
                "Converts a supported declaration to an explicit type through Roslyn refactoring composition."));
        registry.RegisterMutationTool<UseImplicitTypeTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "use-implicit-type",
                "Use Implicit Type",
                "Converts a supported declaration to an implicit type through Roslyn refactoring composition."));
        registry.RegisterMutationTool<UseNamedArgumentsTool, UseNamedArgumentsRequest>(
            CreateMutationMetadata(
                "use-named-arguments",
                "Use Named Arguments",
                "Adds a supported argument name through Roslyn refactoring composition."));
        registry.RegisterMutationTool<UseRecursivePatternsTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "use-recursive-patterns",
                "Use Recursive Patterns",
                "Converts a supported pattern expression to recursive patterns through Roslyn refactoring composition."));
    }

    private static CodeActionToolMetadata CreateMetadata(string name, string title, string description)
    {
        return new CodeActionToolMetadata
        {
            Name = name,
            Title = title,
            Description = description,
        };
    }

    private static CodeActionToolMetadata CreateMutationMetadata(string name, string title, string description)
    {
        return new CodeActionToolMetadata
        {
            Name = name,
            Title = title,
            Description = description,
            Behavior = new CodeActionToolBehavior
            {
                Destructive = true,
            },
        };
    }
}
