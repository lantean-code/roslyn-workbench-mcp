namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginAssemblyMetadataReader
{
    PluginAssemblyInspection Inspect(string assemblyPath);
}
