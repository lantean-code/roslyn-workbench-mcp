using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal sealed class PluginLoadContextFactory : IPluginLoadContextFactory
{
    private readonly IPluginPackagePathPolicy _packagePathPolicy;

    public PluginLoadContextFactory(IPluginPackagePathPolicy packagePathPolicy)
    {
        _packagePathPolicy = packagePathPolicy;
    }

    public bool TryCreate(
        string packageDirectory,
        string entryAssemblyPath,
        [NotNullWhen(true)] out AssemblyLoadContext? loadContext)
    {
        if (!_packagePathPolicy.TryGetContainedPath(packageDirectory, entryAssemblyPath, out var containedEntryAssemblyPath))
        {
            loadContext = null;
            return false;
        }

        loadContext = new PluginAssemblyLoadContext(packageDirectory, containedEntryAssemblyPath, _packagePathPolicy);
        return true;
    }
}
