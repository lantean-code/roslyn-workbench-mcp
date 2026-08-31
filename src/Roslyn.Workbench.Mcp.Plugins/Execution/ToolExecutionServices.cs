using Roslyn.Workbench.Mcp.Workspace.Hierarchy;
using Roslyn.Workbench.Mcp.Workspace.References;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

/// <summary>
/// Aggregates the stable host services exposed to plugin handlers for one server instance.
/// </summary>
internal sealed class ToolExecutionServices : IToolExecutionServices
{
    /// <inheritdoc/>
    public IToolRequestResolver RequestResolver { get; }

    /// <inheritdoc/>
    public ICompilerDiagnosticService CompilerDiagnosticService { get; }

    /// <inheritdoc/>
    public IInspectionContextService InspectionContextService { get; }

    /// <inheritdoc/>
    public IProjectStructureService ProjectStructureService { get; }

    /// <inheritdoc/>
    public IProjectTargetFrameworkResolver ProjectTargetFrameworkResolver { get; }

    /// <inheritdoc/>
    public IDependencyAnalysisService DependencyAnalysisService { get; }

    /// <inheritdoc/>
    public IWorkspaceSelectorFactory WorkspaceSelectorFactory { get; }

    /// <inheritdoc/>
    public IReferenceDiscoveryService ReferenceDiscoveryService { get; }

    /// <inheritdoc/>
    public ITypeHierarchyService TypeHierarchyService { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolExecutionServices"/> class.
    /// </summary>
    /// <param name="requestResolver">The service that resolves request selectors.</param>
    /// <param name="compilerDiagnosticService">The service that collects compiler diagnostics.</param>
    /// <param name="inspectionContextService">The service that projects source context and containing symbols.</param>
    /// <param name="projectStructureService">The service that inspects solution and project structure.</param>
    /// <param name="projectTargetFrameworkResolver">The service that resolves project target frameworks.</param>
    /// <param name="dependencyAnalysisService">The service that analyzes dependency graphs, cycles and test impact.</param>
    /// <param name="workspaceSelectorFactory">The factory that creates canonical workspace selectors.</param>
    /// <param name="referenceDiscoveryService">The service that discovers symbol references.</param>
    /// <param name="typeHierarchyService">The service that inspects type hierarchies.</param>
    public ToolExecutionServices(
        IToolRequestResolver requestResolver,
        ICompilerDiagnosticService compilerDiagnosticService,
        IInspectionContextService inspectionContextService,
        IProjectStructureService projectStructureService,
        IProjectTargetFrameworkResolver projectTargetFrameworkResolver,
        IDependencyAnalysisService dependencyAnalysisService,
        IWorkspaceSelectorFactory workspaceSelectorFactory,
        IReferenceDiscoveryService referenceDiscoveryService,
        ITypeHierarchyService typeHierarchyService)
    {
        RequestResolver = requestResolver;
        CompilerDiagnosticService = compilerDiagnosticService;
        InspectionContextService = inspectionContextService;
        ProjectStructureService = projectStructureService;
        ProjectTargetFrameworkResolver = projectTargetFrameworkResolver;
        DependencyAnalysisService = dependencyAnalysisService;
        WorkspaceSelectorFactory = workspaceSelectorFactory;
        ReferenceDiscoveryService = referenceDiscoveryService;
        TypeHierarchyService = typeHierarchyService;
    }
}
