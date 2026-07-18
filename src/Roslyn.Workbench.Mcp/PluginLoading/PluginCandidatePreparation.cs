using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed record PluginCandidatePreparation
{
    public IReadOnlyList<PreparedCatalogPlugin> Plugins { get; init; } = [];

    public IReadOnlyList<PluginStatus> Statuses { get; init; } = [];

    public IReadOnlyList<AssemblyLoadContext> LoadContexts { get; init; } = [];
}
