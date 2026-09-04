namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class RoslynWorkbenchMcpServerOptionsConfigurationTests
{
    [Fact]
    public async Task GIVEN_ServerOptions_WHEN_Configuring_THEN_ShouldApplyInstructionsAndRoutePluginRequests()
    {
        var pluginRequestHandler = new Mock<IPluginMcpRequestHandler>();
        var listToolsResult = new ListToolsResult
        {
            Tools = [],
        };
        var callToolResult = new CallToolResult
        {
            Content = [],
        };
        var listToolsContext = CreateContext(new ListToolsRequestParams());
        var callToolContext = CreateContext(new CallToolRequestParams
        {
            Name = "plugin-tool",
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        pluginRequestHandler
            .Setup(value => value.ListToolsAsync(listToolsContext, cancellationToken))
            .ReturnsAsync(listToolsResult);
        pluginRequestHandler
            .Setup(value => value.CallToolAsync(callToolContext, cancellationToken))
            .ReturnsAsync(callToolResult);
        var options = new McpServerOptions();
        var target = new RoslynWorkbenchMcpServerOptionsConfiguration(pluginRequestHandler.Object);

        target.Configure(options);

        var actualListToolsResult = await options.Handlers.ListToolsHandler!(listToolsContext, cancellationToken);
        var actualCallToolResult = await options.Handlers.CallToolHandler!(callToolContext, cancellationToken);

        options.ServerInstructions.Should().Contain("Prefer queries before mutations.");
        options.ServerInstructions.Should().Contain("https://lantean-code.github.io/roslyn-workbench-mcp/");
        options.ServerInstructions.Should().Contain("/agent/");
        actualListToolsResult.Should().BeSameAs(listToolsResult);
        actualCallToolResult.Should().BeSameAs(callToolResult);
        pluginRequestHandler.Verify(
            value => value.ListToolsAsync(listToolsContext, cancellationToken),
            Times.Once);
        pluginRequestHandler.Verify(
            value => value.CallToolAsync(callToolContext, cancellationToken),
            Times.Once);
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
