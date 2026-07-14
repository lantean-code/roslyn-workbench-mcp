namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class PluginToolCatalogue
{
    internal IReadOnlyList<IRegisteredPluginTool> Tools { get; }

    internal PluginToolCatalogue(IReadOnlyList<IRegisteredPluginTool> tools)
    {
        Tools = tools;
    }
}
