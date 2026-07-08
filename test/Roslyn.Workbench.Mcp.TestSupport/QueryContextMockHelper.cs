namespace Roslyn.Workbench.Mcp.TestSupport;

public static class QueryContextMockHelper
{
    public static QueryContextMockGraph Create()
    {
        var queryContext = new Mock<IQueryContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var requestResolver = new Mock<IToolRequestResolver>();

        toolExecutionServices
            .SetupGet(item => item.RequestResolver)
            .Returns(requestResolver.Object);
        queryContext
            .SetupGet(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);
        queryContext
            .SetupGet(item => item.ToolExecutionServices)
            .Returns(toolExecutionServices.Object);
        queryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(new AdhocWorkspace().CurrentSolution);

        return new QueryContextMockGraph(
            queryContext,
            workspaceResolver,
            toolExecutionServices,
            requestResolver);
    }
}

public sealed record QueryContextMockGraph(
    Mock<IQueryContext> QueryContext,
    Mock<IWorkspaceResolver> WorkspaceResolver,
    Mock<IToolExecutionServices> ToolExecutionServices,
    Mock<IToolRequestResolver> RequestResolver);
