using System.IO.Pipelines;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace Roslyn.Workbench.Mcp.Test.Protocol;

public sealed class PluginMcpRequestHandlerProtocolIntegrationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

#pragma warning disable MCPEXP001
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_TaskAugmentedPluginCall_WHEN_RoutedThroughMcpSdk_THEN_ShouldRejectRequestWithoutInvokingAdapter()
    {
        var clientToServerPipe = new Pipe();
        var serverToClientPipe = new Pipe();
        var protocolTool = new Tool
        {
            Name = "plugin-tool",
        };
        var tool = new Mock<McpServerTool>();
        tool.SetupGet(static value => value.ProtocolTool).Returns(protocolTool);
        var catalogState = new PluginCatalogState();
        var tools = new Dictionary<string, McpServerTool>(StringComparer.Ordinal)
        {
            [protocolTool.Name] = tool.Object,
        };
        var runtimeCatalog = new PluginRuntimeCatalogSnapshot
        {
            Tools = tools,
        };
        catalogState.Publish(runtimeCatalog);
        var pluginHandler = new PluginMcpRequestHandler(catalogState);

        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddMcpServer(options => options.Handlers.CallToolHandler = pluginHandler.CallToolAsync)
            .WithStreamServerTransport(
                clientToServerPipe.Reader.AsStream(),
                serverToClientPipe.Writer.AsStream());

        await using var serviceProvider = services.BuildServiceProvider(validateScopes: true);
        var server = serviceProvider.GetRequiredService<McpServer>();
        using var serverCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var serverTask = server.RunAsync(serverCancellation.Token);
        var clientTransport = new StreamClientTransport(
            clientToServerPipe.Writer.AsStream(),
            serverToClientPipe.Reader.AsStream(),
            NullLoggerFactory.Instance);
        await using var client = await McpClient.CreateAsync(
            clientTransport,
            loggerFactory: NullLoggerFactory.Instance,
            cancellationToken: TestContext.Current.CancellationToken);
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeoutCancellation.CancelAfter(_timeout);

        try
        {
            var request = new CallToolRequestParams
            {
                Name = protocolTool.Name,
                Task = new McpTaskMetadata(),
            };

            var action = async () => await client.SendRequestAsync<CallToolRequestParams, CallToolResult>(
                RequestMethods.ToolsCall,
                request,
                cancellationToken: timeoutCancellation.Token);

            var exception = await action.Should().ThrowAsync<McpProtocolException>();
            exception.Which.ErrorCode.Should().Be(McpErrorCode.InvalidParams);
            exception.Which.Message.Should().Contain("does not support task-augmented execution");
            tool.Verify(
                value => value.InvokeAsync(It.IsAny<RequestContext<CallToolRequestParams>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            await serverCancellation.CancelAsync();
            await clientToServerPipe.Writer.CompleteAsync();
            await serverToClientPipe.Writer.CompleteAsync();
            await serverTask;
        }
    }
#pragma warning restore MCPEXP001
}
