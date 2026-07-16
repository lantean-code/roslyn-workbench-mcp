using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
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
        ResolveRegisteredTool(descriptor, contextFactory.Object, protocolFactory.Object)
            .Should().BeOfType<PluginQueryMcpServerTool<TestRequest, TestResponse>>();
    }

    [Fact]
    public void GIVEN_PluginMutationRegistration_WHEN_VisitingServices_THEN_ShouldRegisterTypedMutationAdapterFactory()
    {
        var services = new ServiceCollection();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
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
        ResolveRegisteredTool(descriptor, contextFactory.Object, protocolFactory.Object)
            .Should().BeOfType<PluginMutationMcpServerTool<TestRequest>>();
    }

    [Fact]
    public void GIVEN_CodeActionQueryRegistration_WHEN_VisitingServices_THEN_ShouldRegisterContainerValidatedQueryAdapter()
    {
        var services = new ServiceCollection();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var registration = new CodeActionQueryRegistration<TestQueryHandler, TestRequest, TestResponse>(CreateCodeActionMetadata());
        var target = new CodeActionMcpToolRegistrationVisitor(services);

        var result = target.VisitQuery(registration);

        result.Should().BeTrue();
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(CodeActionQueryRegistration<TestQueryHandler, TestRequest, TestResponse>));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(TestQueryHandler));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(McpServerTool)
            && descriptor.ImplementationType == typeof(CodeActionQueryMcpServerTool<TestQueryHandler, TestRequest, TestResponse>)
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        services.AddSingleton(contextFactory.Object);
        services.AddSingleton(protocolFactory.Object);
        services.AddSingleton(Options.Create(new StartupOptions()));
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
        });
        serviceProvider.GetRequiredService<McpServerTool>()
            .Should().BeOfType<CodeActionQueryMcpServerTool<TestQueryHandler, TestRequest, TestResponse>>();
    }

    [Fact]
    public void GIVEN_CodeActionMutationRegistration_WHEN_VisitingServices_THEN_ShouldRegisterContainerValidatedMutationAdapter()
    {
        var services = new ServiceCollection();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var registration = new CodeActionMutationRegistration<TestMutationHandler, TestRequest>(CreateCodeActionMetadata());
        var target = new CodeActionMcpToolRegistrationVisitor(services);

        var result = target.VisitMutation(registration);

        result.Should().BeTrue();
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(CodeActionMutationRegistration<TestMutationHandler, TestRequest>));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(TestMutationHandler));
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(McpServerTool)
            && descriptor.ImplementationType == typeof(CodeActionMutationMcpServerTool<TestMutationHandler, TestRequest>)
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        services.AddSingleton(contextFactory.Object);
        services.AddSingleton(protocolFactory.Object);
        services.AddSingleton(Options.Create(new StartupOptions()));
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
        });
        serviceProvider.GetRequiredService<McpServerTool>()
            .Should().BeOfType<CodeActionMutationMcpServerTool<TestMutationHandler, TestRequest>>();
    }

    private static McpServerTool ResolveRegisteredTool<TContextFactory>(
        ServiceDescriptor descriptor,
        TContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory)
        where TContextFactory : class
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(item => item.GetService(typeof(TContextFactory)))
            .Returns(contextFactory);
        serviceProvider
            .Setup(item => item.GetService(typeof(IMcpToolProtocolFactory)))
            .Returns(protocolFactory);

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
