using System.Reflection;

namespace Roslyn.Workbench.Mcp;

internal interface IPluginCandidatePreparer
{
    PluginCandidatePreparation PrepareBundled(IReadOnlyList<Assembly> bundledAssemblies);

    PluginCandidatePreparation PrepareExternal(
        IReadOnlyList<PluginPackageDiscoveryResult> discoveryResults,
        IReadOnlySet<string> duplicatePluginIds);
}
