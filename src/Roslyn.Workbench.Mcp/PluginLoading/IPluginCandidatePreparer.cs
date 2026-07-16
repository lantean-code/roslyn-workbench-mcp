using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginCandidatePreparer
{
    PluginCandidatePreparation PrepareBundled(IReadOnlyList<Assembly> bundledAssemblies);

    PluginCandidatePreparation PrepareExternal(
        IReadOnlyList<PluginPackageDiscoveryResult> discoveryResults,
        IReadOnlySet<string> duplicatePluginIds);
}
