using Microsoft.Extensions.DependencyInjection;
using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

public sealed class McpServerToolFactoryTests
{
    [Fact]
    public void GIVEN_PluginQueryRegistration_WHEN_VisitingFactory_THEN_ShouldCreateTypedQueryAdapter()
    {
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var handler = new Mock<IQueryToolHandler<TestRequest, TestResponse>>();
        var registration = new PluginQueryRegistration<TestRequest, TestResponse>(
            CreatePluginTool(ToolKind.Query, typeof(TestResponse)),
            handler.Object);
        var target = new PluginMcpServerToolFactory(contextFactory.Object);

        var result = target.VisitQuery(registration);

        result.Should().BeOfType<PluginQueryMcpServerTool<TestRequest, TestResponse>>();
        result.ProtocolTool.Name.Should().Be("test-tool");
    }

    [Fact]
    public void GIVEN_PluginMutationRegistration_WHEN_VisitingFactory_THEN_ShouldCreateTypedMutationAdapter()
    {
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var handler = new Mock<IMutationToolHandler<TestRequest>>();
        var registration = new PluginMutationRegistration<TestRequest>(
            CreatePluginTool(ToolKind.Mutation, typeof(MutationData)),
            handler.Object);
        var target = new PluginMcpServerToolFactory(contextFactory.Object);

        var result = target.VisitMutation(registration);

        result.Should().BeOfType<PluginMutationMcpServerTool<TestRequest>>();
        result.ProtocolTool.Name.Should().Be("test-tool");
    }

    [Fact]
    public void GIVEN_CodeActionQueryRegistration_WHEN_VisitingFactory_THEN_ShouldCreateTypedQueryAdapter()
    {
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var registration = new CodeActionQueryRegistration<TestQueryHandler, TestRequest, TestResponse>(CreateCodeActionMetadata());
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var target = new CodeActionMcpServerToolFactory(serviceProvider, contextFactory.Object);

        var result = target.VisitQuery(registration);

        result.Should().BeOfType<CodeActionQueryMcpServerTool<TestQueryHandler, TestRequest, TestResponse>>();
        result.ProtocolTool.Name.Should().Be("test-tool");
    }

    [Fact]
    public void GIVEN_CodeActionMutationRegistration_WHEN_VisitingFactory_THEN_ShouldCreateTypedMutationAdapter()
    {
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var registration = new CodeActionMutationRegistration<TestMutationHandler, TestRequest>(CreateCodeActionMetadata());
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var target = new CodeActionMcpServerToolFactory(serviceProvider, contextFactory.Object);

        var result = target.VisitMutation(registration);

        result.Should().BeOfType<CodeActionMutationMcpServerTool<TestMutationHandler, TestRequest>>();
        result.ProtocolTool.Name.Should().Be("test-tool");
    }

    private static RegisteredTool CreatePluginTool(ToolKind kind, Type responseType)
    {
        return new RegisteredTool
        {
            Plugin = new PluginMetadata
            {
                PluginId = "plugin.test",
                DisplayName = "Plugin Test",
                Version = "1.0.0",
                SupportedApiVersion = PluginApiVersions.V1,
            },
            Metadata = new ToolRegistrationMetadata
            {
                Name = "test-tool",
                Title = "Test Tool",
                Description = "Description",
            },
            Kind = kind,
            RequestType = typeof(TestRequest),
            ResponseType = responseType,
        };
    }

    private static CodeActionToolMetadata CreateCodeActionMetadata()
    {
        return new CodeActionToolMetadata
        {
            Name = "test-tool",
            Title = "Test Tool",
            Description = "Description",
        };
    }

    public sealed record TestRequest : WorkspaceBoundRequest
    {
    }

    public sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class TestQueryHandler : CodeActionQueryToolHandler<TestRequest, TestResponse>
    {
        protected override ValueTask<CodeActionExecutionResult<TestResponse>> ExecuteCoreAsync(
            TestRequest request,
            ICodeActionQueryContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(CodeActionExecutionResult<TestResponse>.Success(new TestResponse()));
        }
    }

    private sealed class TestMutationHandler : CodeActionMutationToolHandler<TestRequest>
    {
        protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(
            TestRequest request,
            ICodeActionMutationContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(CodeActionExecutionResult<WorkspaceMutationCandidate>.NoChange());
        }
    }
}
