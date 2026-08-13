using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution.Plugins;

public sealed class PluginMcpServerToolFactoryTests
{
    [Fact]
    public void GIVEN_PluginQueryRegistration_WHEN_CreatingAdapter_THEN_ShouldReturnClosedGenericQueryAdapter()
    {
        var handler = new Mock<IQueryToolHandler<TestRequest, TestResponse>>();
        var registration = McpServerToolTestData.CreatePluginQueryRegistration(handler.Object, "test-query");
        var target = CreateTarget();

        var result = target.VisitQuery(registration);

        result.Should().BeOfType<PluginQueryMcpServerTool<TestRequest, TestResponse>>();
    }

    [Fact]
    public void GIVEN_PluginMutationRegistration_WHEN_CreatingAdapter_THEN_ShouldReturnClosedGenericMutationAdapter()
    {
        var handler = new Mock<IMutationToolHandler<TestRequest>>();
        var registration = McpServerToolTestData.CreatePluginMutationRegistration(handler.Object, "test-mutation");
        var target = CreateTarget();

        var result = target.VisitMutation(registration);

        result.Should().BeOfType<PluginMutationMcpServerTool<TestRequest>>();
    }

    private static PluginMcpServerToolFactory CreateTarget()
    {
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var requestBinder = new Mock<IToolRequestBinder>();

        return new PluginMcpServerToolFactory(
            contextFactory.Object,
            protocolFactory.Object,
            requestBinder.Object,
            Options.Create(new StartupOptions()));
    }

#pragma warning disable CA1515 // Moq's dynamic proxy must access these closed-generic handler contracts.
    public sealed record TestRequest : WorkspaceMutationRequest
    {
    }

    public sealed record TestResponse : IQueryResponse
    {
        public string Value { get; init; } = string.Empty;
    }
#pragma warning restore CA1515
}
