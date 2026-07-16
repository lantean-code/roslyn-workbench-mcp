namespace Roslyn.Workbench.Mcp.CodeActions.Test;

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
            add-await|Add Await|Stages one supported add-await refactoring through Roslyn refactoring composition.|Mutation|AddAwaitRequest|MutationData|True|<null>
            add-debugger-display|Add Debugger Display|Adds a DebuggerDisplay attribute through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            add-import|Add Import|Adds a supported using directive through Roslyn refactoring composition.|Mutation|AddImportRequest|MutationData|True|<null>
            add-missing-usings|Add Missing Usings|Adds missing using directives across a selected scope through Roslyn code-fix composition.|Mutation|AddMissingUsingsRequest|MutationData|True|<null>
            add-null-checks|Add Null Checks|Stages the supported Roslyn parameter null-check refactoring at the selected parameter location.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
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
            describe-code-action|Describe Code Action|Revalidates one discovered code action and returns its execution descriptor and preflight context.|Query|DescribeCodeActionRequest|DescribeCodeActionData|False|<null>
            encapsulate-field|Encapsulate Field|Encapsulates one field through Roslyn refactoring composition.|Mutation|EncapsulateFieldRequest|MutationData|True|<null>
            extract-method|Extract Method|Extracts a selected statement or expression block through Roslyn refactoring composition.|Mutation|ExtractMethodRequest|MutationData|True|<null>
            inline-variable|Inline Variable|Inlines a local variable through Roslyn refactoring composition.|Mutation|InlineVariableRequest|MutationData|True|<null>
            introduce-parameter|Introduce Parameter|Promotes a selected expression to a parameter through Roslyn refactoring composition.|Mutation|IntroduceParameterRequest|MutationData|True|<null>
            introduce-using-statement|Introduce Using Statement|Introduces a supported using statement or declaration through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            introduce-variable|Introduce Variable|Stages one supported Roslyn introduce-variable leaf action through refactoring composition.|Mutation|IntroduceVariableRequest|MutationData|True|<null>
            invert-conditional|Invert Conditional|Inverts a supported conditional expression through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            invert-if|Invert If|Inverts a supported if statement through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            invert-logical|Invert Logical|Inverts a supported logical expression through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            list-code-actions|List Code Actions|Lists applicable code actions and code fixes at a target location.|Query|ListCodeActionsRequest|CodeActionListData|False|<null>
            make-local-function-static|Make Local Function Static|Marks a supported local function as static through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            move-declaration-near-reference|Move Declaration Near Reference|Moves a supported local declaration nearer to its first use through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            move-type-to-file|Move Type To File|Moves one selected type into its own Roslyn-chosen file within the current project.|Mutation|MoveTypeToFileRequest|MutationData|True|<null>
            name-tuple-element|Name Tuple Element|Adds a supported tuple element name through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            remove-unused-usings|Remove Unused Usings|Removes unused using directives across a selected scope through Roslyn code-fix composition.|Mutation|RemoveUnusedUsingsRequest|MutationData|True|<null>
            replace-conditional-with-statements|Replace Conditional With Statements|Rewrites a supported conditional expression into statements through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            replace-doc-comment-text-with-tag|Replace Doc Comment Text With Tag|Replaces supported XML doc comment text with a documentation tag through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            reverse-for-statement|Reverse For Statement|Reverses a supported for-statement loop through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            stage-code-action|Stage Code Action|Revalidates and stages one selected refactoring action into the active transaction.|Mutation|StageCodeActionRequest|MutationData|True|<null>
            stage-code-fix|Stage Code Fix|Revalidates and stages one selected code fix into the active transaction.|Mutation|StageCodeFixRequest|MutationData|True|<null>
            stage-fix-all|Stage Fix All|Revalidates one selected code fix and stages its fix-all variant into the active transaction.|Mutation|StageFixAllRequest|MutationData|True|<null>
            use-explicit-type|Use Explicit Type|Converts a supported declaration to an explicit type through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            use-implicit-type|Use Implicit Type|Converts a supported declaration to an implicit type through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            use-named-arguments|Use Named Arguments|Adds a supported argument name through Roslyn refactoring composition.|Mutation|UseNamedArgumentsRequest|MutationData|True|<null>
            use-recursive-patterns|Use Recursive Patterns|Converts a supported pattern expression to recursive patterns through Roslyn refactoring composition.|Mutation|LocationRefactoringRequest|MutationData|True|<null>
            """.ReplaceLineEndings(Environment.NewLine);

        snapshot.Should().Be(expected);
    }

    [Fact]
    public void GIVEN_BuiltInLedger_WHEN_CreatingCatalog_THEN_ShouldPublishExactlyTheVisibleDedicatedTools()
    {
        var dedicatedFamilies = BuiltInCodeActionLedger.Families
            .Where(static family => !string.IsNullOrWhiteSpace(family.ToolName))
            .GroupBy(static family => family.ToolName ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        var toolNames = BundledCodeActionCatalog.Create()
            .Select(static tool => tool.Metadata.Name)
            .ToArray();

        var visibleToolNames = dedicatedFamilies
            .Where(static group => group.Any(static family => family.IsDedicatedToolVisible))
            .Select(static group => group.Key);
        var hiddenToolNames = dedicatedFamilies
            .Where(static group => group.All(static family => !family.IsDedicatedToolVisible))
            .Select(static group => group.Key);
        toolNames.Should().OnlyHaveUniqueItems();
        toolNames.Should().Contain(visibleToolNames);
        toolNames.Intersect(hiddenToolNames, StringComparer.Ordinal).Should().BeEmpty();
    }

    [Theory]
    [InlineData("list-code-actions")]
    [InlineData("describe-code-action")]
    [InlineData("stage-code-action")]
    [InlineData("stage-code-fix")]
    [InlineData("stage-fix-all")]
    public void GIVEN_InfrastructureTool_WHEN_CreatingCatalog_THEN_ShouldPublishOutsideDedicatedLedger(string toolName)
    {
        var tools = BundledCodeActionCatalog.Create();

        tools.Should().ContainSingle(tool => tool.Metadata.Name == toolName);
        BuiltInCodeActionLedger.IsDedicatedTool(toolName).Should().BeFalse();
    }
}
