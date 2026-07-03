using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Roslyn.Workbench.Mcp.Plugins.Core.Test;
using Roslyn.Workbench.Mcp.Workspace.Test;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class PluginDiscoveryAndMcpToolTests
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void GIVEN_PluginDirectoryAssemblies_WHEN_LoadingCatalog_THEN_ShouldKeepEnabledToolsAndDisabledDiagnostics()
    {
        var pluginDirectory = CreatePluginDirectory(
            typeof(HostValidQueryPlugin).Assembly,
            typeof(HostValidMutationPlugin).Assembly,
            typeof(ValidQueryTestPlugin).Assembly);

        var startupOptions = CreateStartupOptions(pluginDirectory);
        var loader = new PluginCatalogLoader();
        var snapshot = loader.Load(startupOptions, []);

        var tools = snapshot.Tools;
        var plugins = snapshot.Plugins;

        tools.Should().HaveCount(2);
        tools.Select(static tool => tool.Metadata.Name).Should().Contain(["host-valid-query", "host-valid-mutation"]);

        plugins.Should().ContainSingle(static status => status.PluginId == "host.valid.query" && status.Enabled);
        plugins.Should().ContainSingle(static status => status.PluginId == "host.valid.mutation" && status.Enabled);
        plugins.Should().ContainSingle(static status => !status.Enabled && status.Diagnostics.Count > 0);
    }

    [Fact]
    public async Task GIVEN_LoadedRegisteredTool_WHEN_BuildingPluginMcpServerTool_THEN_ShouldPublishMetadataAndInvokeStructuredContent()
    {
        var pluginDirectory = CreatePluginDirectory(typeof(HostValidQueryPlugin).Assembly);
        var startupOptions = CreateStartupOptions(pluginDirectory);
        var loader = new PluginCatalogLoader();
        var snapshot = loader.Load(startupOptions, []);
        var tool = snapshot.Tools.Single();
        var executor = new ToolExecutor(CreateExecutionContextFactory());
        var serverTool = new PluginMcpServerTool(tool, executor);

        serverTool.ProtocolTool.Name.Should().Be("host-valid-query");
        serverTool.ProtocolTool.Title.Should().Be("Host Valid Query");
        serverTool.ProtocolTool.Description.Should().Be("Returns a stable host test payload.");
        serverTool.ProtocolTool.Annotations.Should().NotBeNull();
        serverTool.ProtocolTool.Annotations!.ReadOnlyHint.Should().BeTrue();
        serverTool.ProtocolTool.OutputSchema.Should().BeNull();

        var result = await serverTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "host-valid-query",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["name"] = JsonSerializer.SerializeToElement("Name"),
                    },
                }),
            CancellationToken.None);

        result.StructuredContent.Should().NotBeNull();

        var payload = JsonSerializer.Deserialize<ToolResult<HostValidQueryPlugin.Response>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        result.IsError.Should().BeFalse();
        payload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        payload.Data!.Value.Should().Be("Name");
    }

    [Fact]
    public async Task GIVEN_FullOutputSchemaMode_WHEN_BuildingPluginMcpServerTool_THEN_ShouldPublishStructuredOutputSchema()
    {
        var pluginDirectory = CreatePluginDirectory(typeof(HostValidQueryPlugin).Assembly);
        var startupOptions = new StartupOptions
        {
            PluginDirectories = [pluginDirectory],
            DefaultMaxResults = 100,
            ToolOutputSchemaMode = ToolOutputSchemaMode.Full,
        };
        var loader = new PluginCatalogLoader();
        var snapshot = loader.Load(startupOptions, []);
        var tool = snapshot.Tools.Single();
        var executor = new ToolExecutor(CreateExecutionContextFactory());
        var serverTool = new PluginMcpServerTool(tool, executor);

        serverTool.ProtocolTool.OutputSchema.Should().NotBeNull();
        serverTool.ProtocolTool.OutputSchema!.Value.GetProperty("oneOf").ValueKind.Should().Be(JsonValueKind.Array);

        var result = await serverTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                CreateServer(),
                new JsonRpcRequest
                {
                    Method = "tools/call",
                },
                new CallToolRequestParams
                {
                    Name = "host-valid-query",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["name"] = JsonSerializer.SerializeToElement("Name"),
                    },
                }),
            CancellationToken.None);

        var payload = JsonSerializer.Deserialize<ToolResult<HostValidQueryPlugin.Response>>(result.StructuredContent!.Value.GetRawText(), _serializerOptions);

        payload!.Outcome.Should().Be(ToolOutcome.Succeeded);
        payload.Data!.Value.Should().Be("Name");
    }

    private static string CreatePluginDirectory(params Assembly[] assemblies)
    {
        var directory = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-plugin-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directory);

        foreach (var assembly in assemblies)
        {
            File.Copy(assembly.Location, Path.Combine(directory, Path.GetFileName(assembly.Location)), overwrite: true);
        }

        return directory;
    }

    private static StartupOptions CreateStartupOptions(string pluginDirectory)
    {
        return new StartupOptions
        {
            PluginDirectories = [pluginDirectory],
            DefaultMaxResults = 100,
        };
    }

    private static IToolExecutionContextFactory CreateExecutionContextFactory()
    {
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = "workspace-7",
            WorkspaceEpoch = 7,
            LoadedPath = "/workspace",
        };
        var queryContext = new Mock<IQueryContext>();
        var mutationContext = new Mock<IMutationContext>();
        var resolver = new Mock<IWorkspaceResolver>();
        var factory = new Mock<IToolExecutionContextFactory>();

        queryContext.SetupGet(static context => context.WorkspaceIdentity).Returns(workspaceIdentity);
        queryContext.SetupGet(static context => context.Resolver).Returns(resolver.Object);
        mutationContext.SetupGet(static context => context.WorkspaceIdentity).Returns(workspaceIdentity);
        mutationContext.SetupGet(static context => context.Resolver).Returns(resolver.Object);
        factory
            .Setup(static contextFactory => contextFactory.CreateQueryContextAsync(It.IsAny<RegisteredTool>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((RegisteredTool _, object _, CancellationToken _) => ValueTask.FromResult(ToolExecutionContextLease<IQueryContext>.Acquired(queryContext.Object)));
        factory
            .Setup(static contextFactory => contextFactory.CreateMutationContextAsync(It.IsAny<RegisteredTool>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((RegisteredTool _, object _, CancellationToken _) => ValueTask.FromResult(ToolExecutionContextLease<IMutationContext>.Acquired(mutationContext.Object)));

        return factory.Object;
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
