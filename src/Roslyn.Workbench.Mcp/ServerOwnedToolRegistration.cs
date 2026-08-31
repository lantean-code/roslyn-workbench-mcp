using Roslyn.Workbench.Mcp.ErrorReporting.Tools;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp;

/// <summary>
/// Defines the Host-owned tool names and registers their implementations.
/// </summary>
internal static class ServerOwnedToolRegistration
{
    /// <summary>
    /// Defines the number of Host-owned tools published when optional reporting tools are disabled.
    /// </summary>
    public const int BaseToolCount = 12;

    /// <summary>
    /// Defines the published name of the error-details tool.
    /// </summary>
    public const string GetErrorDetailsName = "get-error-details";
    /// <summary>
    /// Defines the published name of the error-report preparation tool.
    /// </summary>
    public const string PrepareErrorReportName = "prepare-error-report";
    /// <summary>
    /// Defines the published name of the server-status tool.
    /// </summary>
    public const string ServerStatusName = "server-status";
    /// <summary>
    /// Defines the published name of the error-report submission tool.
    /// </summary>
    public const string SubmitErrorReportName = "submit-error-report";
    /// <summary>
    /// Defines the published name of the transaction-commit tool.
    /// </summary>
    public const string TransactionCommitName = "transaction-commit";
    /// <summary>
    /// Defines the published name of the transaction-history tool.
    /// </summary>
    public const string TransactionHistoryName = "transaction-history";
    /// <summary>
    /// Defines the published name of the transaction-preview tool.
    /// </summary>
    public const string TransactionPreviewName = "transaction-preview";
    /// <summary>
    /// Defines the published name of the transaction-rollback tool.
    /// </summary>
    public const string TransactionRollbackName = "transaction-rollback";
    /// <summary>
    /// Defines the published name of the transaction-start tool.
    /// </summary>
    public const string TransactionStartName = "transaction-start";
    /// <summary>
    /// Defines the published name of the workspace-close tool.
    /// </summary>
    public const string WorkspaceCloseName = "workspace-close";
    /// <summary>
    /// Defines the published name of the workspace-list tool.
    /// </summary>
    public const string WorkspaceListName = "workspace-list";
    /// <summary>
    /// Defines the published name of the workspace-open tool.
    /// </summary>
    public const string WorkspaceOpenName = "workspace-open";
    /// <summary>
    /// Defines the published name of the workspace-reload tool.
    /// </summary>
    public const string WorkspaceReloadName = "workspace-reload";
    /// <summary>
    /// Defines the published name of the workspace-status tool.
    /// </summary>
    public const string WorkspaceStatusName = "workspace-status";

    /// <summary>
    /// Gets every reserved Host-owned tool name, including optional reporting tools.
    /// </summary>
    public static IReadOnlySet<string> ToolNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        GetErrorDetailsName,
        PrepareErrorReportName,
        ServerStatusName,
        SubmitErrorReportName,
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

    /// <summary>
    /// Calculates the number of Host-owned tools enabled by the error-reporting configuration.
    /// </summary>
    /// <param name="options">The error-reporting settings that control optional tool publication.</param>
    /// <returns>The number of tools that will be published.</returns>
    public static int GetPublishedToolCount(ErrorReportingOptions options)
    {
        return BaseToolCount + (options.AreReportingToolsEnabled ? 2 : 0);
    }

    /// <summary>
    /// Registers the standard Host-owned tools without optional error-report submission tools.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public static void AddMcpTools(IServiceCollection services)
    {
        AddMcpTools(services, new ErrorReportingOptions());
    }

    /// <summary>
    /// Registers Host-owned tools, including optional error-report tools when enabled.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="errorReportingOptions">The settings that control publication of error-report tools.</param>
    public static void AddMcpTools(IServiceCollection services, ErrorReportingOptions errorReportingOptions)
    {
        services.AddSingleton<McpServerTool, GetErrorDetailsTool>();
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

        if (errorReportingOptions.AreReportingToolsEnabled)
        {
            services.AddSingleton<McpServerTool, PrepareErrorReportTool>();
            services.AddSingleton<McpServerTool, SubmitErrorReportTool>();
        }
    }
}
