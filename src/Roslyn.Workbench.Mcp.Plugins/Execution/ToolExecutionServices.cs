using Roslyn.Workbench.Mcp.Workspace.References;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

internal sealed class ToolExecutionServices : IToolExecutionServices
{
    public IToolRequestResolver RequestResolver { get; }

    public ICompilerDiagnosticService CompilerDiagnosticService { get; }

    public IInspectionContextService InspectionContextService { get; }

    public IProjectStructureService ProjectStructureService { get; }

    public IProjectTargetFrameworkResolver ProjectTargetFrameworkResolver { get; }

    public IDependencyAnalysisService DependencyAnalysisService { get; }

    public IWorkspaceSelectorFactory WorkspaceSelectorFactory { get; }

    public IReferenceDiscoveryService ReferenceDiscoveryService { get; }

    public ToolExecutionServices(
        IToolRequestResolver requestResolver,
        ICompilerDiagnosticService compilerDiagnosticService,
        IInspectionContextService inspectionContextService,
        IProjectStructureService projectStructureService,
        IProjectTargetFrameworkResolver projectTargetFrameworkResolver,
        IDependencyAnalysisService dependencyAnalysisService,
        IWorkspaceSelectorFactory workspaceSelectorFactory,
        IReferenceDiscoveryService referenceDiscoveryService)
    {
        RequestResolver = requestResolver;
        CompilerDiagnosticService = compilerDiagnosticService;
        InspectionContextService = inspectionContextService;
        ProjectStructureService = projectStructureService;
        ProjectTargetFrameworkResolver = projectTargetFrameworkResolver;
        DependencyAnalysisService = dependencyAnalysisService;
        WorkspaceSelectorFactory = workspaceSelectorFactory;
        ReferenceDiscoveryService = referenceDiscoveryService;
    }
}
