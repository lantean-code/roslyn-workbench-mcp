using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.PluginLoading;

/// <summary>
/// Builds the startup plugin catalogue while isolating failures to individual external plugins where possible.
/// </summary>
internal sealed class PluginCatalogLoader : IPluginCatalogLoader
{
    private readonly IPluginCandidatePreparer _candidatePreparer;
    private readonly IPluginCatalogEntryMaterializer _entryMaterializer;
    private readonly IPluginCollisionPolicy _collisionPolicy;
    private readonly IPluginPackageDiscovery _packageDiscovery;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginCatalogLoader"/> class.
    /// </summary>
    /// <param name="candidatePreparer">The component that validates and prepares discovered plugin candidates.</param>
    /// <param name="entryMaterializer">The component that creates runtime entries for prepared plugins.</param>
    /// <param name="collisionPolicy">The policy that rejects duplicate plugins and protected tool names.</param>
    /// <param name="packageDiscovery">The component that discovers plugin packages beneath configured roots.</param>
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

    /// <summary>
    /// Discovers, validates, collision-checks and materializes bundled and external plugins.
    /// </summary>
    /// <param name="startupOptions">The configured external plugin directories.</param>
    /// <param name="bundledAssemblies">The bundled assemblies to include in discovery.</param>
    /// <param name="reservedToolNames">The host-owned tool names that external plugins may not publish.</param>
    /// <returns>The immutable catalogue snapshot and all resources it owns.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Catalogue loading must retain both the original loading failure and any failure while releasing plugin providers materialized by an earlier phase.")]
    public PluginCatalogSnapshot Load(
        StartupOptions startupOptions,
        IReadOnlyList<Assembly> bundledAssemblies,
        IEnumerable<string>? reservedToolNames = null)
    {
        var tools = new List<IRegisteredPluginTool>();
        var statuses = new List<PluginStatus>();
        var loadContexts = new List<AssemblyLoadContext>();
        var serviceProviderLifetimes = new List<IDisposable>();
        var protectedToolNames = new HashSet<string>(reservedToolNames ?? [], StringComparer.Ordinal);

        try
        {
            LoadBundledPlugins(
                bundledAssemblies,
                protectedToolNames,
                tools,
                statuses,
                serviceProviderLifetimes);

            LoadExternalPlugins(
                startupOptions,
                protectedToolNames,
                tools,
                statuses,
                loadContexts,
                serviceProviderLifetimes);

            return new PluginCatalogSnapshot
            {
                Tools = tools.ToImmutableArray(),
                Plugins = statuses.ToImmutableArray(),
                LoadContexts = loadContexts.ToImmutableArray(),
                ServiceProviderLifetimes = serviceProviderLifetimes.ToImmutableArray(),
            };
        }
        catch (Exception loadingException)
        {
            DisposeMaterializedProviders(serviceProviderLifetimes, loadingException);
            throw;
        }
    }

    private static void DisposeMaterializedProviders(
        IReadOnlyList<IDisposable> serviceProviderLifetimes,
        Exception loadingException)
    {
        var partialCatalog = new PluginCatalogSnapshot
        {
            ServiceProviderLifetimes = serviceProviderLifetimes,
        };

        try
        {
            partialCatalog.Dispose();
        }
        catch (Exception disposalException)
        {
            throw new AggregateException(
                "Plugin catalogue loading failed and one or more materialized plugin service providers also failed during disposal.",
                loadingException,
                disposalException);
        }
    }

    private void LoadBundledPlugins(
        IReadOnlyList<Assembly> bundledAssemblies,
        HashSet<string> protectedToolNames,
        ICollection<IRegisteredPluginTool> tools,
        ICollection<PluginStatus> statuses,
        ICollection<IDisposable> serviceProviderLifetimes)
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
            AddMaterializedPlugin(plugin, tools, statuses, serviceProviderLifetimes);
        }
    }

    private void LoadExternalPlugins(
        StartupOptions startupOptions,
        IReadOnlySet<string> protectedToolNames,
        ICollection<IRegisteredPluginTool> tools,
        List<PluginStatus> statuses,
        ICollection<AssemblyLoadContext> loadContexts,
        ICollection<IDisposable> serviceProviderLifetimes)
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

            AddMaterializedPlugin(plugin, tools, statuses, serviceProviderLifetimes);
        }
    }

    private void AddMaterializedPlugin(
        PreparedCatalogPlugin plugin,
        ICollection<IRegisteredPluginTool> tools,
        ICollection<PluginStatus> statuses,
        ICollection<IDisposable> serviceProviderLifetimes)
    {
        var materialization = _entryMaterializer.Materialize(plugin);
        foreach (var tool in materialization.Tools)
        {
            tools.Add(tool);
        }

        if (materialization.ServiceProviderLifetime is not null)
        {
            serviceProviderLifetimes.Add(materialization.ServiceProviderLifetime);
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
