using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace;

namespace Roslyn.Workbench.Mcp.TestSupport;

public static class WorkspaceCoordinatorFactory
{
    public static IWorkspaceCoordinator Create(
        WorkspaceCoordinatorOptions options,
        CodeActionRuntime? codeActionRuntime = null,
        IToolExecutionServices? toolExecutionServices = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var runtime = codeActionRuntime ?? new CodeActionRuntime
        {
            CodeActionService = new UnavailableCodeActionService(),
        };

        return new WorkspaceCoordinator(
            Options.Create(options),
            runtime,
            toolExecutionServices ?? new UnavailableToolExecutionServices());
    }
}
