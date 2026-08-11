using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginCatalogStartupLifecycleServiceTests
{
    [Fact]
    public async Task GIVEN_LoadedPluginCatalogue_WHEN_HostIsStarting_THEN_ShouldPublishAtomicRuntimeCatalogue()
    {
        var serviceProviderLifetime = new Mock<IDisposable>();
        using var catalog = new PluginCatalogSnapshot
        {
            ServiceProviderLifetimes = [serviceProviderLifetime.Object],
        };
        var loader = new Mock<IPluginCatalogLoader>();
        loader
            .Setup(value => value.Load(
                It.IsAny<StartupOptions>(),
                It.IsAny<IReadOnlyList<System.Reflection.Assembly>>(),
                It.IsAny<IEnumerable<string>>()))
            .Returns(catalog);
        var toolFactory = new Mock<IPluginMcpServerToolFactory>();
        var catalogState = new Mock<IPluginCatalogState>();
        var codeActionCatalog = new CodeActionCatalogSnapshot();
        var target = new PluginCatalogStartupLifecycleService(
            loader.Object,
            toolFactory.Object,
            catalogState.Object,
            Options.Create(new StartupOptions()),
            codeActionCatalog);

        await target.StartingAsync(TestContext.Current.CancellationToken);

        catalogState.Verify(state => state.Publish(It.Is<PluginRuntimeCatalogSnapshot>(snapshot =>
            ReferenceEquals(snapshot.Catalog, catalog)
            && snapshot.Tools.Count == 0)), Times.Once);
        serviceProviderLifetime.Verify(item => item.Dispose(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CancelledHostStart_WHEN_StartingPluginCatalogue_THEN_ShouldNotLoadPlugins()
    {
        var loader = new Mock<IPluginCatalogLoader>();
        var toolFactory = new Mock<IPluginMcpServerToolFactory>();
        var catalogState = new Mock<IPluginCatalogState>();
        var codeActionCatalog = new CodeActionCatalogSnapshot();
        var target = new PluginCatalogStartupLifecycleService(
            loader.Object,
            toolFactory.Object,
            catalogState.Object,
            Options.Create(new StartupOptions()),
            codeActionCatalog);
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await target.StartingAsync(cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        loader.Verify(value => value.Load(
            It.IsAny<StartupOptions>(),
            It.IsAny<IReadOnlyList<System.Reflection.Assembly>>(),
            It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_CancellationDuringPluginLoading_WHEN_StartingPluginCatalogue_THEN_ShouldNotPublishPartialState()
    {
        using var cancellationSource = new CancellationTokenSource();
        var serviceProviderLifetime = new Mock<IDisposable>();
        using var catalog = new PluginCatalogSnapshot
        {
            ServiceProviderLifetimes = [serviceProviderLifetime.Object],
        };
        var loader = new Mock<IPluginCatalogLoader>();
        loader
            .Setup(value => value.Load(
                It.IsAny<StartupOptions>(),
                It.IsAny<IReadOnlyList<System.Reflection.Assembly>>(),
                It.IsAny<IEnumerable<string>>()))
            .Callback(cancellationSource.Cancel)
            .Returns(catalog);
        var toolFactory = new Mock<IPluginMcpServerToolFactory>();
        var catalogState = new Mock<IPluginCatalogState>();
        var codeActionCatalog = new CodeActionCatalogSnapshot();
        var target = new PluginCatalogStartupLifecycleService(
            loader.Object,
            toolFactory.Object,
            catalogState.Object,
            Options.Create(new StartupOptions()),
            codeActionCatalog);

        var action = async () => await target.StartingAsync(cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        catalogState.Verify(
            state => state.Publish(It.IsAny<PluginRuntimeCatalogSnapshot>()),
            Times.Never);
        serviceProviderLifetime.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ToolPublicationFails_WHEN_StartingPluginCatalogue_THEN_ShouldDisposeProvisionalPluginServices()
    {
        var serviceProviderLifetime = new Mock<IDisposable>();
        var registration = new Mock<IRegisteredPluginTool>();
        using var catalog = new PluginCatalogSnapshot
        {
            Tools = [registration.Object],
            ServiceProviderLifetimes = [serviceProviderLifetime.Object],
        };
        var loader = new Mock<IPluginCatalogLoader>();
        loader
            .Setup(value => value.Load(
                It.IsAny<StartupOptions>(),
                It.IsAny<IReadOnlyList<System.Reflection.Assembly>>(),
                It.IsAny<IEnumerable<string>>()))
            .Returns(catalog);
        var toolFactory = new Mock<IPluginMcpServerToolFactory>();
        registration
            .Setup(value => value.Accept(toolFactory.Object))
            .Throws(new InvalidOperationException("Tool publication failed."));
        var catalogState = new Mock<IPluginCatalogState>();
        var codeActionCatalog = new CodeActionCatalogSnapshot();
        var target = new PluginCatalogStartupLifecycleService(
            loader.Object,
            toolFactory.Object,
            catalogState.Object,
            Options.Create(new StartupOptions()),
            codeActionCatalog);

        var action = async () => await target.StartingAsync(TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tool publication failed.");
        serviceProviderLifetime.Verify(item => item.Dispose(), Times.Once);
        catalogState.Verify(
            state => state.Publish(It.IsAny<PluginRuntimeCatalogSnapshot>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_CataloguePublicationFails_WHEN_StartingPluginCatalogue_THEN_ShouldDisposeProvisionalPluginServices()
    {
        var serviceProviderLifetime = new Mock<IDisposable>();
        using var catalog = new PluginCatalogSnapshot
        {
            ServiceProviderLifetimes = [serviceProviderLifetime.Object],
        };
        var loader = new Mock<IPluginCatalogLoader>();
        loader
            .Setup(value => value.Load(
                It.IsAny<StartupOptions>(),
                It.IsAny<IReadOnlyList<System.Reflection.Assembly>>(),
                It.IsAny<IEnumerable<string>>()))
            .Returns(catalog);
        var toolFactory = new Mock<IPluginMcpServerToolFactory>();
        var catalogState = new Mock<IPluginCatalogState>();
        catalogState
            .Setup(state => state.Publish(It.IsAny<PluginRuntimeCatalogSnapshot>()))
            .Throws(new InvalidOperationException("Publication failed."));
        var codeActionCatalog = new CodeActionCatalogSnapshot();
        var target = new PluginCatalogStartupLifecycleService(
            loader.Object,
            toolFactory.Object,
            catalogState.Object,
            Options.Create(new StartupOptions()),
            codeActionCatalog);

        var action = async () => await target.StartingAsync(TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Publication failed.");
        serviceProviderLifetime.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GIVEN_StartupAndCleanupFail_WHEN_StartingPluginCatalogue_THEN_ShouldRetainBothFailures()
    {
        var serviceProviderLifetime = new Mock<IDisposable>();
        serviceProviderLifetime
            .Setup(item => item.Dispose())
            .Throws(new IOException("Cleanup failed."));
        using var catalog = new PluginCatalogSnapshot
        {
            ServiceProviderLifetimes = [serviceProviderLifetime.Object],
        };
        var loader = new Mock<IPluginCatalogLoader>();
        loader
            .Setup(value => value.Load(
                It.IsAny<StartupOptions>(),
                It.IsAny<IReadOnlyList<System.Reflection.Assembly>>(),
                It.IsAny<IEnumerable<string>>()))
            .Returns(catalog);
        var toolFactory = new Mock<IPluginMcpServerToolFactory>();
        var catalogState = new Mock<IPluginCatalogState>();
        catalogState
            .Setup(state => state.Publish(It.IsAny<PluginRuntimeCatalogSnapshot>()))
            .Throws(new InvalidOperationException("Publication failed."));
        var codeActionCatalog = new CodeActionCatalogSnapshot();
        var target = new PluginCatalogStartupLifecycleService(
            loader.Object,
            toolFactory.Object,
            catalogState.Object,
            Options.Create(new StartupOptions()),
            codeActionCatalog);

        var action = async () => await target.StartingAsync(TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<AggregateException>();
        exception.Which.InnerExceptions.Should().ContainSingle(static item => item is InvalidOperationException);
        exception.Which.InnerExceptions.Should().ContainSingle(static item => item is AggregateException);
        serviceProviderLifetime.Verify(item => item.Dispose(), Times.Once);
    }
}
