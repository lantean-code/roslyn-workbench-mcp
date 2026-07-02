using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class ServerStatusToolTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GIVEN_ServerStatusTool_WHEN_Invoked_THEN_ShouldReturnStructuredServerDiagnostics()
    {
        var startupOptions = new StartupOptions
        {
            DefaultMaxResults = 100,
            MaxResponseBytes = 4096,
            MaxConcurrentQueries = 2,
            MaxTransactionRevisions = 20,
            CodeActionTokenLifetime = TimeSpan.FromMinutes(5),
        };
        var pluginSnapshot = new PluginCatalogSnapshot
        {
            Plugins =
            [
                new PluginStatus
                {
                    PluginId = "plugin.id",
                    DisplayName = "Plugin Name",
                    Version = "1.2.3",
                    SupportedApiVersion = "1.0",
                    Enabled = true,
                },
            ],
        };
        var tool = ServerStatusToolFactory.Create(startupOptions, pluginSnapshot, new ComponentStatus { IsAvailable = true }, 14);

        var result = await tool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "server-status",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);

        var payload = JsonSerializer.Deserialize<ToolResult<ServerStatusData>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        result.IsError.Should().BeFalse();
        payload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        payload.Data!.ProtocolVersion.Should().Be("2025-06-18");
        payload.Data.ToolCount.Should().Be(14);
        payload.Data.CodeActions.Should().NotBeNull();
        payload.Data.Plugins.Should().ContainSingle(static plugin => plugin.PluginId == "plugin.id");
        payload.Data.Configuration!.MaxResponseBytes.Should().Be(4096);
        payload.Data.ServerVersion.Should().Be(Assembly.GetAssembly(typeof(ServerStatusToolFactory))!.GetName().Version!.ToString());
    }

    [Fact]
    public void GIVEN_DefaultOutputSchemaMode_WHEN_CreatingServerStatusTool_THEN_ShouldOmitOutputSchemaAndAppendResultHint()
    {
        var startupOptions = new StartupOptions();
        var pluginSnapshot = new PluginCatalogSnapshot();

        var tool = ServerStatusToolFactory.Create(startupOptions, pluginSnapshot, new ComponentStatus { IsAvailable = true }, 14);

        tool.ProtocolTool.OutputSchema.Should().BeNull();
        tool.ProtocolTool.Description.Should().Be("Returns server diagnostics without requiring a loaded workspace. Result: server diagnostics, effective configuration, plugin status, and unfinished recovery state.");
    }

    [Fact]
    public void GIVEN_FullOutputSchemaMode_WHEN_CreatingServerStatusTool_THEN_ShouldPublishOutputSchema()
    {
        var startupOptions = new StartupOptions
        {
            ToolOutputSchemaMode = ToolOutputSchemaMode.Full,
        };
        var pluginSnapshot = new PluginCatalogSnapshot();

        var tool = ServerStatusToolFactory.Create(startupOptions, pluginSnapshot, new ComponentStatus { IsAvailable = true }, 14);

        tool.ProtocolTool.OutputSchema.Should().NotBeNull();
        tool.ProtocolTool.OutputSchema!.Value.GetProperty("oneOf").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GIVEN_UnfinishedRecoveryRecord_WHEN_InvokingServerStatusTool_THEN_ShouldReturnRecoveryDiagnostics()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-status-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(stateDirectory);
        CommitRecoveryStore.WriteStatus(stateDirectory, new RecoveryStatus
        {
            CommitId = "commit-id",
            SolutionPath = "/workspace/Sample.csproj",
            State = RecoveryState.RecoveryIncomplete,
            Message = "Message",
        });
        var startupOptions = new StartupOptions
        {
            DefaultMaxResults = 100,
            MaxResponseBytes = 4096,
            MaxConcurrentQueries = 2,
            MaxTransactionRevisions = 20,
            CodeActionTokenLifetime = TimeSpan.FromMinutes(5),
            StateDirectory = stateDirectory,
        };
        var pluginSnapshot = new PluginCatalogSnapshot();
        var tool = ServerStatusToolFactory.Create(startupOptions, pluginSnapshot, new ComponentStatus { IsAvailable = true }, 14);

        var result = await tool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "server-status",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);

        var payload = JsonSerializer.Deserialize<ToolResult<ServerStatusData>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        payload!.Data!.Recovery.Should().ContainSingle(static status => status.CommitId == "commit-id");
    }

    [Fact]
    public async Task GIVEN_UnavailableCodeActionComposition_WHEN_InvokingServerStatusTool_THEN_ShouldReturnDisablementDiagnostics()
    {
        var startupOptions = new StartupOptions
        {
            DefaultMaxResults = 100,
            MaxResponseBytes = 4096,
            MaxConcurrentQueries = 2,
            MaxTransactionRevisions = 20,
            CodeActionTokenLifetime = TimeSpan.FromMinutes(5),
        };
        var pluginSnapshot = new PluginCatalogSnapshot();
        var tool = ServerStatusToolFactory.Create(
            startupOptions,
            pluginSnapshot,
            new ComponentStatus
            {
                IsAvailable = false,
                Message = "Code-action composition is unavailable.",
            },
            14);

        var result = await tool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "server-status",
                    Arguments = new Dictionary<string, JsonElement>(),
                }),
            CancellationToken.None);

        var payload = JsonSerializer.Deserialize<ToolResult<ServerStatusData>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        payload!.Data!.CodeActions.Should().NotBeNull();
        payload.Data.CodeActions!.IsAvailable.Should().BeFalse();
        payload.Data.CodeActions.Message.Should().Be("Code-action composition is unavailable.");
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
}
