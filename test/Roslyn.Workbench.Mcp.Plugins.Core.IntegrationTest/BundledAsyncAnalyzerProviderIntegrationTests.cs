using Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class BundledAsyncAnalyzerProviderIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void GIVEN_PublishedAsyncFixerAssembly_WHEN_CreatingProvider_THEN_ShouldDiscoverSupportedAnalyzerSet()
    {
        var target = new BundledAsyncAnalyzerProvider();

        var diagnosticIds = target.Analyzers
            .SelectMany(static analyzer => analyzer.SupportedDiagnostics)
            .Select(static descriptor => descriptor.Id)
            .ToArray();

        diagnosticIds.Should().BeEquivalentTo(
            "AsyncFixer01",
            "AsyncFixer02",
            "AsyncFixer03",
            "AsyncFixer04",
            "AsyncFixer05",
            "AsyncFixer06");
    }
}
