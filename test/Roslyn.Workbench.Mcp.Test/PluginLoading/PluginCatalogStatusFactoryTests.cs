namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginCatalogStatusFactoryTests
{
    [Fact]
    public void GIVEN_ValidMetadata_WHEN_CreatingEnabledStatus_THEN_ShouldPreserveMetadata()
    {
        var metadata = new PluginMetadata
        {
            PluginId = "PluginId",
            DisplayName = "DisplayName",
            Version = "Version",
            SupportedApiVersion = "SupportedApiVersion",
        };

        var result = PluginCatalogStatusFactory.CreateEnabled(metadata, []);

        result.PluginId.Should().Be("PluginId");
        result.DisplayName.Should().Be("DisplayName");
        result.Version.Should().Be("Version");
        result.SupportedApiVersion.Should().Be("SupportedApiVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void GIVEN_UnavailableMetadata_WHEN_CreatingDisabledStatus_THEN_ShouldPublishNullMetadata(string unavailableValue)
    {
        var result = PluginCatalogStatusFactory.CreateDisabled(
            unavailableValue,
            unavailableValue,
            unavailableValue,
            unavailableValue,
            "Message");

        result.PluginId.Should().BeNull();
        result.DisplayName.Should().BeNull();
        result.Version.Should().BeNull();
        result.SupportedApiVersion.Should().BeNull();
    }
}
