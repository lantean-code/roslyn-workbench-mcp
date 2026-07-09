namespace Roslyn.Workbench.Mcp.Plugins.Core;

internal sealed class ToolExecutionServices : IToolExecutionServices
{
    public ToolExecutionServices(
        IToolRequestResolver requestResolver,
        ICompilerDiagnosticService compilerDiagnosticService,
        IInspectionContextService inspectionContextService,
        IProjectStructureService projectStructureService,
        IDependencyAnalysisService dependencyAnalysisService)
    {
        RequestResolver = requestResolver;
        CompilerDiagnosticService = compilerDiagnosticService;
        InspectionContextService = inspectionContextService;
        ProjectStructureService = projectStructureService;
        DependencyAnalysisService = dependencyAnalysisService;
    }

    public IToolRequestResolver RequestResolver { get; }

    public ICompilerDiagnosticService CompilerDiagnosticService { get; }

    public IInspectionContextService InspectionContextService { get; }

    public IProjectStructureService ProjectStructureService { get; }

    public IDependencyAnalysisService DependencyAnalysisService { get; }
}
