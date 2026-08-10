using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ToolExecution.Plugins;

namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginCatalogStartupLifecycleServiceTests
{
    [Fact]
    public async Task GIVEN_LoadedPluginCatalogue_WHEN_HostIsStarting_THEN_ShouldPublishAtomicRuntimeCatalogue()
    {
        var catalog = new PluginCatalogSnapshot();
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
        var loader = new Mock<IPluginCatalogLoader>();
        loader
            .Setup(value => value.Load(
                It.IsAny<StartupOptions>(),
                It.IsAny<IReadOnlyList<System.Reflection.Assembly>>(),
                It.IsAny<IEnumerable<string>>()))
            .Callback(cancellationSource.Cancel)
            .Returns(new PluginCatalogSnapshot());
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
    }
}
