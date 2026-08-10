namespace Roslyn.Workbench.Mcp.Plugins.Test.Validation;

public sealed class PluginToolNamePolicyTests
{
    [Fact]
    public void GIVEN_EveryAllowedCharacterAndMaximumLength_WHEN_Validating_THEN_ShouldAcceptNames()
    {
        var maximumLengthName = new string('a', PluginToolNamePolicy.MaximumLength);
        string[] names = ["AZaz09_.-", maximumLengthName];

        var results = names.Select(PluginToolNamePolicy.IsValid);

        results.Should().OnlyContain(static result => result);
    }

    [Fact]
    public void GIVEN_EmptyOversizedOrUnsupportedCharacters_WHEN_Validating_THEN_ShouldRejectNames()
    {
        var oversizedName = new string('a', PluginToolNamePolicy.MaximumLength + 1);
        string?[] names = [null, string.Empty, oversizedName, "has space", "has/slash", "naïve"];

        var results = names.Select(PluginToolNamePolicy.IsValid);

        results.Should().OnlyContain(static result => !result);
    }
}
