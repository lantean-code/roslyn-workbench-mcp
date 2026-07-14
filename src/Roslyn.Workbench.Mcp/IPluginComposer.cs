using System.Reflection;

namespace Roslyn.Workbench.Mcp;

internal interface IPluginComposer
{
    PluginCompositionResult Configure(Assembly assembly, IPluginConfiguration configuration);
}
