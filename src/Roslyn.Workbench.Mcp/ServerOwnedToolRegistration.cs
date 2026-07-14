using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp;

internal static class ServerOwnedToolRegistration
{
    public const int ToolCount = 11;

    public const string ServerStatusName = "server-status";
    public const string TransactionCommitName = "transaction-commit";
    public const string TransactionHistoryName = "transaction-history";
    public const string TransactionPreviewName = "transaction-preview";
    public const string TransactionRollbackName = "transaction-rollback";
    public const string TransactionStartName = "transaction-start";
    public const string WorkspaceCloseName = "workspace-close";
    public const string WorkspaceListName = "workspace-list";
    public const string WorkspaceOpenName = "workspace-open";
    public const string WorkspaceReloadName = "workspace-reload";
    public const string WorkspaceStatusName = "workspace-status";

    public static IReadOnlySet<string> ToolNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ServerStatusName,
        TransactionCommitName,
        TransactionHistoryName,
        TransactionPreviewName,
        TransactionRollbackName,
        TransactionStartName,
        WorkspaceCloseName,
        WorkspaceListName,
        WorkspaceOpenName,
        WorkspaceReloadName,
        WorkspaceStatusName,
    };

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
