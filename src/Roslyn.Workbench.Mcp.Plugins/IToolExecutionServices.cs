using Roslyn.Workbench.Mcp.Workspace.References;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Provides host-composed execution services shared by plugin tools.
/// </summary>
public interface IToolExecutionServices
{
    /// <summary>
    /// Gets the factory for projecting resolved workspace identities into replayable selectors.
    /// </summary>
    IWorkspaceSelectorFactory WorkspaceSelectorFactory { get; }

    /// <summary>
    /// Gets the service for discovering unique source references within selected documents.
    /// </summary>
    IReferenceDiscoveryService ReferenceDiscoveryService { get; }

    /// <summary>
    /// Gets the request-resolution service for tool execution.
    /// </summary>
    IToolRequestResolver RequestResolver { get; }

    /// <summary>
    /// Gets the compiler-diagnostic service for tool execution.
    /// </summary>
    ICompilerDiagnosticService CompilerDiagnosticService { get; }

    /// <summary>
    /// Gets the inspection-context service for tool execution.
    /// </summary>
    IInspectionContextService InspectionContextService { get; }

    /// <summary>
    /// Gets the project-structure service for tool execution.
    /// </summary>
    IProjectStructureService ProjectStructureService { get; }

    /// <summary>
    /// Gets the snapshot-scoped target-framework resolver for tool execution.
    /// </summary>
    IProjectTargetFrameworkResolver ProjectTargetFrameworkResolver { get; }

    /// <summary>
    /// Gets the dependency analysis service for tool execution.
    /// </summary>
    IDependencyAnalysisService DependencyAnalysisService { get; }
}
