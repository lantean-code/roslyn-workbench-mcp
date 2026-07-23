namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Architecture;

public sealed class BundledCorePublicApiContractTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_BundledCoreAssembly_WHEN_InspectingExportedTypes_THEN_ShouldExposeOnlyPluginEntryPoint()
    {
        var exportedTypes = typeof(BundledCorePlugin).Assembly
            .GetExportedTypes()
            .Select(static type => type.FullName)
            .ToArray();

        exportedTypes.Should().Equal("Roslyn.Workbench.Mcp.Plugins.Core.BundledCorePlugin");
    }
}
