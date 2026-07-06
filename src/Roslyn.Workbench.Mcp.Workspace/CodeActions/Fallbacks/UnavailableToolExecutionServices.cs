using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace.CodeActions.Fallbacks;

internal sealed class UnavailableToolExecutionServices : IToolExecutionServices
{
    public UnavailableToolExecutionServices()
    {
        RequestResolver = new UnavailableToolRequestResolver();
        ResultShaper = new UnavailableToolResultShaper();
        ReplayCodeActionExecutor = new UnavailableReplayCodeActionExecutor();
        CompilerDiagnosticService = new UnavailableCompilerDiagnosticService();
        InspectionContextService = new UnavailableInspectionContextService();
        ProjectStructureService = new UnavailableProjectStructureService();
        DependencyAnalysisService = new UnavailableDependencyAnalysisService();
    }

    public IToolRequestResolver RequestResolver { get; }

    public IToolResultShaper ResultShaper { get; }

    public IReplayCodeActionExecutor ReplayCodeActionExecutor { get; }

    public ICompilerDiagnosticService CompilerDiagnosticService { get; }

    public IInspectionContextService InspectionContextService { get; }

    public IProjectStructureService ProjectStructureService { get; }

    public IDependencyAnalysisService DependencyAnalysisService { get; }
}
