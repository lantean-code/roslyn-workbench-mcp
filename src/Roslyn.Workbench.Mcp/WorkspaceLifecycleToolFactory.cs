using ModelContextProtocol.Server;

using Roslyn.Workbench.Mcp.Contracts.Server;

using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp;

internal static class WorkspaceLifecycleToolFactory
{
    public static IReadOnlyList<McpServerTool> Create(IWorkspaceCoordinator coordinator)
    {
        return
        [
            new ServerToolMcpServerTool<WorkspaceOpenRequest, WorkspaceOpenData>(
                "workspace-open",
                "Workspace Open",
                "Loads a writable workspace.",
                readOnly: false,
                destructive: false,
                (request, _, cancellationToken) => coordinator.OpenAsync(request, cancellationToken)),
            new ServerToolMcpServerTool<EmptyRequest, WorkspaceCloseData>(
                "workspace-close",
                "Workspace Close",
                "Closes the loaded workspace.",
                readOnly: false,
                destructive: false,
                (_, _, cancellationToken) => coordinator.CloseAsync(cancellationToken)),
            new ServerToolMcpServerTool<EmptyRequest, WorkspaceStatusData>(
                "workspace-status",
                "Workspace Status",
                "Reports the current workspace lifecycle state.",
                readOnly: true,
                destructive: false,
                (_, _, cancellationToken) => coordinator.GetStatusAsync(cancellationToken)),
            new ServerToolMcpServerTool<EmptyRequest, WorkspaceReloadData>(
                "workspace-reload",
                "Workspace Reload",
                "Reloads the loaded workspace when it is out of date.",
                readOnly: false,
                destructive: false,
                (_, _, cancellationToken) => coordinator.ReloadAsync(cancellationToken)),
        ];
    }
}
