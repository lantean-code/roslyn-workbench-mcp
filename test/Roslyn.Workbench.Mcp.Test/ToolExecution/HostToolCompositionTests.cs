using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

public sealed class HostToolCompositionTests
{
    [Fact]
    public async Task GIVEN_CompleteHostComposition_WHEN_ValidatingContainer_THEN_ShouldResolveEveryRegisteredMcpTool()
    {
        var builder = Host.CreateApplicationBuilder();
        _ = builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);

        await using var serviceProvider = builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        var pluginCatalog = serviceProvider.GetRequiredService<PluginCatalogSnapshot>();
        var codeActionCatalog = serviceProvider.GetRequiredService<CodeActionCatalogSnapshot>();

        var tools = serviceProvider.GetServices<McpServerTool>().ToArray();

        tools.Should().HaveCount(
            pluginCatalog.Tools.Count
            + codeActionCatalog.Tools.Count
            + ServerOwnedToolRegistration.ToolCount);
        tools.Select(static tool => tool.ProtocolTool.Name).Should().OnlyHaveUniqueItems();
    }
}
