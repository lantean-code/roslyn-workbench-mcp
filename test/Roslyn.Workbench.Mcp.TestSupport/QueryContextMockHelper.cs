using Roslyn.Workbench.Mcp.Workspace.Hierarchy;
using Roslyn.Workbench.Mcp.Workspace.References;
using Roslyn.Workbench.Mcp.Workspace.Results;

namespace Roslyn.Workbench.Mcp.TestSupport;

public static class QueryContextMockHelper
{
    public static QueryContextMockGraph Create()
    {
        using var workspace = new AdhocWorkspace();
        var queryContext = new Mock<IQueryContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var workspacePathService = new Mock<IWorkspacePathService>();
        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var requestResolver = new Mock<IToolRequestResolver>();
        var projectTargetFrameworkResolver = new Mock<IProjectTargetFrameworkResolver>();
        var workspaceSelectorFactory = new Mock<IWorkspaceSelectorFactory>();
        var queryResultCache = new Mock<IQueryResultCache>();
        var referenceDiscoveryService = new Mock<IReferenceDiscoveryService>();
        var typeHierarchyService = new Mock<ITypeHierarchyService>();
        var typeHierarchyServiceImplementation = new TypeHierarchyService();

        toolExecutionServices
            .SetupGet(item => item.ReferenceDiscoveryService)
            .Returns(referenceDiscoveryService.Object);

        referenceDiscoveryService
            .Setup(item => item.FindReferencesAsync(
                It.IsAny<Guid>(),
                It.IsAny<Solution>(),
                It.IsAny<ISymbol>(),
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ReferenceOccurrence>());

        toolExecutionServices
            .SetupGet(item => item.TypeHierarchyService)
            .Returns(typeHierarchyService.Object);

        typeHierarchyService
            .Setup(item => item.FindDerivedTypesAsync(
                It.IsAny<INamedTypeSymbol>(),
                It.IsAny<Solution>(),
                It.IsAny<IReadOnlyCollection<Project>>(),
                It.IsAny<CancellationToken>()))
            .Returns((INamedTypeSymbol root, Solution solution, IReadOnlyCollection<Project> projects, CancellationToken cancellationToken) =>
                typeHierarchyServiceImplementation.FindDerivedTypesAsync(root, solution, projects, cancellationToken));

        toolExecutionServices
            .SetupGet(item => item.RequestResolver)
            .Returns(requestResolver.Object);

        toolExecutionServices
            .SetupGet(item => item.ProjectTargetFrameworkResolver)
            .Returns(projectTargetFrameworkResolver.Object);

        toolExecutionServices
            .SetupGet(item => item.WorkspaceSelectorFactory)
            .Returns(workspaceSelectorFactory.Object);

        queryContext
            .SetupGet(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);

        queryContext
            .SetupGet(item => item.WorkspacePathService)
            .Returns(workspacePathService.Object);

        queryContext
            .SetupGet(item => item.ToolExecutionServices)
            .Returns(toolExecutionServices.Object);

        queryContext
            .SetupGet(item => item.QueryResultCache)
            .Returns(queryResultCache.Object);

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(workspace.CurrentSolution);

        queryContext
            .SetupGet(item => item.WorkspaceIdentity)
            .Returns(new WorkspaceIdentity
            {
                WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            });

        return new QueryContextMockGraph(
            queryContext,
            workspaceResolver,
            workspacePathService,
            toolExecutionServices,
            requestResolver,
            projectTargetFrameworkResolver,
            workspaceSelectorFactory,
            queryResultCache,
            referenceDiscoveryService,
            typeHierarchyService);
    }
}
