using ModelContextProtocol;

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
        var target = new PluginQueryMcpServerTool<TestRequest, TestResponse>(
            protocolTool,
            handler.Object,
            contextFactory.Object);

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
        var target = new PluginQueryMcpServerTool<TestRequest, TestResponse>(
            McpServerToolTestData.CreateProtocolTool("test-query"),
            handler.Object,
            contextFactory.Object);
        var requestContext = new RequestContext<CallToolRequestParams>(
            ServerOwnedToolTestSupport.CreateServer(),
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

    public sealed record TestRequest : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed record TestResponse
    {
        public string Value { get; init; } = string.Empty;
    }
}
