using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal static class CodeActionProviderCatalogFactory
{
    public static ICodeActionProviderCatalog Create(CodeActionCompositionOptions options)
    {
        return new MefCodeActionProviderCatalog(
            Options.Create(options),
            new MefHostExportProviderCompatibilityAdapter());
    }
}
