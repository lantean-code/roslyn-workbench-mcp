using System.Diagnostics.CodeAnalysis;
using System.Runtime.Loader;

namespace Roslyn.Workbench.Mcp;

internal interface IPluginLoadContextFactory
{
    bool TryCreate(
        string packageDirectory,
        string entryAssemblyPath,
        [NotNullWhen(true)] out AssemblyLoadContext? loadContext);
}
