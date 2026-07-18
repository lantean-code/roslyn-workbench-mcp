using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginCandidatePreparer : IPluginCandidatePreparer
{
    private readonly IPluginAssemblyMetadataReader _metadataReader;
    private readonly IPluginEntryPointValidator _entryPointValidator;
    private readonly ILoadedPluginPreparer _loadedPluginPreparer;
    private readonly IPluginLoadContextFactory _loadContextFactory;

    public PluginCandidatePreparer(
        IPluginAssemblyMetadataReader metadataReader,
        IPluginEntryPointValidator entryPointValidator,
        ILoadedPluginPreparer loadedPluginPreparer,
        IPluginLoadContextFactory loadContextFactory)
    {
        _metadataReader = metadataReader;
        _entryPointValidator = entryPointValidator;
        _loadedPluginPreparer = loadedPluginPreparer;
        _loadContextFactory = loadContextFactory;
    }

    public PluginCandidatePreparation PrepareBundled(IReadOnlyList<Assembly> bundledAssemblies)
    {
        var plugins = new List<PreparedCatalogPlugin>();
        var statuses = new List<PluginStatus>();

        foreach (var assembly in bundledAssemblies.OrderBy(static assembly => assembly.FullName, StringComparer.Ordinal))
        {
            PrepareBundledAssembly(assembly, plugins, statuses);
        }

        return new PluginCandidatePreparation
        {
            Plugins = plugins,
            Statuses = statuses,
        };
    }

    public PluginCandidatePreparation PrepareExternal(
        IReadOnlyList<PluginPackageDiscoveryResult> discoveryResults,
        IReadOnlySet<string> duplicatePluginIds)
    {
        var plugins = new List<PreparedCatalogPlugin>();
        var statuses = new List<PluginStatus>();
        var loadContexts = new List<AssemblyLoadContext>();

        foreach (var discoveryResult in discoveryResults)
        {
            PrepareExternalPackage(discoveryResult, duplicatePluginIds, plugins, statuses, loadContexts);
        }

        return new PluginCandidatePreparation
        {
            Plugins = plugins,
            Statuses = statuses,
            LoadContexts = loadContexts,
        };
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Bundled plugin preparation executes plugin-owned configuration; any plugin-defined failure must disable only that plugin and be reported through catalogue status.")]
    private void PrepareBundledAssembly(
        Assembly assembly,
        ICollection<PreparedCatalogPlugin> plugins,
        ICollection<PluginStatus> statuses)
    {
        var inspection = _metadataReader.Inspect(assembly.Location);
        if (inspection.Error is not null || inspection.EntryPoints.Count != 1)
        {
            var identity = assembly.GetName().Name ?? "bundled-plugin";
            statuses.Add(PluginCatalogStatusFactory.CreateDisabled(
                identity,
                identity,
                string.Empty,
                PluginApiVersions.V1,
                PluginDiagnosticIds.Discovery,
                inspection.Error ?? "Bundled plugin assembly must contain exactly one RoslynPlugin entry point."));
            return;
        }

        var entryPoint = inspection.EntryPoints[0];
        var validationError = _entryPointValidator.GetValidationError(entryPoint);
        if (validationError is not null)
        {
            statuses.Add(PluginCatalogStatusFactory.CreateDisabled(entryPoint, PluginDiagnosticIds.Metadata, validationError));
            return;
        }

        try
        {
            AddPreparedPlugin(_loadedPluginPreparer.Prepare(assembly, entryPoint), plugins, statuses);
        }
        catch (Exception exception)
        {
            statuses.Add(PluginCatalogStatusFactory.CreateDisabled(
                entryPoint,
                $"Bundled plugin loading or configuration failed because {exception.GetType().Name} was raised."));
        }
    }

    private void PrepareExternalPackage(
        PluginPackageDiscoveryResult discoveryResult,
        IReadOnlySet<string> duplicatePluginIds,
        ICollection<PreparedCatalogPlugin> plugins,
        ICollection<PluginStatus> statuses,
        ICollection<AssemblyLoadContext> loadContexts)
    {
        var candidate = discoveryResult.Candidate;
        if (candidate is null)
        {
            statuses.Add(PluginCatalogStatusFactory.CreateDisabled(
                discoveryResult.FallbackIdentity,
                discoveryResult.FallbackIdentity,
                string.Empty,
                PluginApiVersions.V1,
                PluginDiagnosticIds.Discovery,
                discoveryResult.Error ?? "Plugin package discovery failed."));
            return;
        }

        var validationError = _entryPointValidator.GetValidationError(candidate.EntryPoint);
        if (validationError is not null)
        {
            statuses.Add(PluginCatalogStatusFactory.CreateDisabled(
                candidate.EntryPoint,
                PluginDiagnosticIds.Metadata,
                validationError));
            return;
        }

        if (duplicatePluginIds.Contains(candidate.EntryPoint.PluginId))
        {
            statuses.Add(PluginCatalogStatusFactory.CreateDisabled(
                candidate.EntryPoint,
                PluginDiagnosticIds.Collision,
                "Multiple external plugin packages declare the same plugin ID."));
            return;
        }

        PrepareExternalCandidate(candidate, plugins, statuses, loadContexts);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "External plugin loading and preparation execute third-party code; any plugin-defined failure must disable only that plugin and be reported through catalogue status.")]
    private void PrepareExternalCandidate(
        PluginPackageCandidate candidate,
        ICollection<PreparedCatalogPlugin> plugins,
        ICollection<PluginStatus> statuses,
        ICollection<AssemblyLoadContext> loadContexts)
    {
        try
        {
            if (!_loadContextFactory.TryCreate(candidate.PackageDirectory, candidate.EntryAssemblyPath, out var loadContext))
            {
                statuses.Add(PluginCatalogStatusFactory.CreateDisabled(
                    candidate.EntryPoint,
                    "External plugin entry assembly resolves outside its package directory."));
                return;
            }

            loadContexts.Add(loadContext);
            var assembly = loadContext.LoadFromAssemblyPath(candidate.EntryAssemblyPath);
            AddPreparedPlugin(_loadedPluginPreparer.Prepare(assembly, candidate.EntryPoint), plugins, statuses);
        }
        catch (Exception exception)
        {
            statuses.Add(PluginCatalogStatusFactory.CreateDisabled(
                candidate.EntryPoint,
                $"External plugin loading or configuration failed because {exception.GetType().Name} was raised."));
        }
    }

    private static void AddPreparedPlugin(
        PreparedCatalogPlugin plugin,
        ICollection<PreparedCatalogPlugin> plugins,
        ICollection<PluginStatus> statuses)
    {
        if (plugin.Preparation.Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            statuses.Add(PluginCatalogStatusFactory.CreateDisabled(plugin.Metadata, plugin.Preparation.Diagnostics));
            return;
        }

        plugins.Add(plugin);
    }

}
