using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;

namespace Roslyn.Workbench.Mcp.TestSupport;

public static class BundledCoreToolExecutionServicesFactory
{
    public static IToolExecutionServices Create()
    {
        var resultShaper = new DefaultToolResultShaper();
        var requestResolver = new DefaultToolRequestResolver(resultShaper);

        return new ToolExecutionServices(
            requestResolver,
            resultShaper,
            new ReplayCodeActionExecutor(resultShaper),
            new DefaultCompilerDiagnosticService(),
            new DefaultInspectionContextService(),
            new DefaultProjectStructureService(),
            new DefaultDependencyAnalysisService());
    }
}
