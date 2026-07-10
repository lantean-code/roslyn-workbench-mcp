using System.Reflection;
using System.Text.Json;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class PluginDiscoveryAndMcpToolIntegrationTests
{
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
        tools.Select(static tool => tool.Tool.Metadata.Name).Should().Contain(["host-valid-query", "host-valid-mutation"]);

        plugins.Should().ContainSingle(static status => status.PluginId == "host.valid.query" && status.Enabled);
        plugins.Should().ContainSingle(static status => status.PluginId == "host.valid.mutation" && status.Enabled);
        plugins.Should().ContainSingle(static status => !status.Enabled && status.Diagnostics.Count > 0);
    }

    [Fact]
    public async Task GIVEN_LoadedRegisteredTool_WHEN_PublishingAndInvokingThroughMcp_THEN_ShouldExposeProtocolMetadataSchemaAndStructuredContent()
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
        var serverTool = new PluginMcpServerTool(tool, CreateExecutionContextFactory());

        serverTool.ProtocolTool.Name.Should().Be("host-valid-query");
        serverTool.ProtocolTool.Title.Should().Be("Host Valid Query");
        serverTool.ProtocolTool.Annotations.Should().NotBeNull();
        serverTool.ProtocolTool.Annotations!.ReadOnlyHint.Should().BeTrue();
        serverTool.ProtocolTool.OutputSchema.Should().NotBeNull();
        serverTool.ProtocolTool.OutputSchema!.Value.GetProperty("oneOf").ValueKind.Should().Be(JsonValueKind.Array);

        var result = await McpIntegrationTestHost.InvokeServerToolAsync(serverTool, "host-valid-query", new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("Name"),
        });

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("ok").GetBoolean().Should().BeTrue();
        result.StructuredContent.Value.GetProperty("data").GetProperty("value").GetString().Should().Be("Name");
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
        queryContext.SetupGet(static context => context.WorkspaceResolver).Returns(resolver.Object);
        mutationContext.SetupGet(static context => context.WorkspaceIdentity).Returns(workspaceIdentity);
        mutationContext.SetupGet(static context => context.WorkspaceResolver).Returns(resolver.Object);
        factory
            .Setup(static contextFactory => contextFactory.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), It.IsAny<CancellationToken>()))
            .Returns((WorkspaceBoundRequest _, CancellationToken _) => ToolExecutionContextLease<IQueryContext>.Acquired(queryContext.Object));
        factory
            .Setup(static contextFactory => contextFactory.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), It.IsAny<CancellationToken>()))
            .Returns((WorkspaceBoundRequest _, CancellationToken _) => ToolExecutionContextLease<IMutationContext>.Acquired(mutationContext.Object));

        return factory.Object;
    }

}
