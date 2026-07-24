using Roslyn.Workbench.Mcp.Workspace.IO;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class HostStartupComposerTests
{
    [Fact]
    public void GIVEN_ValidStartupConfiguration_WHEN_Composing_THEN_ShouldReturnOrderedCatalogueSnapshots()
    {
        var pluginCatalog = new PluginCatalogSnapshot();
        var pluginCatalogBootstrap = new Mock<IPluginCatalogBootstrap>();
        pluginCatalogBootstrap
            .Setup(bootstrap => bootstrap.Load(
                It.IsAny<StartupOptions>(),
                It.IsAny<IReadOnlyList<System.Reflection.Assembly>>(),
                It.IsAny<IEnumerable<string>>()))
            .Returns(pluginCatalog);

        var pathComparison = new Mock<IWorkspacePathComparison>();
        pathComparison
            .Setup(comparison => comparison.GetComparer(It.IsAny<string>()))
            .Returns(StringComparer.Ordinal);

        var target = new HostStartupComposer(pluginCatalogBootstrap.Object, pathComparison.Object);

        var result = target.Compose(
        [
            "--plugin-directory=/missing/plugins",
            "--default-max-results=25",
            "--code-action-token-lifetime=00:10:00",
            "--max-transaction-revisions=30",
            "--max-concurrent-queries=4",
            "--tool-output-schema-mode=full",
            "--state-directory=/state",
        ]);

        result.Options.Should().BeSameAs(result.Configuration.Options);
        result.Configuration.Warnings.Should().BeEmpty();
        result.CodeActions.Tools.Should().NotBeEmpty();
        result.Plugins.Should().BeSameAs(pluginCatalog);

        var protectedToolNames = result.CodeActions.Tools
            .Select(static tool => tool.Metadata.Name)
            .Concat(ServerOwnedToolRegistration.ToolNames)
            .ToHashSet(StringComparer.Ordinal);

        pluginCatalogBootstrap.Verify(bootstrap => bootstrap.Load(
            result.Options,
            It.Is<IReadOnlyList<System.Reflection.Assembly>>(assemblies =>
                assemblies.Count == 1 && assemblies[0] == typeof(BundledCorePlugin).Assembly),
            It.Is<IEnumerable<string>>(toolNames => protectedToolNames.SetEquals(toolNames))), Times.Once);
    }
}
