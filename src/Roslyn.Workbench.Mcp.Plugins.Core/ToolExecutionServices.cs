namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class ToolExecutionServices : IToolExecutionServices
{
    public ToolExecutionServices(
        IToolRequestResolver requestResolver,
        IToolResultShaper resultShaper,
        IReplayCodeActionExecutor replayCodeActionExecutor,
        ICompilerDiagnosticService compilerDiagnosticService,
        IInspectionContextService inspectionContextService,
        IProjectStructureService projectStructureService,
        IDependencyAnalysisService dependencyAnalysisService)
    {
        RequestResolver = requestResolver;
        ResultShaper = resultShaper;
        ReplayCodeActionExecutor = replayCodeActionExecutor;
        CompilerDiagnosticService = compilerDiagnosticService;
        InspectionContextService = inspectionContextService;
        ProjectStructureService = projectStructureService;
        DependencyAnalysisService = dependencyAnalysisService;
    }

    public IToolRequestResolver RequestResolver { get; }

    public IToolResultShaper ResultShaper { get; }

    public IReplayCodeActionExecutor ReplayCodeActionExecutor { get; }

    public ICompilerDiagnosticService CompilerDiagnosticService { get; }

    public IInspectionContextService InspectionContextService { get; }

    public IProjectStructureService ProjectStructureService { get; }

    public IDependencyAnalysisService DependencyAnalysisService { get; }
}
