namespace Roslyn.Workbench.Mcp.TestSupport;

/// <summary>
/// Holds the visible mocks connected for a mutation tool unit test.
/// </summary>
public sealed record MutationContextMockGraph
{
    /// <summary>
    /// Gets the mutation context mock.
    /// </summary>
    public Mock<IMutationContext> MutationContext { get; }

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
    /// Initializes a new instance of the <see cref="MutationContextMockGraph"/> class.
    /// </summary>
    /// <param name="mutationContext">The mutation context mock.</param>
    /// <param name="workspaceResolver">The workspace resolver mock.</param>
    /// <param name="workspacePathService">The workspace path service mock.</param>
    /// <param name="toolExecutionServices">The tool execution services mock.</param>
    /// <param name="requestResolver">The tool request resolver mock.</param>
    public MutationContextMockGraph(
        Mock<IMutationContext> mutationContext,
        Mock<IWorkspaceResolver> workspaceResolver,
        Mock<IWorkspacePathService> workspacePathService,
        Mock<IToolExecutionServices> toolExecutionServices,
        Mock<IToolRequestResolver> requestResolver)
    {
        MutationContext = mutationContext;
        WorkspaceResolver = workspaceResolver;
        WorkspacePathService = workspacePathService;
        ToolExecutionServices = toolExecutionServices;
        RequestResolver = requestResolver;
    }
}
