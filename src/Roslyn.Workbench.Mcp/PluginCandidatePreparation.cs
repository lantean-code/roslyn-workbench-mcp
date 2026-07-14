using System.Runtime.Loader;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp;

internal sealed record PluginCandidatePreparation
{
    public IReadOnlyList<PreparedCatalogPlugin> Plugins { get; init; } = [];

    public IReadOnlyList<PluginStatus> Statuses { get; init; } = [];

    public IReadOnlyList<AssemblyLoadContext> LoadContexts { get; init; } = [];
}
