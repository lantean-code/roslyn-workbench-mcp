using Roslyn.Workbench.Mcp.CodeActions.Contracts.CodeFixes;
using Roslyn.Workbench.Mcp.CodeActions.Contracts.Conversions;
using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.Test.Contracts.Schema;

internal static class ContractSchemaTestTools
{
    [McpServerTool(Name = "workspace-open", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<WorkspaceStatusData>))]
    public static CallToolResult WorkspaceOpen(WorkspaceOpenRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "workspace-status", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<WorkspaceStatusData>))]
    public static CallToolResult WorkspaceStatus(WorkspaceStatusRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "workspace-list", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<WorkspaceListData>))]
    public static CallToolResult WorkspaceList(WorkspaceListRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "transaction-start", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<TransactionStartData>))]
    public static CallToolResult TransactionStart(TransactionStartRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "schema-probe")]
    public static string SchemaProbe(ContractSchemaProbeRequest request)
    {
        _ = request;

        return "SchemaProbe";
    }

    [McpServerTool(Name = "search-symbols", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<SymbolSearchData>))]
    public static CallToolResult SearchSymbols(SearchSymbolsRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "resolve-symbol", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<ResolveSymbolData>))]
    public static CallToolResult ResolveSymbol(ResolveSymbolRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "get-solution-structure", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<SolutionStructureData>))]
    public static CallToolResult GetSolutionStructure(GetSolutionStructureRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "get-type-hierarchy", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<TypeHierarchyData>))]
    public static CallToolResult GetTypeHierarchy(GetTypeHierarchyRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "analyze-control-flow", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<ControlFlowAnalysisData>))]
    public static CallToolResult AnalyzeControlFlow(AnalyzeControlFlowRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "get-control-flow-graph", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<ControlFlowGraphData>))]
    public static CallToolResult GetControlFlowGraph(GetControlFlowGraphRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "rename-symbol", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult RenameSymbol(RenameSymbolRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "list-code-actions", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<CodeActionListData>))]
    public static CallToolResult ListCodeActions(ListCodeActionsRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "prepare-fix-all", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<PrepareFixAllData>))]
    public static CallToolResult PrepareFixAll(PrepareFixAllRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "describe-code-action", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<DescribeCodeActionData>))]
    public static CallToolResult DescribeCodeAction(DescribeCodeActionRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "stage-code-action", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult StageCodeAction(StageCodeActionRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "stage-fix-all", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult StageFixAll(StageFixAllRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "remove-unused-usings", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult RemoveUnusedUsings(RemoveUnusedUsingsRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "add-missing-usings", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult AddMissingUsings(AddMissingUsingsRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "add-explicit-cast", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult AddExplicitCast(FixedCompilerCodeFixRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "add-debugger-display", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult AddDebuggerDisplay(LocationRefactoringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "add-import", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult AddImport(AddImportRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "inline-variable", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult InlineVariable(InlineVariableRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "convert-to-interpolated-string", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ConvertToInterpolatedString(ConvertToInterpolatedStringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "convert-anonymous-type-to-class", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ConvertAnonymousTypeToClass(ConvertAnonymousTypeToClassRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "convert-auto-property-to-full-property", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ConvertAutoPropertyToFullProperty(ConvertAutoPropertyToFullPropertyRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "convert-between-regular-and-verbatim-interpolated-string", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ConvertBetweenRegularAndVerbatimInterpolatedString(LocationRefactoringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "convert-between-regular-and-verbatim-string", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ConvertBetweenRegularAndVerbatimString(LocationRefactoringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "convert-direct-cast-to-try-cast", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ConvertDirectCastToTryCast(LocationRefactoringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "extract-method", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ExtractMethod(ExtractMethodRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "introduce-parameter", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult IntroduceParameter(IntroduceParameterRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "encapsulate-field", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult EncapsulateField(EncapsulateFieldRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "convert-foreach-linq", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ConvertForeachLinq(ConvertForeachLinqRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "convert-local-function-to-method", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ConvertLocalFunctionToMethod(LocationRefactoringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "convert-primary-to-regular-constructor", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ConvertPrimaryToRegularConstructor(LocationRefactoringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "convert-try-cast-to-direct-cast", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ConvertTryCastToDirectCast(LocationRefactoringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "introduce-variable", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult IntroduceVariable(IntroduceVariableRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "name-tuple-element", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult NameTupleElement(LocationRefactoringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "replace-conditional-with-statements", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ReplaceConditionalWithStatements(LocationRefactoringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "replace-doc-comment-text-with-tag", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ReplaceDocCommentTextWithTag(LocationRefactoringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "reverse-for-statement", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ReverseForStatement(LocationRefactoringRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "move-type-to-file", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult MoveTypeToFile(MoveTypeToFileRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "convert-property", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult ConvertProperty(ConvertPropertyRequest request)
    {
        _ = request;

        return new CallToolResult();
    }
}
