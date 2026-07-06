using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp;

internal static class ServerOwnedToolRegistration
{
    public const int ToolCount = 11;

    public static void AddMcpTools(IServiceCollection services)
    {
        services.AddSingleton<McpServerTool, ServerStatusTool>();
        services.AddSingleton<McpServerTool, WorkspaceOpenTool>();
        services.AddSingleton<McpServerTool, WorkspaceListTool>();
        services.AddSingleton<McpServerTool, WorkspaceCloseTool>();
        services.AddSingleton<McpServerTool, WorkspaceStatusTool>();
        services.AddSingleton<McpServerTool, WorkspaceReloadTool>();
        services.AddSingleton<McpServerTool, TransactionStartTool>();
        services.AddSingleton<McpServerTool, TransactionPreviewTool>();
        services.AddSingleton<McpServerTool, TransactionHistoryTool>();
        services.AddSingleton<McpServerTool, TransactionCommitTool>();
        services.AddSingleton<McpServerTool, TransactionRollbackTool>();
    }
}
