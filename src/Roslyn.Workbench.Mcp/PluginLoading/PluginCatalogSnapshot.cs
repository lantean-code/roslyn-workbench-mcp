using System.Runtime.Loader;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed record PluginCatalogSnapshot
{
    public IReadOnlyList<IRegisteredPluginTool> Tools { get; init; } = [];

    public IReadOnlyList<PluginStatus> Plugins { get; init; } = [];

    public IReadOnlyList<AssemblyLoadContext> LoadContexts { get; init; } = [];
}
