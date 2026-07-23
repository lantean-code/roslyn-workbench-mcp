using Roslyn.Workbench.Mcp.Workspace.Contracts.Caching;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class ToolExecutionServices : IToolExecutionServices
{
    public ToolExecutionServices(
        IToolRequestResolver requestResolver,
        ICompilerDiagnosticService compilerDiagnosticService,
        IInspectionContextService inspectionContextService,
        IProjectStructureService projectStructureService,
        IProjectTargetFrameworkResolver projectTargetFrameworkResolver,
        IDependencyAnalysisService dependencyAnalysisService,
        IQueryCache queryCache)
    {
        RequestResolver = requestResolver;
        CompilerDiagnosticService = compilerDiagnosticService;
        InspectionContextService = inspectionContextService;
        ProjectStructureService = projectStructureService;
        ProjectTargetFrameworkResolver = projectTargetFrameworkResolver;
        DependencyAnalysisService = dependencyAnalysisService;
        QueryCache = queryCache;
    }

    public IToolRequestResolver RequestResolver { get; }

    public ICompilerDiagnosticService CompilerDiagnosticService { get; }

    public IInspectionContextService InspectionContextService { get; }

    public IProjectStructureService ProjectStructureService { get; }

    public IProjectTargetFrameworkResolver ProjectTargetFrameworkResolver { get; }

    public IDependencyAnalysisService DependencyAnalysisService { get; }

    public IQueryCache QueryCache { get; }
}
