using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Test.Tools;

internal static class ServerOwnedToolTestSupport
{
    public static async Task<CallToolResult> InvokeAsync(
        McpServerTool tool,
        string toolName,
        IDictionary<string, JsonElement>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var server = CreateServer();
        await using var serverDisposal = server;
        return await tool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                server,
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = toolName,
                    Arguments = arguments ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                }),
            cancellationToken);
    }

    public static McpServer CreateServer()
    {
        var asyncDisposable = new Mock<IAsyncDisposable>();
        var server = new Mock<McpServer>();

        asyncDisposable.Setup(disposable => disposable.DisposeAsync()).Returns(ValueTask.CompletedTask);
        server.SetupGet(value => value.ClientCapabilities).Returns(new ClientCapabilities());
        server.SetupGet(value => value.ClientInfo).Returns(new Implementation
        {
            Name = "Test Client",
            Version = "1.0.0",
        });

        server.SetupGet(value => value.ServerOptions).Returns(new McpServerOptions());
        server.SetupGet(value => value.Services).Returns(new Mock<IServiceProvider>().Object);
        server.SetupGet(value => value.LoggingLevel).Returns((LoggingLevel?)null);
        server.SetupGet(value => value.SessionId).Returns("session");
        server.SetupGet(value => value.NegotiatedProtocolVersion).Returns("2025-06-18");
        server.Setup(value => value.RunAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        server
            .Setup(value => value.SendRequestAsync(It.IsAny<JsonRpcRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonRpcResponse
            {
                Result = new JsonObject(),
            });

        server
            .Setup(value => value.SendMessageAsync(It.IsAny<JsonRpcMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        server
            .Setup(value => value.RegisterNotificationHandler(It.IsAny<string>(), It.IsAny<Func<JsonRpcNotification, CancellationToken, ValueTask>>()))
            .Returns(asyncDisposable.Object);

        server.Setup(value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);

        return server.Object;
    }
}
