using Microsoft.Extensions.DependencyInjection;

using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

public sealed class McpToolRegistrationVisitorTests
{
    [Fact]
    public void GIVEN_PluginQueryRegistration_WHEN_VisitingServices_THEN_ShouldRegisterTypedQueryAdapterFactory()
    {
        var services = new ServiceCollection();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var handler = new Mock<IQueryToolHandler<TestRequest, TestResponse>>();
        var registration = new PluginQueryRegistration<TestRequest, TestResponse>(
            CreatePluginTool(ToolKind.Query, typeof(TestResponse)),
            handler.Object);
        var target = new PluginMcpToolRegistrationVisitor(services, ToolOutputSchemaMode.Omit);

        var result = target.VisitQuery(registration);

        result.Should().BeTrue();
        var descriptor = services.Should().ContainSingle().Subject;
        descriptor.ServiceType.Should().Be(typeof(McpServerTool));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        descriptor.ImplementationFactory.Should().NotBeNull();
        ResolveRegisteredTool(descriptor, contextFactory.Object).Should().BeOfType<PluginQueryMcpServerTool<TestRequest, TestResponse>>();
    }

    [Fact]
    public void GIVEN_PluginMutationRegistration_WHEN_VisitingServices_THEN_ShouldRegisterTypedMutationAdapterFactory()
    {
        var services = new ServiceCollection();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var handler = new Mock<IMutationToolHandler<TestRequest>>();
        var registration = new PluginMutationRegistration<TestRequest>(
            CreatePluginTool(ToolKind.Mutation, typeof(MutationData)),
            handler.Object);
        var target = new PluginMcpToolRegistrationVisitor(services, ToolOutputSchemaMode.Omit);

        var result = target.VisitMutation(registration);

        result.Should().BeTrue();
        var descriptor = services.Should().ContainSingle().Subject;
        descriptor.ServiceType.Should().Be(typeof(McpServerTool));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        descriptor.ImplementationFactory.Should().NotBeNull();
        ResolveRegisteredTool(descriptor, contextFactory.Object).Should().BeOfType<PluginMutationMcpServerTool<TestRequest>>();
    }

    [Fact]
    public void GIVEN_CodeActionQueryRegistration_WHEN_VisitingServices_THEN_ShouldRegisterTypedQueryAdapterFactory()
    {
        var services = new ServiceCollection();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var handler = new Mock<ICodeActionQueryToolHandler<TestRequest, TestResponse>>();
        var registration = new CodeActionQueryRegistration<TestRequest, TestResponse>(
            CreateCodeActionMetadata(),
            handler.Object);
        var target = new CodeActionMcpToolRegistrationVisitor(services, ToolOutputSchemaMode.Omit);

        var result = target.VisitQuery(registration);

        result.Should().BeTrue();
        var descriptor = services.Should().ContainSingle().Subject;
        descriptor.ServiceType.Should().Be(typeof(McpServerTool));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        descriptor.ImplementationFactory.Should().NotBeNull();
        ResolveRegisteredTool(descriptor, contextFactory.Object).Should().BeOfType<CodeActionQueryMcpServerTool<TestRequest, TestResponse>>();
    }

    [Fact]
    public void GIVEN_CodeActionMutationRegistration_WHEN_VisitingServices_THEN_ShouldRegisterTypedMutationAdapterFactory()
    {
        var services = new ServiceCollection();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var handler = new Mock<ICodeActionMutationToolHandler<TestRequest>>();
        var registration = new CodeActionMutationRegistration<TestRequest>(
            CreateCodeActionMetadata(),
            handler.Object);
        var target = new CodeActionMcpToolRegistrationVisitor(services, ToolOutputSchemaMode.Omit);

        var result = target.VisitMutation(registration);

        result.Should().BeTrue();
        var descriptor = services.Should().ContainSingle().Subject;
        descriptor.ServiceType.Should().Be(typeof(McpServerTool));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        descriptor.ImplementationFactory.Should().NotBeNull();
        ResolveRegisteredTool(descriptor, contextFactory.Object).Should().BeOfType<CodeActionMutationMcpServerTool<TestRequest>>();
    }

    private static McpServerTool ResolveRegisteredTool<TContextFactory>(
        ServiceDescriptor descriptor,
        TContextFactory contextFactory)
        where TContextFactory : class
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(item => item.GetService(typeof(TContextFactory)))
            .Returns(contextFactory);

        return (McpServerTool)descriptor.ImplementationFactory!(serviceProvider.Object);
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
