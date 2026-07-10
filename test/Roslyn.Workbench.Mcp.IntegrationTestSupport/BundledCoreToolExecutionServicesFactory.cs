using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public static class BundledCoreToolExecutionServicesFactory
{
    public static IToolExecutionServices Create()
    {
        return new ToolExecutionServices(
            new DefaultToolRequestResolver(),
            new DefaultCompilerDiagnosticService(),
            new DefaultInspectionContextService(),
            new DefaultProjectStructureService(),
            new DefaultDependencyAnalysisService());
    }
}
