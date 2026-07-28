namespace Roslyn.Workbench.Mcp.CodeActions.Test.Registration;

public sealed class BundledCodeActionCatalogTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_BundledRegistrar_WHEN_CreatingCatalog_THEN_ShouldPreservePublishedToolContracts()
    {
        var contracts = BundledCodeActionCatalog.Create()
            .OrderBy(static tool => tool.Metadata.Name, StringComparer.Ordinal)
            .Select(static tool =>
                $"{tool.Metadata.Name}|{tool.Metadata.Title}|{tool.Metadata.Description}|{tool.Kind}|{tool.RequestType.Name}|{tool.ResponseType.Name}|{tool.Metadata.Behavior.Destructive}|{tool.Metadata.ResultSummary ?? "<null>"}");

        var snapshot = string.Join(Environment.NewLine, contracts);

        var expected =
            """
            add-anonymous-type-member-name|Add Anonymous Type Member Name|Adds a generated member name to an invalid anonymous-type member declarator through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            add-await|Add Await|Stages one supported add-await refactoring through Roslyn refactoring composition.|Mutation|AddAwaitRequest|MutationData|True|<null>
            add-conditional-interpolation-parentheses|Add Conditional Interpolation Parentheses|Parenthesises a conditional expression used in an interpolated string through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            add-constructor-parameters|Add Constructor Parameters|Adds required or optional constructor parameters for selected fields or properties through Roslyn refactoring composition.|Mutation|AddConstructorParametersRequest|MutationData|True|<null>
            add-debugger-display|Add Debugger Display|Adds a DebuggerDisplay attribute through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            add-documentation-comment-nodes|Add Documentation Comment Nodes|Adds missing parameter documentation nodes to an existing XML documentation comment through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            add-explicit-cast|Add Explicit Cast|Adds the explicit cast required by an invalid implicit conversion through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            add-import|Add Import|Adds a supported using directive through Roslyn refactoring composition.|Mutation|AddImportRequest|MutationData|True|<null>
            add-inheritdoc|Add Inheritdoc|Adds an inheritdoc XML comment to an undocumented inherited member through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            add-missing-usings|Add Missing Usings|Adds missing using directives across a selected scope through Roslyn code-fix composition.|Mutation|AddMissingUsingsRequest|MutationData|True|<null>
            add-null-checks|Add Null Checks|Stages the supported Roslyn parameter null-check refactoring at the selected parameter location.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            add-obsolete-attribute|Add Obsolete Attribute|Adds an Obsolete attribute to a declaration that uses or overrides an obsolete API through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            add-yield|Add Yield|Replaces an invalid iterator return statement with a yield return statement through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            assign-out-parameters|Assign Out Parameters|Assigns unassigned out parameters at the earliest deterministic location through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            change-iterator-return-type|Change Iterator Return Type|Changes an invalid iterator return type to IEnumerable of its yielded value type through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            convert-anonymous-type-to-class|Convert Anonymous Type To Class|Converts a supported anonymous type to a generated class or record through Roslyn refactoring composition.|Mutation|ConvertAnonymousTypeToClassRequest|MutationData|True|<null>
            convert-anonymous-type-to-tuple|Convert Anonymous Type To Tuple|Converts a supported anonymous type to a tuple through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            convert-auto-property-to-full-property|Convert Auto Property To Full Property|Converts a supported auto-property to a full property through Roslyn refactoring composition.|Mutation|ConvertAutoPropertyToFullPropertyRequest|MutationData|True|<null>
            convert-between-regular-and-verbatim-interpolated-string|Convert Between Regular And Verbatim Interpolated String|Converts a supported interpolated string between regular and verbatim forms through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            convert-between-regular-and-verbatim-string|Convert Between Regular And Verbatim String|Converts a supported string literal between regular and verbatim forms through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            convert-direct-cast-to-try-cast|Convert Direct Cast To Try Cast|Converts a supported cast expression to an as-expression through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            convert-expression-body|Convert Expression Body|Stages a supported Roslyn block-body or expression-body conversion at the selected declaration.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            convert-for-to-foreach|Convert For To Foreach|Converts a supported for loop to a foreach loop through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            convert-foreach-linq|Convert Foreach LINQ|Stages one supported Roslyn foreach or LINQ conversion through refactoring composition.|Mutation|ConvertForeachLinqRequest|MutationData|True|<null>
            convert-foreach-to-for|Convert Foreach To For|Converts a supported foreach loop to a for loop through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            convert-if-to-switch|Convert If To Switch|Converts a supported if-chain to a switch statement or switch expression through Roslyn refactoring composition.|Mutation|ConvertIfToSwitchRequest|MutationData|True|<null>
            convert-local-function-to-method|Convert Local Function To Method|Converts a supported local function to a method through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            convert-primary-to-regular-constructor|Convert Primary To Regular Constructor|Converts a supported primary constructor to a regular constructor through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            convert-property|Convert Property|Converts one selected property between supported auto-property and full-property forms through Roslyn composition.|Mutation|ConvertPropertyRequest|MutationData|True|<null>
            convert-to-interpolated-string|Convert To Interpolated String|Converts a supported string expression to an interpolated string through Roslyn refactoring composition.|Mutation|ConvertToInterpolatedStringRequest|MutationData|True|<null>
            convert-to-record|Convert To Record|Converts a supported class declaration to a record through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            convert-try-cast-to-direct-cast|Convert Try Cast To Direct Cast|Converts a supported as-expression to a cast expression through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            declare-as-nullable|Declare As Nullable|Makes the declaration associated with a nullability compiler warning nullable through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            describe-code-action|Describe Code Action|Revalidates one discovered code action and returns its execution descriptor and preflight context.|Query|DescribeCodeActionRequest|DescribeCodeActionData|False|<null>
            disambiguate-same-variable|Disambiguate Same Variable|Qualifies a member when an assignment or comparison incorrectly uses the same variable on both sides through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            encapsulate-field|Encapsulate Field|Encapsulates one field through Roslyn refactoring composition.|Mutation|EncapsulateFieldRequest|MutationData|True|<null>
            extract-method|Extract Method|Extracts a selected statement or expression block through Roslyn refactoring composition.|Mutation|ExtractMethodRequest|MutationData|True|<null>
            fix-incorrect-constraint|Fix Incorrect Constraint|Replaces an invalid enum or delegate generic constraint with its supported constraint form through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            fix-return-type|Fix Return Type|Changes a void or task-like return type to match the returned expression through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            generate-comparison-operators|Generate Comparison Operators|Generates missing comparison operators for an eligible comparable type through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            hide-base-member|Hide Base Member|Adds the new modifier when a member is intended to hide an inherited member through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            implement-interface|Implement Interface|Implements missing members for one eligible interface through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            inline-variable|Inline Variable|Inlines a local variable through Roslyn refactoring composition.|Mutation|InlineVariableRequest|MutationData|True|<null>
            introduce-parameter|Introduce Parameter|Promotes a selected expression to a parameter through Roslyn refactoring composition.|Mutation|IntroduceParameterRequest|MutationData|True|<null>
            introduce-using-statement|Introduce Using Statement|Introduces a supported using statement or declaration through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            introduce-variable|Introduce Variable|Stages one supported Roslyn introduce-variable leaf action through refactoring composition.|Mutation|IntroduceVariableRequest|MutationData|True|<null>
            invert-conditional|Invert Conditional|Inverts a supported conditional expression through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            invert-if|Invert If|Inverts a supported if statement through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            invert-logical|Invert Logical|Inverts a supported logical expression through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            list-code-actions|List Code Actions|Lists bounded Roslyn code fixes and refactorings for a document, selection or caret.|Query|ListCodeActionsRequest|CodeActionListData|False|<null>
            make-local-function-static|Make Local Function Static|Marks a supported local function as static through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            make-member-required|Make Member Required|Adds the required modifier to an uninitialised settable non-nullable member through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            make-member-static|Make Member Static|Adds the static modifier to an invalid instance member declared in a static type through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            make-method-asynchronous|Make Method Asynchronous|Makes a method or anonymous function asynchronous using the explicitly selected return strategy through Roslyn code-fix composition.|Mutation|MakeMethodAsynchronousRequest|MutationData|True|<null>
            make-ref-struct|Make Ref Struct|Adds the ref modifier to a struct that contains a ref-like field through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            make-statement-asynchronous|Make Statement Asynchronous|Adds await to a foreach or using statement that consumes an asynchronous resource through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            make-type-abstract|Make Type Abstract|Adds the abstract modifier to a type that declares an abstract member through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            make-type-partial|Make Type Partial|Adds the partial modifier to a type whose other declaration is partial through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            move-declaration-near-reference|Move Declaration Near Reference|Moves a supported local declaration nearer to its first use through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            move-type-to-file|Move Type To File|Moves one selected type into its own Roslyn-chosen file within the current project.|Mutation|MoveTypeToFileRequest|MutationData|True|<null>
            name-tuple-element|Name Tuple Element|Adds a supported tuple element name through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            order-modifiers|Order Modifiers|Reorders declaration modifiers into a valid sequence through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            organize-imports|Organize Imports|Sorts imports in one document through Roslyn composition and the document's configured import-order options.|Mutation|OrganizeImportsRequest|MutationData|True|<null>
            pass-captured-variables-as-arguments|Pass Captured Variables As Arguments|Passes captured variables as arguments to an invalid static local function through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            remove-documentation-comment-node|Remove Documentation Comment Node|Removes a duplicate or unmatched parameter documentation node through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            remove-in-keyword|Remove In Keyword|Removes an invalid in argument modifier through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            remove-new-modifier|Remove New Modifier|Removes a new modifier that does not hide an accessible inherited member through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            remove-unused-local-function|Remove Unused Local Function|Removes an unreferenced local function through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            remove-unused-usings|Remove Unused Usings|Removes unused using directives across a selected scope through Roslyn code-fix composition.|Mutation|RemoveUnusedUsingsRequest|MutationData|True|<null>
            replace-conditional-with-statements|Replace Conditional With Statements|Rewrites a supported conditional expression into statements through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            replace-default-literal|Replace Default Literal|Replaces an invalid default literal with the corresponding typed default value through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            replace-doc-comment-text-with-tag|Replace Doc Comment Text With Tag|Replaces supported XML doc comment text with a documentation tag through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            replace-method-with-property|Replace Method With Property|Replaces an eligible getter, or matching getter and setter, with a property through Roslyn refactoring composition.|Mutation|ReplaceMethodWithPropertyRequest|MutationData|True|<null>
            replace-property-with-methods|Replace Property With Methods|Replaces an eligible property and its references with getter and setter methods through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            reverse-for-statement|Reverse For Statement|Reverses a supported for-statement loop through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            stage-code-action|Stage Code Action|Revalidates and stages one selected Code Fix or refactoring action into the active transaction.|Mutation|StageCodeActionRequest|MutationData|True|<null>
            stage-fix-all|Stage Fix All|Revalidates one selected code fix and stages its fix-all variant into the active transaction.|Mutation|StageFixAllRequest|MutationData|True|<null>
            transpose-record-keyword|Transpose Record Keyword|Moves the record keyword into the valid position for a record struct declaration through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            unseal-class|Unseal Class|Removes the sealed modifier from a base class that is inherited through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            use-explicit-array-in-expression-tree|Use Explicit Array In Expression Tree|Wraps expanded params arguments in an explicit array when used in an expression tree through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            use-explicit-type|Use Explicit Type|Converts a supported declaration to an explicit type through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            use-explicit-type-for-const|Use Explicit Type For Const|Replaces var with the inferred explicit type in a constant declaration through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            use-implicit-type|Use Implicit Type|Converts a supported declaration to an implicit type through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            use-interpolated-verbatim-string|Use Interpolated Verbatim String|Corrects the prefix order of an interpolated verbatim string for the configured language version through Roslyn code-fix composition.|Mutation|FixedCompilerCodeFixRequest|MutationData|True|<null>
            use-named-arguments|Use Named Arguments|Adds a supported argument name through Roslyn refactoring composition.|Mutation|UseNamedArgumentsRequest|MutationData|True|<null>
            use-recursive-patterns|Use Recursive Patterns|Converts a supported pattern expression to recursive patterns through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            """.ReplaceLineEndings(Environment.NewLine);

        snapshot.Should().Be(expected);
    }

    [Fact]
    public void GIVEN_SupportedBuiltInLedger_WHEN_CreatingCatalog_THEN_ShouldPublishEveryDedicatedTool()
    {
        var expectedDedicatedToolNames = BuiltInCodeActionLedger.Families
            .Where(static family => !string.IsNullOrWhiteSpace(family.ToolName))
            .Select(static family => family.ToolName!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var toolNames = BundledCodeActionCatalog.Create()
            .Select(static tool => tool.Metadata.Name)
            .ToArray();

        var infrastructureToolNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "list-code-actions",
            "describe-code-action",
            "stage-code-action",
            "stage-fix-all",
        };

        var actualDedicatedToolNames = toolNames
            .Where(toolName => !infrastructureToolNames.Contains(toolName))
            .ToArray();

        toolNames.Should().OnlyHaveUniqueItems();
        actualDedicatedToolNames.Should().BeEquivalentTo(expectedDedicatedToolNames);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_DedicatedCodeActionCatalog_WHEN_CheckingAcceptanceManifest_THEN_ShouldRequireCoverageForEveryTool()
    {
        var expectedDedicatedToolNames = BuiltInCodeActionLedger.Families
            .Where(static family => !string.IsNullOrWhiteSpace(family.ToolName))
            .Select(static family => family.ToolName!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static toolName => toolName, StringComparer.Ordinal)
            .ToArray();

        var acceptanceToolNames = LoadAcceptanceToolNames();

        acceptanceToolNames.Should().OnlyHaveUniqueItems();
        acceptanceToolNames.Should().Equal(expectedDedicatedToolNames);
    }

    [Theory]
    [InlineData("list-code-actions")]
    [InlineData("describe-code-action")]
    [InlineData("stage-code-action")]
    [InlineData("stage-fix-all")]
    public void GIVEN_InfrastructureTool_WHEN_CreatingCatalog_THEN_ShouldPublishOutsideDedicatedLedger(string toolName)
    {
        var tools = BundledCodeActionCatalog.Create();

        tools.Should().ContainSingle(tool => tool.Metadata.Name == toolName);
        BuiltInCodeActionLedger.Families
            .Should()
            .NotContain(family => family.ToolName == toolName);
    }

    private static string[] LoadAcceptanceToolNames()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "TestAssets",
            "CodeActionAcceptanceToolNames.txt");

        return File.ReadAllLines(path)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }
}
