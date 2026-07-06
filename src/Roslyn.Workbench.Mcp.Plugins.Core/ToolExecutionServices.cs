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
        RequestResolver = requestResolver ?? throw new ArgumentNullException(nameof(requestResolver));
        ResultShaper = resultShaper ?? throw new ArgumentNullException(nameof(resultShaper));
        ReplayCodeActionExecutor = replayCodeActionExecutor ?? throw new ArgumentNullException(nameof(replayCodeActionExecutor));
        CompilerDiagnosticService = compilerDiagnosticService ?? throw new ArgumentNullException(nameof(compilerDiagnosticService));
        InspectionContextService = inspectionContextService ?? throw new ArgumentNullException(nameof(inspectionContextService));
        ProjectStructureService = projectStructureService ?? throw new ArgumentNullException(nameof(projectStructureService));
        DependencyAnalysisService = dependencyAnalysisService ?? throw new ArgumentNullException(nameof(dependencyAnalysisService));
    }

    public IToolRequestResolver RequestResolver { get; }

    public IToolResultShaper ResultShaper { get; }

    public IReplayCodeActionExecutor ReplayCodeActionExecutor { get; }

    public ICompilerDiagnosticService CompilerDiagnosticService { get; }

    public IInspectionContextService InspectionContextService { get; }

    public IProjectStructureService ProjectStructureService { get; }

    public IDependencyAnalysisService DependencyAnalysisService { get; }
}
