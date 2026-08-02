using Roslyn.Workbench.Mcp.Workspace.Hierarchy;
using Roslyn.Workbench.Mcp.Workspace.References;

namespace Roslyn.Workbench.Mcp.TestSupport;

/// <summary>
/// Holds the visible mocks connected for a query tool unit test.
/// </summary>
public sealed record QueryContextMockGraph
{
    /// <summary>
    /// Gets the query context mock.
    /// </summary>
    public Mock<IQueryContext> QueryContext { get; }

    /// <summary>
    /// Gets the workspace resolver mock.
    /// </summary>
    public Mock<IWorkspaceResolver> WorkspaceResolver { get; }

    /// <summary>
    /// Gets the workspace path service mock.
    /// </summary>
    public Mock<IWorkspacePathService> WorkspacePathService { get; }

    /// <summary>
    /// Gets the tool execution services mock.
    /// </summary>
    public Mock<IToolExecutionServices> ToolExecutionServices { get; }

    /// <summary>
    /// Gets the tool request resolver mock.
    /// </summary>
    public Mock<IToolRequestResolver> RequestResolver { get; }

    /// <summary>
    /// Gets the project target-framework resolver mock.
    /// </summary>
    public Mock<IProjectTargetFrameworkResolver> ProjectTargetFrameworkResolver { get; }

    /// <summary>
    /// Gets the Workspace selector factory mock.
    /// </summary>
    public Mock<IWorkspaceSelectorFactory> WorkspaceSelectorFactory { get; }

    /// <summary>
    /// Gets the invocation query-result cache mock.
    /// </summary>
    public Mock<IQueryResultCache> QueryResultCache { get; }

    /// <summary>
    /// Gets the reference discovery service mock.
    /// </summary>
    public Mock<IReferenceDiscoveryService> ReferenceDiscoveryService { get; }

    /// <summary>
    /// Gets the type-hierarchy service mock.
    /// </summary>
    public Mock<ITypeHierarchyService> TypeHierarchyService { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryContextMockGraph"/> class.
    /// </summary>
    /// <param name="queryContext">The query context mock.</param>
    /// <param name="workspaceResolver">The workspace resolver mock.</param>
    /// <param name="workspacePathService">The workspace path service mock.</param>
    /// <param name="toolExecutionServices">The tool execution services mock.</param>
    /// <param name="requestResolver">The tool request resolver mock.</param>
    /// <param name="projectTargetFrameworkResolver">The project target-framework resolver mock.</param>
    /// <param name="workspaceSelectorFactory">The Workspace selector factory mock.</param>
    /// <param name="queryResultCache">The invocation query-result cache mock.</param>
    /// <param name="referenceDiscoveryService">The reference discovery service mock.</param>
    /// <param name="typeHierarchyService">The type-hierarchy service mock.</param>
    public QueryContextMockGraph(
        Mock<IQueryContext> queryContext,
        Mock<IWorkspaceResolver> workspaceResolver,
        Mock<IWorkspacePathService> workspacePathService,
        Mock<IToolExecutionServices> toolExecutionServices,
        Mock<IToolRequestResolver> requestResolver,
        Mock<IProjectTargetFrameworkResolver> projectTargetFrameworkResolver,
        Mock<IWorkspaceSelectorFactory> workspaceSelectorFactory,
        Mock<IQueryResultCache> queryResultCache,
        Mock<IReferenceDiscoveryService> referenceDiscoveryService,
        Mock<ITypeHierarchyService> typeHierarchyService)
    {
        QueryContext = queryContext;
        WorkspaceResolver = workspaceResolver;
        WorkspacePathService = workspacePathService;
        ToolExecutionServices = toolExecutionServices;
        RequestResolver = requestResolver;
        ProjectTargetFrameworkResolver = projectTargetFrameworkResolver;
        WorkspaceSelectorFactory = workspaceSelectorFactory;
        QueryResultCache = queryResultCache;
        ReferenceDiscoveryService = referenceDiscoveryService;
        TypeHierarchyService = typeHierarchyService;
    }
}
