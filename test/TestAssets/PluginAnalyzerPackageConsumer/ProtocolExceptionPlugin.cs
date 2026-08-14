using ModelContextProtocol;
using Roslyn.Workbench.Mcp.Plugins;

[RoslynPlugin("example.tools", "Example Tools", PluginApiVersions.V1)]
public sealed class ExamplePlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
    }
}

public static class ProtocolFailure
{
    public static void Throw()
    {
        throw new McpProtocolException();
    }
}
