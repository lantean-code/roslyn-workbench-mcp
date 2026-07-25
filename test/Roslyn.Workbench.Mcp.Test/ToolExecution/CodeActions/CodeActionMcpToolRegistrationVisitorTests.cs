using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ToolExecution.CodeActions;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution.CodeActions;

public sealed class CodeActionMcpToolRegistrationVisitorTests
{
    [Fact]
    public void GIVEN_CodeActionQueryRegistration_WHEN_VisitingServices_THEN_ShouldRegisterContainerValidatedQueryAdapter()
    {
        var services = new ServiceCollection();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var registration = new CodeActionQueryRegistration<TestQueryHandler, TestRequest, TestResponse>(CreateMetadata());
        var target = new CodeActionMcpToolRegistrationVisitor(services);

        var result = target.VisitQuery(registration);

        result.Should().BeTrue();
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(CodeActionQueryRegistration<TestQueryHandler, TestRequest, TestResponse>)
            && descriptor.ImplementationInstance == registration);

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(TestQueryHandler)
            && descriptor.ImplementationType == typeof(TestQueryHandler));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(McpServerTool)
            && descriptor.ImplementationType == typeof(CodeActionQueryMcpServerTool<TestQueryHandler, TestRequest, TestResponse>)
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        AddAdapterDependencies(services, contextFactory.Object, protocolFactory.Object);
        using var serviceProvider = BuildValidatedProvider(services);
        serviceProvider.GetRequiredService<McpServerTool>()
            .Should().BeOfType<CodeActionQueryMcpServerTool<TestQueryHandler, TestRequest, TestResponse>>();
    }

    [Fact]
    public void GIVEN_CodeActionMutationRegistration_WHEN_VisitingServices_THEN_ShouldRegisterContainerValidatedMutationAdapter()
    {
        var services = new ServiceCollection();
        var contextFactory = new Mock<ICodeActionExecutionContextFactory>();
        var referenceStore = new Mock<ICodeActionReferenceStore>();
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var registration = new CodeActionMutationRegistration<TestMutationHandler, TestRequest>(CreateMetadata());
        var target = new CodeActionMcpToolRegistrationVisitor(services);

        var result = target.VisitMutation(registration);

        result.Should().BeTrue();
        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(CodeActionMutationRegistration<TestMutationHandler, TestRequest>)
            && descriptor.ImplementationInstance == registration);

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(TestMutationHandler)
            && descriptor.ImplementationType == typeof(TestMutationHandler));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(McpServerTool)
            && descriptor.ImplementationType == typeof(CodeActionMutationMcpServerTool<TestMutationHandler, TestRequest>)
            && descriptor.Lifetime == ServiceLifetime.Singleton);

        AddAdapterDependencies(services, contextFactory.Object, protocolFactory.Object);
        services.AddSingleton(referenceStore.Object);
        using var serviceProvider = BuildValidatedProvider(services);
        serviceProvider.GetRequiredService<McpServerTool>()
            .Should().BeOfType<CodeActionMutationMcpServerTool<TestMutationHandler, TestRequest>>();
    }

    private static void AddAdapterDependencies(
        IServiceCollection services,
        ICodeActionExecutionContextFactory contextFactory,
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

    private static CodeActionToolMetadata CreateMetadata()
    {
        return new CodeActionToolMetadata
        {
            Name = "test-tool",
            Title = "Test Tool",
            Description = "Description",
        };
    }

#pragma warning disable CA1812 // Contract and handler fixtures are activated indirectly by dependency injection.
    private sealed record TestRequest : WorkspaceMutationRequest
    {
    }

    private sealed record TestResponse
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
            var response = new TestResponse();
            var result = CodeActionExecutionResult<TestResponse>.Success(response);
            return ValueTask.FromResult(result);
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
#pragma warning restore CA1812
}
