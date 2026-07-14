namespace Roslyn.Workbench.Mcp.Test;

public sealed class PluginEntryPointValidatorTests
{
    private readonly PluginEntryPointValidator _target;

    public PluginEntryPointValidatorTests()
    {
        _target = new PluginEntryPointValidator();
    }

    [Theory]
    [InlineData("", "DisplayName", "1.0.0", PluginApiVersions.V1, "must provide")]
    [InlineData("PluginId", "", "1.0.0", PluginApiVersions.V1, "must provide")]
    [InlineData("PluginId", "DisplayName", "1.0.0", "", "must provide")]
    [InlineData("PluginId", "DisplayName", "invalid", PluginApiVersions.V1, "invalid AssemblyInformationalVersion")]
    [InlineData("PluginId", "DisplayName", "1.0.0", "9.9", "unsupported API version")]
    public void GIVEN_InvalidEntryPoint_WHEN_Validating_THEN_ShouldReturnValidationError(
        string pluginId,
        string displayName,
        string version,
        string apiVersion,
        string expectedMessage)
    {
        var entryPoint = CreateEntryPoint(pluginId, displayName, version, apiVersion);

        var result = _target.GetValidationError(entryPoint);

        result.Should().Contain(expectedMessage);
    }

    [Fact]
    public void GIVEN_ValidEntryPoint_WHEN_Validating_THEN_ShouldReturnNoError()
    {
        var entryPoint = CreateEntryPoint("PluginId", "DisplayName", "1.0.0", PluginApiVersions.V1);

        var result = _target.GetValidationError(entryPoint);

        result.Should().BeNull();
    }

    private static PluginEntryPointMetadata CreateEntryPoint(
        string pluginId,
        string displayName,
        string version,
        string apiVersion)
    {
        return new PluginEntryPointMetadata
        {
            PluginId = pluginId,
            DisplayName = displayName,
            Version = version,
            SupportedApiVersion = apiVersion,
        };
    }
}
