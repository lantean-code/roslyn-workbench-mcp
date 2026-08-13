using ModelContextProtocol;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class PluginMcpRequestHandlerTests
{
    private readonly Mock<IPluginCatalogState> _catalogState;
    private readonly Dictionary<string, McpServerTool> _tools;
    private readonly PluginMcpRequestHandler _target;

    public PluginMcpRequestHandlerTests()
    {
        _tools = new Dictionary<string, McpServerTool>(StringComparer.Ordinal);
        var snapshot = new PluginRuntimeCatalogSnapshot
        {
            Tools = _tools,
        };
        _catalogState = new Mock<IPluginCatalogState>();
        _catalogState.SetupGet(static value => value.Current).Returns(snapshot);
        _target = new PluginMcpRequestHandler(_catalogState.Object);
    }

    [Fact]
    public async Task GIVEN_PublishedPluginTools_WHEN_ListingTools_THEN_ShouldReturnProtocolMetadata()
    {
        var tool = new Mock<McpServerTool>();
        tool.SetupGet(static value => value.ProtocolTool).Returns(new Tool
        {
            Name = "plugin-tool",
        });
        _tools.Add("plugin-tool", tool.Object);
        var context = CreateContext(new ListToolsRequestParams());

        var result = await _target.ListToolsAsync(context, TestContext.Current.CancellationToken);

        result.Tools.Should().ContainSingle().Which.Name.Should().Be("plugin-tool");
    }

    [Fact]
    public async Task GIVEN_ContinuationCursor_WHEN_ListingTools_THEN_ShouldReturnEmptyPage()
    {
        var context = CreateContext(new ListToolsRequestParams
        {
            Cursor = "next",
        });

        var result = await _target.ListToolsAsync(context, TestContext.Current.CancellationToken);

        result.Tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_UnknownPluginTool_WHEN_CallingTool_THEN_ShouldReturnInvalidParametersProtocolError()
    {
        var context = CreateContext(new CallToolRequestParams
        {
            Name = "missing-tool",
        });

        var action = async () => await _target.CallToolAsync(context, TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<McpProtocolException>();
        exception.Which.ErrorCode.Should().Be(McpErrorCode.InvalidParams);
    }

    [Fact]
    public async Task GIVEN_RegisteredPluginTool_WHEN_CallingTool_THEN_ShouldInvokePrebuiltAdapter()
    {
        var expected = new CallToolResult
        {
            Content = [],
        };
        var context = CreateContext(new CallToolRequestParams
        {
            Name = "plugin-tool",
        });
        var tool = new Mock<McpServerTool>();
        tool.SetupGet(static value => value.ProtocolTool).Returns(new Tool
        {
            Name = "plugin-tool",
        });
        tool.Setup(value => value.InvokeAsync(context, TestContext.Current.CancellationToken))
            .ReturnsAsync(expected);
        _tools.Add("plugin-tool", tool.Object);

        var result = await _target.CallToolAsync(context, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expected);
        tool.Verify(value => value.InvokeAsync(context, TestContext.Current.CancellationToken), Times.Once);
    }

#pragma warning disable MCPEXP001

    [Fact]
    public async Task GIVEN_TaskAugmentedPluginCall_WHEN_CallingTool_THEN_ShouldRejectRequestWithoutInvokingAdapter()
    {
        var context = CreateContext(new CallToolRequestParams
        {
            Name = "plugin-tool",
            Task = new McpTaskMetadata(),
        });
        var tool = new Mock<McpServerTool>();
        tool.SetupGet(static value => value.ProtocolTool).Returns(new Tool
        {
            Name = "plugin-tool",
        });
        _tools.Add("plugin-tool", tool.Object);

        var action = async () => await _target.CallToolAsync(context, TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<McpProtocolException>();
        exception.Which.ErrorCode.Should().Be(McpErrorCode.InvalidParams);
        exception.Which.Message.Should().Contain("does not support task-augmented execution");
        tool.Verify(
            value => value.InvokeAsync(It.IsAny<RequestContext<CallToolRequestParams>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

#pragma warning restore MCPEXP001

    [Fact]
    public async Task GIVEN_CancelledRequest_WHEN_ListingTools_THEN_ShouldObserveCancellation()
    {
        var context = CreateContext(new ListToolsRequestParams());
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await _target.ListToolsAsync(context, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static RequestContext<TParams> CreateContext<TParams>(TParams parameters)
    {
        var server = new Mock<McpServer>();
        var request = new JsonRpcRequest
        {
            Method = "test",
        };

        return new RequestContext<TParams>(server.Object, request, parameters);
    }
}
