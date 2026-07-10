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
            CreatePluginTool(ToolKind.Mutation, typeof(MutationProposal)),
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
        var handler = new Mock<ICodeActionQueryToolHandler<TestRequest, TestResponse>>();
        var registration = new CodeActionQueryRegistration<TestRequest, TestResponse>(
            CreateCodeActionMetadata(),
            handler.Object);
        var target = new CodeActionMcpServerToolFactory(contextFactory.Object);

        var result = target.VisitQuery(registration);

        result.Should().BeOfType<CodeActionQueryMcpServerTool<TestRequest, TestResponse>>();
        result.ProtocolTool.Name.Should().Be("test-tool");
    }

    [Fact]
    public void GIVEN_CodeActionMutationRegistration_WHEN_VisitingFactory_THEN_ShouldCreateTypedMutationAdapter()
    {
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var handler = new Mock<ICodeActionMutationToolHandler<TestRequest>>();
        var registration = new CodeActionMutationRegistration<TestRequest>(
            CreateCodeActionMetadata(),
            handler.Object);
        var target = new CodeActionMcpServerToolFactory(contextFactory.Object);

        var result = target.VisitMutation(registration);

        result.Should().BeOfType<CodeActionMutationMcpServerTool<TestRequest>>();
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
}
