using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Collects prepared plugins, rejected-candidate statuses and external load contexts from one preparation phase.
/// </summary>
internal sealed record PluginCandidatePreparation
{
    /// <summary>
    /// Gets the candidates ready for collision checks and runtime materialization.
    /// </summary>
    public IReadOnlyList<PreparedCatalogPlugin> Plugins { get; init; } = [];

    /// <summary>
    /// Gets disabled status entries for candidates rejected during preparation.
    /// </summary>
    public IReadOnlyList<PluginStatus> Statuses { get; init; } = [];

    /// <summary>
    /// Gets the load contexts that keep accepted external plugin assemblies available for the process lifetime.
    /// </summary>
    public IReadOnlyList<AssemblyLoadContext> LoadContexts { get; init; } = [];
}
