using System.Reflection;

namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginComposer
{
    PluginCompositionResult Configure(Assembly assembly, IPluginConfiguration configuration);
}
