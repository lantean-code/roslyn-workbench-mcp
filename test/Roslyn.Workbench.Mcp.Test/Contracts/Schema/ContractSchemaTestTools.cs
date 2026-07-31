namespace Roslyn.Workbench.Mcp.Test.Contracts.Schema;

internal static class ContractSchemaTestTools
{
    [McpServerTool(Name = "workspace-open", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<WorkspaceStatusData>))]
    public static CallToolResult WorkspaceOpen(WorkspaceOpenRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "workspace-status", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<WorkspaceStatusData>))]
    public static CallToolResult WorkspaceStatus(WorkspaceStatusRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "workspace-list", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<WorkspaceListData>))]
    public static CallToolResult WorkspaceList(WorkspaceListRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "transaction-start", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<TransactionStartData>))]
    public static CallToolResult TransactionStart(TransactionStartRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "schema-probe")]
    public static string SchemaProbe(ContractSchemaProbeRequest request)
    {
        return "SchemaProbe";
    }

    [McpServerTool(Name = "search-symbols", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<SymbolSearchData>))]
    public static CallToolResult SearchSymbols(SearchSymbolsRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "resolve-symbol", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<ResolveSymbolData>))]
    public static CallToolResult ResolveSymbol(ResolveSymbolRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "get-solution-structure", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<SolutionStructureData>))]
    public static CallToolResult GetSolutionStructure(GetSolutionStructureRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "get-type-hierarchy", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<TypeHierarchyData>))]
    public static CallToolResult GetTypeHierarchy(GetTypeHierarchyRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "analyze-control-flow", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<ControlFlowAnalysisData>))]
    public static CallToolResult AnalyzeControlFlow(AnalyzeControlFlowRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "get-control-flow-graph", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<ControlFlowGraphData>))]
    public static CallToolResult GetControlFlowGraph(GetControlFlowGraphRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "rename-symbol", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult RenameSymbol(RenameSymbolRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "list-code-actions", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<CodeActionListData>))]
    public static CallToolResult ListCodeActions(ListCodeActionsRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "prepare-fix-all", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<PrepareFixAllData>))]
    public static CallToolResult PrepareFixAll(PrepareFixAllRequest request)
    {
        return CreateResult(request);
    }

    [McpServerTool(Name = "stage-code-action", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult StageCodeAction(StageCodeActionRequest request)
    {
        return CreateResult(request);
    }

    private static CallToolResult CreateResult<TRequest>(TRequest request)
    {
        return new CallToolResult();
    }
}
