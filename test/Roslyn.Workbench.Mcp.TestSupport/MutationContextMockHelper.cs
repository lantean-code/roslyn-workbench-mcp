namespace Roslyn.Workbench.Mcp.TestSupport;

/// <summary>
/// Creates repeatable Moq graphs for mutation tool unit tests.
/// </summary>
public static class MutationContextMockHelper
{
    /// <summary>
    /// Creates a mutation context and its directly connected dependency mocks.
    /// </summary>
    /// <returns>The connected mutation context mock graph.</returns>
    public static MutationContextMockGraph Create()
    {
        var mutationContext = new Mock<IMutationContext>();
        var workspaceResolver = new Mock<IWorkspaceResolver>();
        var toolExecutionServices = new Mock<IToolExecutionServices>();
        var requestResolver = new Mock<IToolRequestResolver>();

        toolExecutionServices
            .SetupGet(item => item.RequestResolver)
            .Returns(requestResolver.Object);

        mutationContext
            .SetupGet(item => item.WorkspaceResolver)
            .Returns(workspaceResolver.Object);

        mutationContext
            .SetupGet(item => item.ToolExecutionServices)
            .Returns(toolExecutionServices.Object);

        return new MutationContextMockGraph(
            mutationContext,
            workspaceResolver,
            toolExecutionServices,
            requestResolver);
    }
}
