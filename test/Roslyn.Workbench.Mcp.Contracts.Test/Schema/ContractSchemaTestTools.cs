using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Inspection;
using Roslyn.Workbench.Mcp.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.Contracts.Test.Schema;

internal static class ContractSchemaTestTools
{
    [McpServerTool(Name = "workspace-open", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<WorkspaceStatusData>))]
    public static CallToolResult WorkspaceOpen(WorkspaceOpenRequest request)
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

    [McpServerTool(Name = "stage-code-action", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult StageCodeAction(StageCodeActionRequest request)
    {
        _ = request;

        return new CallToolResult();
    }

    [McpServerTool(Name = "stage-code-fix", UseStructuredContent = true, OutputSchemaType = typeof(ToolResult<MutationData>))]
    public static CallToolResult StageCodeFix(StageCodeFixRequest request)
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
}
