using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

internal static class CodeActionCompositionFactory
{
    public static ICodeActionComposition Create(CodeActionCompositionOptions options)
    {
        return new MefCodeActionComposition(
            Options.Create(options),
            new MefHostExportProviderCompatibilityAdapter());
    }
}
