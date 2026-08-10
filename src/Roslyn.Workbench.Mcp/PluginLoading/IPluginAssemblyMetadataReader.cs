namespace Roslyn.Workbench.Mcp.PluginLoading;

internal interface IPluginAssemblyMetadataReader
{
    PluginAssemblyInspectionResult Inspect(string assemblyPath);
}
