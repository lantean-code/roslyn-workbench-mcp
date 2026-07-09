using Roslyn.Workbench.Mcp.Plugins.Core;

namespace Roslyn.Workbench.Mcp.TestSupport;

internal sealed class ToolExecutionServicesBuilder
{
    private IToolRequestResolver? _requestResolver;
    private ICompilerDiagnosticService? _compilerDiagnosticService;
    private IInspectionContextService? _inspectionContextService;
    private IProjectStructureService? _projectStructureService;
    private IDependencyAnalysisService? _dependencyAnalysisService;

    public ToolExecutionServicesBuilder WithRequestResolver(IToolRequestResolver requestResolver)
    {
        _requestResolver = requestResolver ?? throw new ArgumentNullException(nameof(requestResolver));
        return this;
    }

    public ToolExecutionServicesBuilder WithCompilerDiagnosticService(ICompilerDiagnosticService compilerDiagnosticService)
    {
        _compilerDiagnosticService = compilerDiagnosticService ?? throw new ArgumentNullException(nameof(compilerDiagnosticService));
        return this;
    }

    public ToolExecutionServicesBuilder WithInspectionContextService(IInspectionContextService inspectionContextService)
    {
        _inspectionContextService = inspectionContextService ?? throw new ArgumentNullException(nameof(inspectionContextService));
        return this;
    }

    public ToolExecutionServicesBuilder WithProjectStructureService(IProjectStructureService projectStructureService)
    {
        _projectStructureService = projectStructureService ?? throw new ArgumentNullException(nameof(projectStructureService));
        return this;
    }

    public ToolExecutionServicesBuilder WithDependencyAnalysisService(IDependencyAnalysisService dependencyAnalysisService)
    {
        _dependencyAnalysisService = dependencyAnalysisService ?? throw new ArgumentNullException(nameof(dependencyAnalysisService));
        return this;
    }

    public IToolExecutionServices Build()
    {
        var requestResolver = _requestResolver ?? new DefaultToolRequestResolver();
        var compilerDiagnosticService = _compilerDiagnosticService ?? new DefaultCompilerDiagnosticService();
        var inspectionContextService = _inspectionContextService ?? new DefaultInspectionContextService();
        var projectStructureService = _projectStructureService ?? new DefaultProjectStructureService();
        var dependencyAnalysisService = _dependencyAnalysisService ?? new DefaultDependencyAnalysisService();

        return new ToolExecutionServices(
            requestResolver,
            compilerDiagnosticService,
            inspectionContextService,
            projectStructureService,
            dependencyAnalysisService);
    }
}
