namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal interface IMefHostExportProviderCompatibilityAdapter
{
    MefHostExportReadResult<T> ReadExports<T>(MefHostServices hostServices);
}
