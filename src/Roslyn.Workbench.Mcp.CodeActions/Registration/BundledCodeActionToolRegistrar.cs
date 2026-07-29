using Roslyn.Workbench.Mcp.CodeActions.Contracts.CodeFixes;
using Roslyn.Workbench.Mcp.CodeActions.Contracts.Conversions;
using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Registration;

internal static class BundledCodeActionToolRegistrar
{
    public static void RegisterAll(ICodeActionToolRegistry registry)
    {
        RegisterInfrastructureTools(registry);
        RegisterCodeFixTools(registry);
        RegisterAddTools(registry);
        RegisterConvertTools(registry);
        RegisterExtractAndIntroduceTools(registry);
        RegisterInvertAndMoveTools(registry);
        RegisterRemainingRefactoringTools(registry);
    }

    private static void RegisterCodeFixTools(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool<AddAnonymousTypeMemberNameTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "add-anonymous-type-member-name",
                "Add Anonymous Type Member Name",
                "Adds a generated member name to an invalid anonymous-type member declarator through Roslyn code-fix composition."));

        registry.RegisterMutationTool<AddConditionalInterpolationParenthesesTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "add-conditional-interpolation-parentheses",
                "Add Conditional Interpolation Parentheses",
                "Parenthesises a conditional expression used in an interpolated string through Roslyn code-fix composition."));

        registry.RegisterMutationTool<AddDocumentationCommentNodesTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "add-documentation-comment-nodes",
                "Add Documentation Comment Nodes",
                "Adds missing parameter documentation nodes to an existing XML documentation comment through Roslyn code-fix composition."));

        registry.RegisterMutationTool<AddExplicitCastTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "add-explicit-cast",
                "Add Explicit Cast",
                "Adds the explicit cast required by an invalid implicit conversion through Roslyn code-fix composition."));

        registry.RegisterMutationTool<AddInheritdocTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "add-inheritdoc",
                "Add Inheritdoc",
                "Adds an inheritdoc XML comment to an undocumented inherited member through Roslyn code-fix composition."));

        registry.RegisterMutationTool<AddObsoleteAttributeTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "add-obsolete-attribute",
                "Add Obsolete Attribute",
                "Adds an Obsolete attribute to a declaration that uses or overrides an obsolete API through Roslyn code-fix composition."));

        registry.RegisterMutationTool<AddYieldTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "add-yield",
                "Add Yield",
                "Replaces an invalid iterator return statement with a yield return statement through Roslyn code-fix composition."));

        registry.RegisterMutationTool<AddMissingUsingsTool, AddMissingUsingsRequest>(
            CreateMutationMetadata(
                "add-missing-usings",
                "Add Missing Usings",
                "Adds missing using directives across a selected scope through Roslyn code-fix composition."));

        registry.RegisterMutationTool<AssignOutParametersTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "assign-out-parameters",
                "Assign Out Parameters",
                "Assigns unassigned out parameters at the earliest deterministic location through Roslyn code-fix composition."));

        registry.RegisterMutationTool<DeclareAsNullableTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "declare-as-nullable",
                "Declare As Nullable",
                "Makes the declaration associated with a nullability compiler warning nullable through Roslyn code-fix composition."));

        registry.RegisterMutationTool<DisambiguateSameVariableTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "disambiguate-same-variable",
                "Disambiguate Same Variable",
                "Qualifies a member when an assignment or comparison incorrectly uses the same variable on both sides through Roslyn code-fix composition."));

        registry.RegisterMutationTool<ChangeIteratorReturnTypeTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "change-iterator-return-type",
                "Change Iterator Return Type",
                "Changes an invalid iterator return type to IEnumerable of its yielded value type through Roslyn code-fix composition."));

        registry.RegisterMutationTool<FixIncorrectConstraintTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "fix-incorrect-constraint",
                "Fix Incorrect Constraint",
                "Replaces an invalid enum or delegate generic constraint with its supported constraint form through Roslyn code-fix composition."));

        registry.RegisterMutationTool<FixReturnTypeTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "fix-return-type",
                "Fix Return Type",
                "Changes a void or task-like return type to match the returned expression through Roslyn code-fix composition."));

        registry.RegisterMutationTool<HideBaseMemberTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "hide-base-member",
                "Hide Base Member",
                "Adds the new modifier when a member is intended to hide an inherited member through Roslyn code-fix composition."));

        registry.RegisterMutationTool<MakeMemberRequiredTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "make-member-required",
                "Make Member Required",
                "Adds the required modifier to an uninitialised settable non-nullable member through Roslyn code-fix composition."));

        registry.RegisterMutationTool<MakeMemberStaticTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "make-member-static",
                "Make Member Static",
                "Adds the static modifier to an invalid instance member declared in a static type through Roslyn code-fix composition."));

        registry.RegisterMutationTool<MakeMethodAsynchronousTool, MakeMethodAsynchronousRequest>(
            CreateMutationMetadata(
                "make-method-asynchronous",
                "Make Method Asynchronous",
                "Makes a method or anonymous function asynchronous using the explicitly selected return strategy through Roslyn code-fix composition."));

        registry.RegisterMutationTool<MakeRefStructTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "make-ref-struct",
                "Make Ref Struct",
                "Adds the ref modifier to a struct that contains a ref-like field through Roslyn code-fix composition."));

        registry.RegisterMutationTool<MakeStatementAsynchronousTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "make-statement-asynchronous",
                "Make Statement Asynchronous",
                "Adds await to a foreach or using statement that consumes an asynchronous resource through Roslyn code-fix composition."));

        registry.RegisterMutationTool<MakeTypeAbstractTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "make-type-abstract",
                "Make Type Abstract",
                "Adds the abstract modifier to a type that declares an abstract member through Roslyn code-fix composition."));

        registry.RegisterMutationTool<MakeTypePartialTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "make-type-partial",
                "Make Type Partial",
                "Adds the partial modifier to a type whose other declaration is partial through Roslyn code-fix composition."));

        registry.RegisterMutationTool<OrderModifiersTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "order-modifiers",
                "Order Modifiers",
                "Reorders declaration modifiers into a valid sequence through Roslyn code-fix composition."));

        registry.RegisterMutationTool<PassCapturedVariablesAsArgumentsTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "pass-captured-variables-as-arguments",
                "Pass Captured Variables As Arguments",
                "Passes captured variables as arguments to an invalid static local function through Roslyn code-fix composition."));

        registry.RegisterMutationTool<RemoveInKeywordTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "remove-in-keyword",
                "Remove In Keyword",
                "Removes an invalid in argument modifier through Roslyn code-fix composition."));

        registry.RegisterMutationTool<RemoveDocumentationCommentNodeTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "remove-documentation-comment-node",
                "Remove Documentation Comment Node",
                "Removes a duplicate or unmatched parameter documentation node through Roslyn code-fix composition."));

        registry.RegisterMutationTool<RemoveNewModifierTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "remove-new-modifier",
                "Remove New Modifier",
                "Removes a new modifier that does not hide an accessible inherited member through Roslyn code-fix composition."));

        registry.RegisterMutationTool<RemoveUnusedUsingsTool, RemoveUnusedUsingsRequest>(
            CreateMutationMetadata(
                "remove-unused-usings",
                "Remove Unused Usings",
                "Removes unused using directives across a selected scope through Roslyn code-fix composition."));

        registry.RegisterMutationTool<RemoveUnusedLocalFunctionTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "remove-unused-local-function",
                "Remove Unused Local Function",
                "Removes an unreferenced local function through Roslyn code-fix composition."));

        registry.RegisterMutationTool<ReplaceDefaultLiteralTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "replace-default-literal",
                "Replace Default Literal",
                "Replaces an invalid default literal with the corresponding typed default value through Roslyn code-fix composition."));

        registry.RegisterMutationTool<TransposeRecordKeywordTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "transpose-record-keyword",
                "Transpose Record Keyword",
                "Moves the record keyword into the valid position for a record struct declaration through Roslyn code-fix composition."));

        registry.RegisterMutationTool<UnsealClassTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "unseal-class",
                "Unseal Class",
                "Removes the sealed modifier from a base class that is inherited through Roslyn code-fix composition."));

        registry.RegisterMutationTool<UseExplicitArrayInExpressionTreeTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "use-explicit-array-in-expression-tree",
                "Use Explicit Array In Expression Tree",
                "Wraps expanded params arguments in an explicit array when used in an expression tree through Roslyn code-fix composition."));

        registry.RegisterMutationTool<UseExplicitTypeForConstTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "use-explicit-type-for-const",
                "Use Explicit Type For Const",
                "Replaces var with the inferred explicit type in a constant declaration through Roslyn code-fix composition."));

        registry.RegisterMutationTool<UseInterpolatedVerbatimStringTool, FixedCompilerCodeFixRequest>(
            CreateMutationMetadata(
                "use-interpolated-verbatim-string",
                "Use Interpolated Verbatim String",
                "Corrects the prefix order of an interpolated verbatim string for the configured language version through Roslyn code-fix composition."));
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
                "Lists bounded Roslyn code fixes and refactorings for a document, selection or caret."));

        registry.RegisterQueryTool<PrepareFixAllTool, PrepareFixAllRequest, PrepareFixAllData>(
            CreateMetadata(
                "prepare-fix-all",
                "Prepare Fix All",
                "Revalidates a Code Fix and reports the bounded impact of one explicit Fix All scope without staging changes."));

        registry.RegisterMutationTool<StageCodeActionTool, StageCodeActionRequest>(
            CreateMutationMetadata(
                "stage-code-action",
                "Stage Code Action",
                "Revalidates and stages one selected Code Fix or refactoring action into the active transaction."));

        registry.RegisterMutationTool<StageFixAllTool, StageFixAllRequest>(
            CreateMutationMetadata(
                "stage-fix-all",
                "Stage Fix All",
                "Revalidates one selected code fix and stages its fix-all variant into the active transaction."));
    }

    private static void RegisterAddTools(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool<AddConstructorParametersTool, AddConstructorParametersRequest>(
            CreateMutationMetadata(
                "add-constructor-parameters",
                "Add Constructor Parameters",
                "Adds required or optional constructor parameters for selected fields or properties through Roslyn refactoring composition."));

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
        registry.RegisterMutationTool<GenerateComparisonOperatorsTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "generate-comparison-operators",
                "Generate Comparison Operators",
                "Generates missing comparison operators for an eligible comparable type through Roslyn refactoring composition."));

        registry.RegisterMutationTool<ImplementInterfaceTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "implement-interface",
                "Implement Interface",
                "Implements missing members for one eligible interface through Roslyn refactoring composition."));

        registry.RegisterMutationTool<NameTupleElementTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "name-tuple-element",
                "Name Tuple Element",
                "Adds a supported tuple element name through Roslyn refactoring composition."));

        registry.RegisterMutationTool<OrganizeImportsTool, OrganizeImportsRequest>(
            CreateMutationMetadata(
                "organize-imports",
                "Organize Imports",
                "Sorts imports in one document through Roslyn composition and the document's configured import-order options."));

        registry.RegisterMutationTool<ReplaceConditionalWithStatementsTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "replace-conditional-with-statements",
                "Replace Conditional With Statements",
                "Rewrites a supported conditional expression into statements through Roslyn refactoring composition."));

        registry.RegisterMutationTool<ReplaceMethodWithPropertyTool, ReplaceMethodWithPropertyRequest>(
            CreateMutationMetadata(
                "replace-method-with-property",
                "Replace Method With Property",
                "Replaces an eligible getter, or matching getter and setter, with a property through Roslyn refactoring composition."));

        registry.RegisterMutationTool<ReplacePropertyWithMethodsTool, LocationRefactoringRequest>(
            CreateMutationMetadata(
                "replace-property-with-methods",
                "Replace Property With Methods",
                "Replaces an eligible property and its references with getter and setter methods through Roslyn refactoring composition."));

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
        var behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        };

        return new CodeActionToolMetadata
        {
            Name = name,
            Title = title,
            Description = description,
            Behavior = behavior,
        };
    }
}
