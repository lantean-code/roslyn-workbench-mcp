namespace Roslyn.Workbench.Mcp;

internal interface IPluginAssemblyMetadataReader
{
    PluginAssemblyInspection Inspect(string assemblyPath);
}
