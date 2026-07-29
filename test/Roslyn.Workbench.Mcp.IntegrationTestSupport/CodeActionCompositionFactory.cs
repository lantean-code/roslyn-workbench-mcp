using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal static class CodeActionCompositionFactory
{
    public static ICodeActionComposition Create(CodeActionCompositionOptions options)
    {
        var configuredOptions = Options.Create(options);
        var exportProvider = new MefHostExportProviderCompatibilityAdapter();

        return new MefCodeActionComposition(
            configuredOptions,
            exportProvider);
    }
}
