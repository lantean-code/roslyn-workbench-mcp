using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using NuGet.Versioning;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

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
        var loader = PluginCatalogComposition.CreateLoader();
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
        var loader = PluginCatalogComposition.CreateLoader();
        var snapshot = loader.Load(startupOptions, []);
        var tool = snapshot.Tools.Single();
        var serverTool = tool.Accept(new PluginMcpServerToolFactory(
            CreateExecutionContextFactory(),
            ToolOutputSchemaMode.Full));

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
        var pluginDependencyVersion = result.StructuredContent.Value
            .GetProperty("data")
            .GetProperty("privateDependencyVersion")
            .GetString();
        var hostDependencyVersion = typeof(NuGetVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        pluginDependencyVersion.Should().NotBeNullOrWhiteSpace();
        hostDependencyVersion.Should().NotBeNullOrWhiteSpace();
        pluginDependencyVersion.Should().NotBe(hostDependencyVersion);
    }

    [Fact]
    public async Task GIVEN_PackagedMutationPlugin_WHEN_InvokingThroughMcp_THEN_ShouldExecuteAndStageProposal()
    {
        var pluginDirectory = CreatePluginDirectory(typeof(HostValidMutationPlugin).Assembly);
        var loader = PluginCatalogComposition.CreateLoader();
        var snapshot = loader.Load(CreateStartupOptions(pluginDirectory), []);
        var tool = snapshot.Tools.Single();
        using var workspace = new AdhocWorkspace();
        var serverTool = tool.Accept(new PluginMcpServerToolFactory(
            CreateMutationExecutionContextFactory(workspace.CurrentSolution),
            ToolOutputSchemaMode.Full));

        var result = await McpIntegrationTestHost.InvokeServerToolAsync(serverTool, "host-valid-mutation", new Dictionary<string, JsonElement>
        {
            ["summary"] = JsonSerializer.SerializeToElement("Summary"),
        });

        result.IsError.Should().BeFalse();
        var content = result.StructuredContent ?? throw new InvalidOperationException("The mutation result did not contain structured content.");
        content.GetProperty("ok").GetBoolean().Should().BeTrue();
        content.GetProperty("staged").GetBoolean().Should().BeTrue();
        content.GetProperty("summary").GetString().Should().Be("Summary");
    }

    [Fact]
    public void GIVEN_PluginToolCollidesWithReservedCodeActionName_WHEN_LoadingCatalog_THEN_ShouldDisablePluginWithDiagnostic()
    {
        var pluginDirectory = CreatePluginDirectory(typeof(HostValidQueryPlugin).Assembly);
        var loader = PluginCatalogComposition.CreateLoader();

        var snapshot = loader.Load(
            CreateStartupOptions(pluginDirectory),
            [],
            ["host-valid-query"]);

        snapshot.Tools.Should().BeEmpty();
        snapshot.Plugins.Should().ContainSingle(status =>
            status.PluginId == "host.valid.query"
            && !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginCollision"
                && diagnostic.Message.Contains("collide", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void GIVEN_MultiplePackagesWithSamePluginId_WHEN_LoadingCatalog_THEN_ShouldDisableEveryPackageDeterministically()
    {
        var pluginDirectory = CreatePluginDirectory(
            typeof(HostValidQueryPlugin).Assembly,
            typeof(HostValidQueryPlugin).Assembly);
        var loader = PluginCatalogComposition.CreateLoader();

        var snapshot = loader.Load(CreateStartupOptions(pluginDirectory), []);

        snapshot.Tools.Should().BeEmpty();
        snapshot.Plugins.Should().HaveCount(2);
        snapshot.Plugins.Should().OnlyContain(status =>
            status.PluginId == "host.valid.query"
            && !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginCollision"
                && diagnostic.Message.Contains("same plugin ID", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void GIVEN_NoExternalPluginDirectory_WHEN_LoadingBundledCore_THEN_ShouldComposeBundledCatalogueInDefaultContext()
    {
        var loader = PluginCatalogComposition.CreateLoader();

        var snapshot = loader.Load(new StartupOptions(), [typeof(BundledCorePlugin).Assembly]);

        snapshot.Tools.Should().HaveCount(41);
        snapshot.Plugins.Should().ContainSingle(status => status.PluginId == "roslyn.workbench.core" && status.Enabled);
        snapshot.LoadContexts.Should().BeEmpty();
    }

    [Fact]
    public void GIVEN_SingleExportPluginConfigureThrows_WHEN_LoadingCatalog_THEN_ShouldDisablePluginWithoutPublishingExceptionDetails()
    {
        var pluginDirectory = CreatePluginDirectory(typeof(ThrowingConfigurationTestPlugin).Assembly);
        var loader = PluginCatalogComposition.CreateLoader();

        var snapshot = loader.Load(CreateStartupOptions(pluginDirectory), []);

        snapshot.Tools.Should().BeEmpty();
        snapshot.Plugins.Should().ContainSingle(status =>
            status.PluginId == "test.throwing.configuration"
            && !status.Enabled
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginLoad"
                && diagnostic.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)
                && !diagnostic.Message.Contains("Configuration failed", StringComparison.Ordinal)));
    }

    private static string CreatePluginDirectory(params Assembly[] assemblies)
    {
        var searchRoot = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-plugin-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(searchRoot);

        for (var index = 0; index < assemblies.Length; index++)
        {
            var assembly = assemblies[index];
            var packageName = assembly.GetName().Name ?? "plugin";
            var packageDirectory = Path.Combine(searchRoot, $"{index:D2}-{packageName}");
            Directory.CreateDirectory(packageDirectory);
            if (assembly == typeof(HostValidQueryPlugin).Assembly)
            {
                var fixtureAssets = Path.Combine(AppContext.BaseDirectory, "PluginFixtureAssets", "HostQuery");
                foreach (var assetPath in Directory.EnumerateFiles(fixtureAssets))
                {
                    File.Copy(assetPath, Path.Combine(packageDirectory, Path.GetFileName(assetPath)), overwrite: true);
                }
            }
            else
            {
                File.Copy(assembly.Location, Path.Combine(packageDirectory, Path.GetFileName(assembly.Location)), overwrite: true);
            }
        }

        return searchRoot;
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
        var resolver = new Mock<IWorkspaceResolver>();
        var factory = new Mock<IToolExecutionContextFactory>();

        queryContext.SetupGet(static context => context.WorkspaceIdentity).Returns(workspaceIdentity);
        queryContext.SetupGet(static context => context.WorkspaceResolver).Returns(resolver.Object);
        factory
            .Setup(static contextFactory => contextFactory.CreateQueryContext(It.IsAny<WorkspaceBoundRequest>(), It.IsAny<CancellationToken>()))
            .Returns((WorkspaceBoundRequest _, CancellationToken _) => ToolExecutionContextLease<IQueryContext>.Acquired(queryContext.Object));

        return factory.Object;
    }

    private static IToolExecutionContextFactory CreateMutationExecutionContextFactory(Solution solution)
    {
        var context = new Mock<IMutationContext>();
        var workspaceContext = new Mock<IWorkspaceExecutionContext>();
        var stager = new Mock<IWorkspaceMutationStager>();
        var factory = new Mock<IToolExecutionContextFactory>();
        context.SetupGet(static value => value.CurrentSolution).Returns(solution);
        stager
            .Setup(value => value.StageAsync(
                "host-valid-mutation",
                It.Is<WorkspaceMutationCandidate>(candidate => candidate.CandidateSolution == solution && candidate.Summary == "Summary"),
                It.IsAny<IReadOnlyList<DiagnosticInfo>>(),
                It.IsAny<IReadOnlyList<WarningInfo>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceOperationResult<MutationStagingOutcome>
            {
                Status = WorkspaceOperationStatus.Succeeded,
                Data = new MutationStagingOutcome
                {
                    Operation = "host-valid-mutation",
                    Summary = "Summary",
                    Transaction = new TransactionInfo
                    {
                        Revision = 1,
                    },
                },
            });
        var workspaceLease = WorkspaceMutationExecutionLease.Acquired(workspaceContext.Object, stager.Object);
        factory
            .Setup(static value => value.CreateMutationContext(It.IsAny<WorkspaceBoundRequest>(), It.IsAny<CancellationToken>()))
            .Returns(new PluginMutationExecutionLease(workspaceLease, context.Object, failure: null));
        return factory.Object;
    }

}
