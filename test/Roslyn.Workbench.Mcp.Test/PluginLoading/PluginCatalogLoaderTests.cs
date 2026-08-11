using System.Reflection;
using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginCatalogLoaderTests
{
    private readonly Mock<IPluginCandidatePreparer> _candidatePreparer;
    private readonly Mock<IPluginCatalogEntryMaterializer> _entryMaterializer;
    private readonly Mock<IPluginCollisionPolicy> _collisionPolicy;
    private readonly Mock<IPluginPackageDiscovery> _packageDiscovery;

    public PluginCatalogLoaderTests()
    {
        _candidatePreparer = new Mock<IPluginCandidatePreparer>();
        _entryMaterializer = new Mock<IPluginCatalogEntryMaterializer>();
        _collisionPolicy = new Mock<IPluginCollisionPolicy>();
        _packageDiscovery = new Mock<IPluginPackageDiscovery>();
        _candidatePreparer.Setup(static value => value.PrepareBundled(It.IsAny<IReadOnlyList<Assembly>>()))
            .Returns(new PluginCandidatePreparation());

        _candidatePreparer.Setup(static value => value.PrepareExternal(
            It.IsAny<IReadOnlyList<PluginPackageDiscoveryResult>>(),
            It.IsAny<IReadOnlySet<string>>()))
            .Returns(new PluginCandidatePreparation());

        _collisionPolicy.Setup(static value => value.FindDuplicateExternalPluginIds(It.IsAny<IReadOnlyList<PluginPackageDiscoveryResult>>()))
            .Returns(new HashSet<string>(StringComparer.Ordinal));

        _collisionPolicy.Setup(static value => value.FindExternalToolCollisions(
            It.IsAny<IReadOnlyList<PreparedCatalogPlugin>>(),
            It.IsAny<IReadOnlySet<string>>()))
            .Returns(new HashSet<string>(StringComparer.Ordinal));

        _packageDiscovery.Setup(static value => value.Discover(It.IsAny<IReadOnlyList<string>>())).Returns([]);
    }

    [Fact]
    public void GIVEN_PreparedBundledAndExternalPlugins_WHEN_Loading_THEN_ShouldCoordinatePhasesAndAggregateSnapshot()
    {
        var bundledPlugin = CreatePreparedPlugin("bundled", "bundled-tool");
        var externalPlugin = CreatePreparedPlugin("external", "external-tool");
        var bundledStatus = CreateStatus("bundled-warning", false);
        var externalStatus = CreateStatus("external-warning", false);
        var bundledMaterialization = CreateMaterialization(bundledPlugin, true);
        var externalMaterialization = CreateMaterialization(externalPlugin, true);
        var bundledServiceProviderLifetime = new Mock<IDisposable>();
        var externalServiceProviderLifetime = new Mock<IDisposable>();
        bundledMaterialization = bundledMaterialization with
        {
            ServiceProviderLifetime = bundledServiceProviderLifetime.Object,
        };

        externalMaterialization = externalMaterialization with
        {
            ServiceProviderLifetime = externalServiceProviderLifetime.Object,
        };

        var loadContext = new AssemblyLoadContext("external");
        var discoveryResults = new[] { new PluginPackageDiscoveryResult { FallbackIdentity = "external" } };
        _candidatePreparer.Setup(static value => value.PrepareBundled(It.IsAny<IReadOnlyList<Assembly>>()))
            .Returns(new PluginCandidatePreparation
            {
                Plugins = [bundledPlugin],
                Statuses = [bundledStatus],
            });

        _packageDiscovery.Setup(static value => value.Discover(It.IsAny<IReadOnlyList<string>>())).Returns(discoveryResults);
        _candidatePreparer.Setup(value => value.PrepareExternal(discoveryResults, It.IsAny<IReadOnlySet<string>>()))
            .Returns(new PluginCandidatePreparation
            {
                Plugins = [externalPlugin],
                Statuses = [externalStatus],
                LoadContexts = [loadContext],
            });

        _entryMaterializer.Setup(value => value.Materialize(bundledPlugin)).Returns(bundledMaterialization);
        _entryMaterializer.Setup(value => value.Materialize(externalPlugin)).Returns(externalMaterialization);
        var target = CreateTarget();

        var result = target.Load(new StartupOptions { PluginDirectories = ["plugins"] }, [typeof(BundledCorePlugin).Assembly], ["reserved"]);

        result.Tools.Should().HaveCount(2);
        result.Plugins.Should().BeEquivalentTo([bundledStatus, bundledMaterialization.Status, externalStatus, externalMaterialization.Status]);
        result.LoadContexts.Should().ContainSingle().Which.Should().BeSameAs(loadContext);
        result.ServiceProviderLifetimes.Should().Equal(
            bundledServiceProviderLifetime.Object,
            externalServiceProviderLifetime.Object);
        _collisionPolicy.Verify(value => value.FindExternalToolCollisions(
            It.IsAny<IReadOnlyList<PreparedCatalogPlugin>>(),
            It.Is<IReadOnlySet<string>>(names => names.Count == 2 && names.Contains("reserved") && names.Contains("bundled-tool"))), Times.Once);
    }

    [Fact]
    public void GIVEN_BundledToolCollidesWithProtectedName_WHEN_Loading_THEN_ShouldFailBeforeMaterialization()
    {
        var plugin = CreatePreparedPlugin("bundled", "reserved");
        _candidatePreparer.Setup(static value => value.PrepareBundled(It.IsAny<IReadOnlyList<Assembly>>()))
            .Returns(new PluginCandidatePreparation { Plugins = [plugin] });

        _collisionPolicy.Setup(value => value.FindProtectedToolCollision(plugin, It.IsAny<IReadOnlySet<string>>()))
            .Returns("reserved");

        var target = CreateTarget();

        var action = () => target.Load(new StartupOptions(), [typeof(BundledCorePlugin).Assembly], ["reserved"]);

        action.Should().Throw<InvalidOperationException>().WithMessage("*collides with a reserved*");
        _entryMaterializer.Verify(static value => value.Materialize(It.IsAny<PreparedCatalogPlugin>()), Times.Never);
    }

    [Fact]
    public void GIVEN_LaterBundledPluginCollidesWithEarlierPlugin_WHEN_Loading_THEN_ShouldValidateAllBeforeMaterialization()
    {
        var firstPlugin = CreatePreparedPlugin("first", "shared");
        var secondPlugin = CreatePreparedPlugin("second", "shared");
        _candidatePreparer.Setup(static value => value.PrepareBundled(It.IsAny<IReadOnlyList<Assembly>>()))
            .Returns(new PluginCandidatePreparation { Plugins = [firstPlugin, secondPlugin] });

        _collisionPolicy.Setup(value => value.FindProtectedToolCollision(firstPlugin, It.IsAny<IReadOnlySet<string>>()))
            .Returns((string?)null);

        _collisionPolicy.Setup(value => value.FindProtectedToolCollision(
            secondPlugin,
            It.Is<IReadOnlySet<string>>(names => names.Contains("shared"))))
            .Returns("shared");

        var target = CreateTarget();

        var action = () => target.Load(new StartupOptions(), [typeof(BundledCorePlugin).Assembly]);

        action.Should().Throw<InvalidOperationException>().WithMessage("*collides with a reserved*");
        _entryMaterializer.Verify(static value => value.Materialize(It.IsAny<PreparedCatalogPlugin>()), Times.Never);
    }

    [Fact]
    public void GIVEN_ExternalToolCollision_WHEN_Loading_THEN_ShouldDisablePluginWithoutMaterialization()
    {
        var plugin = CreatePreparedPlugin("external", "shared");
        _candidatePreparer.Setup(static value => value.PrepareExternal(
            It.IsAny<IReadOnlyList<PluginPackageDiscoveryResult>>(),
            It.IsAny<IReadOnlySet<string>>()))
            .Returns(new PluginCandidatePreparation { Plugins = [plugin] });

        _collisionPolicy.Setup(static value => value.FindExternalToolCollisions(
            It.IsAny<IReadOnlyList<PreparedCatalogPlugin>>(),
            It.IsAny<IReadOnlySet<string>>()))
            .Returns(new HashSet<string>(["external"], StringComparer.Ordinal));

        var target = CreateTarget();

        var result = target.Load(new StartupOptions(), []);

        result.Tools.Should().BeEmpty();
        result.Plugins.Should().ContainSingle(status =>
            !status.Enabled
            && status.PluginId == "external"
            && status.Diagnostics.Any(diagnostic =>
                diagnostic.Id == "PluginCollision"
                && diagnostic.Message.Contains("collide", StringComparison.Ordinal)));

        _entryMaterializer.Verify(static value => value.Materialize(It.IsAny<PreparedCatalogPlugin>()), Times.Never);
    }

    [Fact]
    public void GIVEN_LaterLoadingPhaseFails_WHEN_Loading_THEN_ShouldDisposeEarlierMaterializedPluginServices()
    {
        var bundledPlugin = CreatePreparedPlugin("bundled", "bundled-tool");
        var serviceProviderLifetime = new Mock<IDisposable>();
        var materialization = CreateMaterialization(bundledPlugin, true) with
        {
            ServiceProviderLifetime = serviceProviderLifetime.Object,
        };
        _candidatePreparer.Setup(static value => value.PrepareBundled(It.IsAny<IReadOnlyList<Assembly>>()))
            .Returns(new PluginCandidatePreparation { Plugins = [bundledPlugin] });
        _entryMaterializer.Setup(value => value.Materialize(bundledPlugin)).Returns(materialization);
        _packageDiscovery
            .Setup(static value => value.Discover(It.IsAny<IReadOnlyList<string>>()))
            .Throws(new InvalidOperationException("Discovery failed."));
        var target = CreateTarget();

        var action = () => target.Load(new StartupOptions(), [typeof(BundledCorePlugin).Assembly]);

        action.Should().Throw<InvalidOperationException>().WithMessage("Discovery failed.");
        serviceProviderLifetime.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public void GIVEN_LoadingAndCleanupFail_WHEN_Loading_THEN_ShouldRetainBothFailures()
    {
        var bundledPlugin = CreatePreparedPlugin("bundled", "bundled-tool");
        var serviceProviderLifetime = new Mock<IDisposable>();
        serviceProviderLifetime
            .Setup(item => item.Dispose())
            .Throws(new IOException("Cleanup failed."));
        var materialization = CreateMaterialization(bundledPlugin, true) with
        {
            ServiceProviderLifetime = serviceProviderLifetime.Object,
        };
        _candidatePreparer.Setup(static value => value.PrepareBundled(It.IsAny<IReadOnlyList<Assembly>>()))
            .Returns(new PluginCandidatePreparation { Plugins = [bundledPlugin] });
        _entryMaterializer.Setup(value => value.Materialize(bundledPlugin)).Returns(materialization);
        _packageDiscovery
            .Setup(static value => value.Discover(It.IsAny<IReadOnlyList<string>>()))
            .Throws(new InvalidOperationException("Discovery failed."));
        var target = CreateTarget();

        var action = () => target.Load(new StartupOptions(), [typeof(BundledCorePlugin).Assembly]);

        var exception = action.Should().Throw<AggregateException>();
        exception.Which.InnerExceptions.Should().ContainSingle(static item => item is InvalidOperationException);
        exception.Which.InnerExceptions.Should().ContainSingle(static item => item is AggregateException);
        serviceProviderLifetime.Verify(item => item.Dispose(), Times.Once);
    }

    private PluginCatalogLoader CreateTarget()
    {
        return new PluginCatalogLoader(
            _candidatePreparer.Object,
            _entryMaterializer.Object,
            _collisionPolicy.Object,
            _packageDiscovery.Object);
    }

    private static PreparedCatalogPlugin CreatePreparedPlugin(string pluginId, string toolName)
    {
        return new PreparedCatalogPlugin
        {
            Metadata = CreateMetadata(pluginId),
            Preparation = new PluginPreparationResult
            {
                Tools = [CreatePreparedTool(pluginId, toolName)],
            },
        };
    }

    private static PreparedPluginTool CreatePreparedTool(string pluginId, string toolName)
    {
        return new PreparedPluginTool
        {
            HandlerType = typeof(object),
            HandlerContract = typeof(object),
            Tool = new RegisteredTool
            {
                Plugin = CreateMetadata(pluginId),
                Metadata = new ToolRegistrationMetadata
                {
                    Name = toolName,
                    Title = "Title",
                    Description = "Description",
                },
            },
        };
    }

    private static PluginMetadata CreateMetadata(string pluginId)
    {
        return new PluginMetadata
        {
            PluginId = pluginId,
            DisplayName = "DisplayName",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
        };
    }

    private static PluginStatus CreateStatus(string pluginId, bool enabled)
    {
        return new PluginStatus
        {
            PluginId = pluginId,
            DisplayName = "DisplayName",
            Version = "1.0.0",
            SupportedApiVersion = PluginApiVersions.V1,
            Enabled = enabled,
        };
    }

    private static PluginCatalogEntryMaterialization CreateMaterialization(PreparedCatalogPlugin plugin, bool enabled)
    {
        var registration = new Mock<IRegisteredPluginTool>();
        registration.SetupGet(static value => value.Tool).Returns(plugin.Preparation.Tools.Single().Tool);
        return new PluginCatalogEntryMaterialization
        {
            Tools = [registration.Object],
            Status = CreateStatus(plugin.Metadata.PluginId, enabled),
        };
    }
}
