using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;

namespace Roslyn.Workbench.Mcp.TestSupport;

public static class BundledCoreToolExecutionServicesFactory
{
    public static IToolExecutionServices Create()
    {
        return new ToolExecutionServices(
            new DefaultToolRequestResolver(),
            new ReplayCodeActionExecutor(),
            new DefaultCompilerDiagnosticService(),
            new DefaultInspectionContextService(),
            new DefaultProjectStructureService(),
            new DefaultDependencyAnalysisService());
    }
}
