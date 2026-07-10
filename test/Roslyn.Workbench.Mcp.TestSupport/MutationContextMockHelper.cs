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

/// <summary>
/// Holds the visible mocks connected for a mutation tool unit test.
/// </summary>
/// <param name="MutationContext">The mutation context mock.</param>
/// <param name="WorkspaceResolver">The workspace resolver mock.</param>
/// <param name="ToolExecutionServices">The tool execution services mock.</param>
/// <param name="RequestResolver">The tool request resolver mock.</param>
public sealed record MutationContextMockGraph(
    Mock<IMutationContext> MutationContext,
    Mock<IWorkspaceResolver> WorkspaceResolver,
    Mock<IToolExecutionServices> ToolExecutionServices,
    Mock<IToolRequestResolver> RequestResolver);
