using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution.Plugins;

public sealed class PluginMcpToolRegistrationVisitorTests
{
    [Fact]
    public void GIVEN_PluginQueryRegistration_WHEN_VisitingServices_THEN_ShouldRegisterContainerValidatedQueryAdapter()
    {
        var services = new ServiceCollection();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var handler = new Mock<IQueryToolHandler<TestRequest, TestResponse>>();
        var registration = McpServerToolTestData.CreatePluginQueryRegistration(handler.Object, "test-query");
        var target = new PluginMcpToolRegistrationVisitor(services);

        var result = target.VisitQuery(registration);

        result.Should().BeTrue();
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(PluginQueryRegistration<TestRequest, TestResponse>)
            && descriptor.ImplementationInstance == registration
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(McpServerTool)
            && descriptor.ImplementationType == typeof(PluginQueryMcpServerTool<TestRequest, TestResponse>)
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        AddAdapterDependencies(services, contextFactory.Object, protocolFactory.Object);
        using var serviceProvider = BuildValidatedProvider(services);
        serviceProvider.GetRequiredService<McpServerTool>()
            .Should().BeOfType<PluginQueryMcpServerTool<TestRequest, TestResponse>>();
    }

    [Fact]
    public void GIVEN_PluginMutationRegistration_WHEN_VisitingServices_THEN_ShouldRegisterContainerValidatedMutationAdapter()
    {
        var services = new ServiceCollection();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var handler = new Mock<IMutationToolHandler<TestRequest>>();
        var registration = McpServerToolTestData.CreatePluginMutationRegistration(handler.Object, "test-mutation");
        var target = new PluginMcpToolRegistrationVisitor(services);

        var result = target.VisitMutation(registration);

        result.Should().BeTrue();
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(PluginMutationRegistration<TestRequest>)
            && descriptor.ImplementationInstance == registration
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(McpServerTool)
            && descriptor.ImplementationType == typeof(PluginMutationMcpServerTool<TestRequest>)
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        AddAdapterDependencies(services, contextFactory.Object, protocolFactory.Object);
        using var serviceProvider = BuildValidatedProvider(services);
        serviceProvider.GetRequiredService<McpServerTool>()
            .Should().BeOfType<PluginMutationMcpServerTool<TestRequest>>();
    }

    private static void AddAdapterDependencies(
        IServiceCollection services,
        IToolExecutionContextFactory contextFactory,
        IMcpToolProtocolFactory protocolFactory)
    {
        services.AddSingleton(contextFactory);
        services.AddSingleton(protocolFactory);
        services.AddSingleton<IOptions<StartupOptions>>(Options.Create(new StartupOptions()));
    }

    private static ServiceProvider BuildValidatedProvider(IServiceCollection services)
    {
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    public sealed record TestRequest : WorkspaceBoundRequest
    {
    }

    public sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }
}
