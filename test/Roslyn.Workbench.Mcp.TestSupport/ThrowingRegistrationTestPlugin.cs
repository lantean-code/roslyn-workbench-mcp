using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed class ThrowingRegistrationTestPlugin : IRoslynPlugin
{
    public PluginMetadata Metadata => new()
    {
        PluginId = "test.throwing.registration",
        DisplayName = "Throwing Registration Test Plugin",
        Version = "1.0.0",
        SupportedApiVersion = PluginApiVersions.V1,
    };

    public void Register(IPluginRegistry registry)
    {
        _ = registry;

        throw new InvalidOperationException("Registration failed.");
    }
}
