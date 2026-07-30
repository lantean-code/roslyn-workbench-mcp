using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.PluginFixtures;

[RoslynPlugin("test.throwing.configuration", "Throwing Configuration Test Plugin", PluginApiVersions.V1)]
public sealed class ThrowingConfigurationTestPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        configuration;

        throw new InvalidOperationException("Configuration failed.");
    }
}
