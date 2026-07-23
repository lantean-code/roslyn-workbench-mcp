using Roslyn.Workbench.Mcp.Test.Tools;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

public sealed class McpServerToolBaseTests
{
    [Fact]
    public void GIVEN_ProtocolTool_WHEN_InspectingBaseProperties_THEN_ShouldExposeProtocolAndEmptyMetadata()
    {
        var protocolTool = McpServerToolTestData.CreateProtocolTool("test-query");
        var handler = new Mock<IQueryToolHandler<TestRequest, TestResponse>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var registration = McpServerToolTestData.CreatePluginQueryRegistration(handler.Object, "test-query");
        var protocolFactory = McpServerToolTestData.CreateProtocolFactory(protocolTool);
        var target = new PluginQueryMcpServerTool<TestRequest, TestResponse>(
            registration,
            contextFactory.Object,
            protocolFactory.Object,
            McpServerToolTestData.CreateOptions());

        target.ProtocolTool.Should().BeSameAs(protocolTool);
        target.Metadata.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_RequestContextWithoutArguments_WHEN_Invoking_THEN_ShouldUseEmptyArguments()
    {
        var handler = new Mock<IQueryToolHandler<TestRequest, TestResponse>>();
        var contextFactory = new Mock<IToolExecutionContextFactory>();
        var context = new Mock<IQueryContext>();
        contextFactory
            .Setup(item => item.CreateQueryContext(
                It.Is<TestRequest>(request => request.Name == string.Empty),
                CancellationToken.None))
            .Returns(ToolExecutionContextLease<IQueryContext>.Acquired(context.Object));

        handler
            .Setup(item => item.ExecuteAsync(
                It.Is<TestRequest>(request => request.Name == string.Empty),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(PluginExecutionResult<TestResponse>.Success(new TestResponse
            {
                Value = "Value",
            }));

        var registration = McpServerToolTestData.CreatePluginQueryRegistration(handler.Object, "test-query");
        var protocolFactory = McpServerToolTestData.CreateProtocolFactory(
            McpServerToolTestData.CreateProtocolTool("test-query"));

        var target = new PluginQueryMcpServerTool<TestRequest, TestResponse>(
            registration,
            contextFactory.Object,
            protocolFactory.Object,
            McpServerToolTestData.CreateOptions());

        var server = ServerOwnedToolTestSupport.CreateServer();
        await using var serverDisposal = server;
        var requestContext = new RequestContext<CallToolRequestParams>(
            server,
            new JsonRpcRequest
            {
                Method = "tools/call",
            },
            new CallToolRequestParams
            {
                Name = "test-query",
                Arguments = null,
            });

        var result = await target.InvokeAsync(requestContext, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("value").GetString().Should().Be("Value");
    }

#pragma warning disable CA1515 // Moq's dynamic proxy must access these closed-generic handler contracts.
    public sealed record TestRequest : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }
#pragma warning restore CA1515
}
