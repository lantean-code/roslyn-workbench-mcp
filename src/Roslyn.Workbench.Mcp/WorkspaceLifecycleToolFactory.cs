using ModelContextProtocol.Server;

using Roslyn.Workbench.Mcp.Contracts.Server;

using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp;

internal static class WorkspaceLifecycleToolFactory
{
    public static IReadOnlyList<McpServerTool> Create(IWorkspaceCoordinator coordinator, ToolOutputSchemaMode outputSchemaMode = ToolOutputSchemaMode.Omit)
    {
        return
        [
            new ServerToolMcpServerTool<WorkspaceOpenRequest, WorkspaceOpenData>(
                "workspace-open",
                "Workspace Open",
                "Loads an additional writable workspace.",
                readOnly: false,
                destructive: false,
                outputSchemaMode,
                resultSummary: null,
                (request, _, cancellationToken) => coordinator.OpenAsync(request, cancellationToken)),
            new ServerToolMcpServerTool<WorkspaceListRequest, WorkspaceListData>(
                "workspace-list",
                "Workspace List",
                "Lists the currently loaded workspaces.",
                readOnly: true,
                destructive: false,
                outputSchemaMode,
                resultSummary: null,
                (request, _, cancellationToken) => coordinator.ListAsync(request, cancellationToken)),
            new ServerToolMcpServerTool<WorkspaceCloseRequest, WorkspaceCloseData>(
                "workspace-close",
                "Workspace Close",
                "Closes the selected workspace.",
                readOnly: false,
                destructive: false,
                outputSchemaMode,
                resultSummary: null,
                (request, _, cancellationToken) => coordinator.CloseAsync(request, cancellationToken)),
            new ServerToolMcpServerTool<WorkspaceStatusRequest, WorkspaceStatusData>(
                "workspace-status",
                "Workspace Status",
                "Reports the selected workspace lifecycle state.",
                readOnly: true,
                destructive: false,
                outputSchemaMode,
                resultSummary: null,
                (request, _, cancellationToken) => coordinator.GetStatusAsync(request, cancellationToken)),
            new ServerToolMcpServerTool<WorkspaceReloadRequest, WorkspaceReloadData>(
                "workspace-reload",
                "Workspace Reload",
                "Reloads the selected workspace when it is out of date.",
                readOnly: false,
                destructive: false,
                outputSchemaMode,
                resultSummary: null,
                (request, _, cancellationToken) => coordinator.ReloadAsync(request, cancellationToken)),
        ];
    }
}
