using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.TestSupport;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.Test;

[Trait("Category", "Integration")]
public sealed class WorkspaceStatusToolTests
{
    [Fact]
    public async Task GIVEN_OpenedWorkspace_WHEN_RequestingDefaultStatus_THEN_ShouldOmitLoadDiagnosticsBranch()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var runtime = WorkspaceCoordinatorFactory.Create();
        var openTool = new WorkspaceOpenTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);
        var statusTool = new WorkspaceStatusTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);

        await openTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-open",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["path"] = JsonSerializer.SerializeToElement(fixture.ProjectPath),
                    },
                }),
            CancellationToken.None);

        var result = await statusTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-status",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.TryGetProperty("loadDiagnostics", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_OpenedWorkspace_WHEN_RequestingFullStatusDetail_THEN_ShouldIncludeLoadDiagnosticsBranch()
    {
        using var fixture = await TestWorkspaceFixture.CreateAsync();
        var runtime = WorkspaceCoordinatorFactory.Create();
        var openTool = new WorkspaceOpenTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);
        var statusTool = new WorkspaceStatusTool(CreateStartupOptions(), runtime.WorkspaceLifecycleService);

        await openTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-open",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["path"] = JsonSerializer.SerializeToElement(fixture.ProjectPath),
                    },
                }),
            CancellationToken.None);

        var result = await statusTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "workspace-status",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["detail"] = JsonSerializer.SerializeToElement(StatusDetailLevel.Full),
                    },
                }),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("loadDiagnostics").ValueKind.Should().Be(JsonValueKind.Array);
    }

    private static McpServer CreateServer()
    {
        var asyncDisposable = new Mock<IAsyncDisposable>();
        var server = new Mock<McpServer>();

        asyncDisposable.Setup(static disposable => disposable.DisposeAsync()).Returns(ValueTask.CompletedTask);
        server.SetupGet(static value => value.ClientCapabilities).Returns(new ClientCapabilities());
        server.SetupGet(static value => value.ClientInfo).Returns(new Implementation
        {
            Name = "Test Client",
            Version = "1.0.0",
        });
        server.SetupGet(static value => value.ServerOptions).Returns(new McpServerOptions());
        server.SetupGet(static value => value.Services).Returns(Mock.Of<IServiceProvider>());
        server.SetupGet(static value => value.LoggingLevel).Returns((LoggingLevel?)null);
        server.SetupGet(static value => value.SessionId).Returns("session");
        server.SetupGet(static value => value.NegotiatedProtocolVersion).Returns("2025-06-18");
        server.Setup(static value => value.RunAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        server
            .Setup(static value => value.SendRequestAsync(It.IsAny<JsonRpcRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonRpcResponse
            {
                Result = new JsonObject(),
            });
        server
            .Setup(static value => value.SendMessageAsync(It.IsAny<JsonRpcMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        server
            .Setup(static value => value.RegisterNotificationHandler(It.IsAny<string>(), It.IsAny<Func<JsonRpcNotification, CancellationToken, ValueTask>>()))
            .Returns(asyncDisposable.Object);
        server.Setup(static value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);

        return server.Object;
    }

    private static IOptions<StartupOptions> CreateStartupOptions()
    {
        return Options.Create(new StartupOptions());
    }
}
