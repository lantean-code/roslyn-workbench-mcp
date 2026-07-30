using Roslyn.Workbench.Mcp.Workspace.Caching;
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
        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var requestResolver = new Mock<IToolRequestResolver>();
        var projectTargetFrameworkResolver = new Mock<IProjectTargetFrameworkResolver>();
        var workspaceSelectorFactory = new Mock<IWorkspaceSelectorFactory>();
        var queryCache = new Mock<IQueryCache>();
        var referenceDiscoveryService = new Mock<IReferenceDiscoveryService>();

        toolExecutionServices
            .SetupGet(item => item.QueryCache)
            .Returns(queryCache.Object);

        toolExecutionServices
            .SetupGet(item => item.ReferenceDiscoveryService)
            .Returns(referenceDiscoveryService.Object);

        referenceDiscoveryService
            .Setup(item => item.FindReferencesAsync(
                It.IsAny<string>(),
                It.IsAny<Solution>(),
                It.IsAny<ISymbol>(),
                It.IsAny<IReadOnlyList<Document>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ReferenceOccurrence>());

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
            .SetupGet(item => item.ToolExecutionServices)
            .Returns(toolExecutionServices.Object);

        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(workspace.CurrentSolution);

        queryContext
            .SetupGet(item => item.WorkspaceIdentity)
            .Returns(new WorkspaceIdentity
            {
                WorkspaceId = "WorkspaceId",
            });

        return new QueryContextMockGraph(
            queryContext,
            workspaceResolver,
            toolExecutionServices,
            requestResolver,
            projectTargetFrameworkResolver,
            workspaceSelectorFactory,
            queryCache,
            referenceDiscoveryService);
    }
}
