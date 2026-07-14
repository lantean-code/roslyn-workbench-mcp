using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp;

internal sealed class PluginCatalogLoader
{
    private readonly IPluginCandidatePreparer _candidatePreparer;
    private readonly IPluginCatalogEntryMaterializer _entryMaterializer;
    private readonly IPluginCollisionPolicy _collisionPolicy;
    private readonly IPluginPackageDiscovery _packageDiscovery;

    public PluginCatalogLoader(
        IPluginCandidatePreparer candidatePreparer,
        IPluginCatalogEntryMaterializer entryMaterializer,
        IPluginCollisionPolicy collisionPolicy,
        IPluginPackageDiscovery packageDiscovery)
    {
        _candidatePreparer = candidatePreparer;
        _entryMaterializer = entryMaterializer;
        _collisionPolicy = collisionPolicy;
        _packageDiscovery = packageDiscovery;
    }

    public PluginCatalogSnapshot Load(
        StartupOptions startupOptions,
        IReadOnlyList<Assembly> bundledAssemblies,
        IEnumerable<string>? reservedToolNames = null)
    {
        var tools = new List<IRegisteredPluginTool>();
        var statuses = new List<PluginStatus>();
        var loadContexts = new List<AssemblyLoadContext>();
        var protectedToolNames = new HashSet<string>(reservedToolNames ?? [], StringComparer.Ordinal);

        LoadBundledPlugins(bundledAssemblies, protectedToolNames, tools, statuses);
        LoadExternalPlugins(startupOptions, protectedToolNames, tools, statuses, loadContexts);

        return new PluginCatalogSnapshot
        {
            Tools = tools.ToImmutableArray(),
            Plugins = statuses.ToImmutableArray(),
            LoadContexts = loadContexts.ToImmutableArray(),
        };
    }

    private void LoadBundledPlugins(
        IReadOnlyList<Assembly> bundledAssemblies,
        HashSet<string> protectedToolNames,
        ICollection<IRegisteredPluginTool> tools,
        ICollection<PluginStatus> statuses)
    {
        var preparation = _candidatePreparer.PrepareBundled(bundledAssemblies);
        AddStatuses(preparation.Statuses, statuses);

        foreach (var plugin in preparation.Plugins)
        {
            var collision = _collisionPolicy.FindProtectedToolCollision(plugin, protectedToolNames);
            if (collision is not null)
            {
                throw new InvalidOperationException($"Bundled plugin tool '{collision}' collides with a reserved tool name.");
            }

            protectedToolNames.UnionWith(GetToolNames(plugin));
        }

        foreach (var plugin in preparation.Plugins)
        {
            AddMaterializedPlugin(plugin, tools, statuses);
        }
    }

    private void LoadExternalPlugins(
        StartupOptions startupOptions,
        IReadOnlySet<string> protectedToolNames,
        ICollection<IRegisteredPluginTool> tools,
        ICollection<PluginStatus> statuses,
        ICollection<AssemblyLoadContext> loadContexts)
    {
        var discoveryResults = _packageDiscovery.Discover(startupOptions.PluginDirectories);
        var duplicateIds = _collisionPolicy.FindDuplicateExternalPluginIds(discoveryResults);
        var preparation = _candidatePreparer.PrepareExternal(discoveryResults, duplicateIds);
        AddStatuses(preparation.Statuses, statuses);
        AddLoadContexts(preparation.LoadContexts, loadContexts);

        var collidingPluginIds = _collisionPolicy.FindExternalToolCollisions(preparation.Plugins, protectedToolNames);
        foreach (var plugin in preparation.Plugins)
        {
            if (collidingPluginIds.Contains(plugin.Metadata.PluginId))
            {
                statuses.Add(PluginCatalogStatusFactory.CreateDisabled(
                    plugin.Metadata,
                    PluginDiagnosticIds.Collision,
                    "Plugin tool names collide with reserved, bundled, or other external plugin tools."));
                continue;
            }

            AddMaterializedPlugin(plugin, tools, statuses);
        }
    }

    private void AddMaterializedPlugin(
        PreparedCatalogPlugin plugin,
        ICollection<IRegisteredPluginTool> tools,
        ICollection<PluginStatus> statuses)
    {
        var materialization = _entryMaterializer.Materialize(plugin);
        foreach (var tool in materialization.Tools)
        {
            tools.Add(tool);
        }

        statuses.Add(materialization.Status);
    }

    private static IEnumerable<string> GetToolNames(PreparedCatalogPlugin plugin)
    {
        return plugin.Preparation.Tools.Select(static tool => tool.Tool.Metadata.Name);
    }

    private static void AddStatuses(IEnumerable<PluginStatus> source, ICollection<PluginStatus> destination)
    {
        foreach (var status in source)
        {
            destination.Add(status);
        }
    }

    private static void AddLoadContexts(IEnumerable<AssemblyLoadContext> source, ICollection<AssemblyLoadContext> destination)
    {
        foreach (var loadContext in source)
        {
            destination.Add(loadContext);
        }
    }
}
