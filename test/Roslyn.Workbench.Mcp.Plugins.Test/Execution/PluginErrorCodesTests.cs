namespace Roslyn.Workbench.Mcp.Plugins.Test.Execution;

public sealed class PluginErrorCodesTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_PluginErrorCodes_WHEN_ComparingCompatibilityContract_THEN_ShouldRetainExactValues()
    {
        var actual = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(PluginErrorCodes.AnalysisLimitExceeded)] = PluginErrorCodes.AnalysisLimitExceeded,
            [nameof(PluginErrorCodes.DocumentAmbiguous)] = PluginErrorCodes.DocumentAmbiguous,
            [nameof(PluginErrorCodes.DocumentNotFound)] = PluginErrorCodes.DocumentNotFound,
            [nameof(PluginErrorCodes.DocumentSelectorInvalid)] = PluginErrorCodes.DocumentSelectorInvalid,
            [nameof(PluginErrorCodes.InvalidRequest)] = PluginErrorCodes.InvalidRequest,
            [nameof(PluginErrorCodes.LocationAmbiguous)] = PluginErrorCodes.LocationAmbiguous,
            [nameof(PluginErrorCodes.LocationNotFound)] = PluginErrorCodes.LocationNotFound,
            [nameof(PluginErrorCodes.LocationSelectorInvalid)] = PluginErrorCodes.LocationSelectorInvalid,
            [nameof(PluginErrorCodes.ProjectAmbiguous)] = PluginErrorCodes.ProjectAmbiguous,
            [nameof(PluginErrorCodes.ProjectNotFound)] = PluginErrorCodes.ProjectNotFound,
            [nameof(PluginErrorCodes.ProjectSelectorInvalid)] = PluginErrorCodes.ProjectSelectorInvalid,
            [nameof(PluginErrorCodes.ProjectStructureUnavailable)] = PluginErrorCodes.ProjectStructureUnavailable,
            [nameof(PluginErrorCodes.SnapshotMismatch)] = PluginErrorCodes.SnapshotMismatch,
            [nameof(PluginErrorCodes.SymbolAmbiguous)] = PluginErrorCodes.SymbolAmbiguous,
            [nameof(PluginErrorCodes.SymbolNotFound)] = PluginErrorCodes.SymbolNotFound,
            [nameof(PluginErrorCodes.SymbolSelectorInvalid)] = PluginErrorCodes.SymbolSelectorInvalid,
            [nameof(PluginErrorCodes.UnhandledException)] = PluginErrorCodes.UnhandledException,
        };

        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AnalysisLimitExceeded"] = "AnalysisLimitExceeded",
            ["DocumentAmbiguous"] = "DocumentAmbiguous",
            ["DocumentNotFound"] = "DocumentNotFound",
            ["DocumentSelectorInvalid"] = "DocumentSelectorInvalid",
            ["InvalidRequest"] = "InvalidRequest",
            ["LocationAmbiguous"] = "LocationAmbiguous",
            ["LocationNotFound"] = "LocationNotFound",
            ["LocationSelectorInvalid"] = "LocationSelectorInvalid",
            ["ProjectAmbiguous"] = "ProjectAmbiguous",
            ["ProjectNotFound"] = "ProjectNotFound",
            ["ProjectSelectorInvalid"] = "ProjectSelectorInvalid",
            ["ProjectStructureUnavailable"] = "ProjectStructureUnavailable",
            ["SnapshotMismatch"] = "SnapshotMismatch",
            ["SymbolAmbiguous"] = "SymbolAmbiguous",
            ["SymbolNotFound"] = "SymbolNotFound",
            ["SymbolSelectorInvalid"] = "SymbolSelectorInvalid",
            ["UnhandledException"] = "UnhandledException",
        };

        actual.Should().BeEquivalentTo(expected);
    }
}
