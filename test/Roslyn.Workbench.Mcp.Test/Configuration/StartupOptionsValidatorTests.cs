using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Configuration;

public sealed class StartupOptionsValidatorTests
{
    private readonly StartupOptionsValidator _target;

    public StartupOptionsValidatorTests()
    {
        _target = new StartupOptionsValidator();
    }

    [Fact]
    public void GIVEN_DefaultOptions_WHEN_Validating_THEN_ShouldSucceed()
    {
        var result = _target.Validate(null, new StartupOptions());

        result.Succeeded.Should().BeTrue();
        result.Failures.Should().BeNull();
    }

    [Fact]
    public void GIVEN_AllInvalidOptions_WHEN_Validating_THEN_ShouldReportEveryFailure()
    {
        var options = CreateInvalidOptions();

        var result = _target.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().BeEquivalentTo(
        [
            "DefaultMaxResults must be greater than zero.",
            "CodeActionTokenLifetime must be greater than zero and no greater than 1.00:00:00.",
            "MaxTransactionRevisions must be greater than zero.",
            "MaxConcurrentQueries must be greater than zero.",
            "ToolOutputSchemaMode must be a supported value.",
            "StateDirectory must be a valid non-blank path.",
            "PluginDirectories must not contain blank paths.",
        ]);
    }

    [Fact]
    public void GIVEN_DefaultOptions_WHEN_EnsuringValidity_THEN_ShouldNotThrow()
    {
        var action = () => _target.EnsureValid(new StartupOptions());

        action.Should().NotThrow();
    }

    [Fact]
    public void GIVEN_InvalidOptions_WHEN_EnsuringValidity_THEN_ShouldThrowValidationException()
    {
        var options = CreateInvalidOptions();

        var action = () => _target.EnsureValid(options);

        action.Should().Throw<OptionsValidationException>()
            .WithMessage("*DefaultMaxResults must be greater than zero.*PluginDirectories must not contain blank paths.*");
    }

    [Fact]
    public void GIVEN_ExcessiveTokenLifetime_WHEN_Validating_THEN_ShouldReportFailure()
    {
        var options = new StartupOptions
        {
            CodeActionTokenLifetime = TimeSpan.FromDays(1) + TimeSpan.FromTicks(1),
        };

        var result = _target.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle().Which.Should().Be(
            "CodeActionTokenLifetime must be greater than zero and no greater than 1.00:00:00.");
    }

    private static StartupOptions CreateInvalidOptions()
    {
        return new StartupOptions
        {
            PluginDirectories = [" "],
            DefaultMaxResults = 0,
            CodeActionTokenLifetime = TimeSpan.Zero,
            MaxTransactionRevisions = 0,
            MaxConcurrentQueries = 0,
            ToolOutputSchemaMode = (ToolOutputSchemaMode)999,
            StateDirectory = " ",
        };
    }
}
