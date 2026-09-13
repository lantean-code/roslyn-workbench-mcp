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
    public void GIVEN_OperationalModeConfigurationError_WHEN_Validating_THEN_ShouldFailWithCapturedError()
    {
        var options = new StartupOptions { OperationalModeConfigurationError = "ConfigurationError" };

        var result = _target.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Be("ConfigurationError");
    }

    [Fact]
    public void GIVEN_UnsupportedOperationalMode_WHEN_Validating_THEN_ShouldFail()
    {
        var options = new StartupOptions { OperationalMode = (OperationalMode)999 };

        var result = _target.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Be("OperationalMode must be a supported value.");
    }

    [Fact]
    public void GIVEN_ApprovalRequiredMode_WHEN_Validating_THEN_ShouldFailUntilReceiptApprovalExists()
    {
        var options = new StartupOptions { OperationalMode = OperationalMode.ApprovalRequired };

        var result = _target.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Contain("receipt-bound approval");
    }

    [Fact]
    public void GIVEN_PluginDirectoryWithoutExplicitEnablement_WHEN_Validating_THEN_ShouldFail()
    {
        var options = new StartupOptions { PluginDirectories = ["PluginDirectory"] };

        var result = _target.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Be("Plugin directories require the --enable-plugins switch.");
    }

    [Fact]
    public void GIVEN_PluginsEnabledWithoutDirectories_WHEN_Validating_THEN_ShouldSucceed()
    {
        var options = new StartupOptions { ExternalPluginsEnabled = true };

        var result = _target.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_PluginDirectoryEnabledInInspectionOnlyMode_WHEN_Validating_THEN_ShouldSucceed()
    {
        var options = new StartupOptions
        {
            ExternalPluginsEnabled = true,
            PluginDirectories = ["PluginDirectory"],
        };

        var result = _target.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_ExternalPluginsConfigurationError_WHEN_Validating_THEN_ShouldFailWithCapturedError()
    {
        var options = new StartupOptions { ExternalPluginsConfigurationError = "ConfigurationError" };

        var result = _target.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Be("ConfigurationError");
    }

    [Fact]
    public void GIVEN_ExistingAbsoluteWorkspaceRoot_WHEN_Validating_THEN_ShouldSucceed()
    {
        var options = new StartupOptions
        {
            AllowedWorkspaceRoots = [Path.GetTempPath()],
            ExternalDocumentPolicy = "reject-workspace",
        };

        var result = _target.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative")]
    public void GIVEN_InvalidWorkspaceRoot_WHEN_Validating_THEN_ShouldFail(string root)
    {
        var options = new StartupOptions { AllowedWorkspaceRoots = [root] };

        var result = _target.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Contain("AllowedWorkspaceRoots");
    }

    [Fact]
    public void GIVEN_MissingAbsoluteWorkspaceRoot_WHEN_Validating_THEN_ShouldFail()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var options = new StartupOptions { AllowedWorkspaceRoots = [root] };

        var result = _target.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Contain("AllowedWorkspaceRoots");
    }

    [Fact]
    public void GIVEN_UncanonicalisableAbsoluteWorkspaceRoot_WHEN_Validating_THEN_ShouldFail()
    {
        var root = Path.GetPathRoot(Path.GetTempPath()) + "\0";
        var options = new StartupOptions { AllowedWorkspaceRoots = [root] };

        var result = _target.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Contain("AllowedWorkspaceRoots");
    }

    [Fact]
    public void GIVEN_UnsupportedExternalDocumentPolicy_WHEN_Validating_THEN_ShouldFail()
    {
        var options = new StartupOptions { ExternalDocumentPolicy = "Allow" };

        var result = _target.Validate(null, options);

        result.Failures.Should().ContainSingle().Which.Should().Contain("ExternalDocumentPolicy");
    }

    [Fact]
    public void GIVEN_MinimumCodeActionReferenceCacheSize_WHEN_Validating_THEN_ShouldSucceed()
    {
        var options = new StartupOptions
        {
            CodeActionReferenceCacheSizeLimit = 40_000,
        };

        var result = _target.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void GIVEN_CodeActionReferenceCacheSizeBelowMinimum_WHEN_Validating_THEN_ShouldReportSupportedRange()
    {
        var options = new StartupOptions
        {
            CodeActionReferenceCacheSizeLimit = 39_999,
        };

        var result = _target.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle().Which.Should().Be(
            "CodeActionReferenceCacheSizeLimit must be between 40000 and 250000, inclusive.");
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
            "CodeActionReferenceLifetime must be greater than zero and no greater than 1.00:00:00.",
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
    public void GIVEN_ExcessiveReferenceLifetime_WHEN_Validating_THEN_ShouldReportFailure()
    {
        var options = new StartupOptions
        {
            CodeActionReferenceLifetime = TimeSpan.FromDays(1) + TimeSpan.FromTicks(1),
        };

        var result = _target.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().ContainSingle().Which.Should().Be(
            "CodeActionReferenceLifetime must be greater than zero and no greater than 1.00:00:00.");
    }

    [Fact]
    public void GIVEN_InvalidCacheAndErrorReportingOptions_WHEN_Validating_THEN_ShouldReportEveryFailure()
    {
        var options = new StartupOptions
        {
            WorkspaceQueryCacheSizeLimit = 4_999,
            PluginQueryCacheEntryLimit = 50_001,
            WorkspaceQueryCacheSlidingExpiration = TimeSpan.Zero,
            PluginQueryCacheSlidingExpiration = TimeSpan.FromDays(1) + TimeSpan.FromTicks(1),
            ErrorReporting = new ErrorReportingOptions
            {
                ConsentMode = (ErrorReportingConsentMode)999,
                CapturedErrorCapacity = 9,
                MaximumCapturedErrorBytes = 16_383,
                PreparedSubmissionCapacity = 4,
                MaximumPayloadBytes = 8_191,
                CapturedErrorLifetime = TimeSpan.Zero,
                PreparedSubmissionLifetime = TimeSpan.FromHours(4) + TimeSpan.FromTicks(1),
            },
        };

        var result = _target.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().BeEquivalentTo(
        [
            "WorkspaceQueryCacheSizeLimit must be between 5000 and 100000, inclusive.",
            "PluginQueryCacheEntryLimit must be between 7500 and 50000, inclusive.",
            "WorkspaceQueryCacheSlidingExpiration must be greater than zero and no greater than 1.00:00:00.",
            "PluginQueryCacheSlidingExpiration must be greater than zero and no greater than 1.00:00:00.",
            "ConsentMode must be a supported value.",
            "CapturedErrorCapacity must be between 10 and 1000, inclusive.",
            "MaximumCapturedErrorBytes must be between 16384 and 262144, inclusive.",
            "PreparedSubmissionCapacity must be between 5 and 500, inclusive.",
            "MaximumPayloadBytes must be between 8192 and 262144, inclusive.",
            "CapturedErrorLifetime must be greater than zero and no greater than 1.00:00:00.",
            "PreparedSubmissionLifetime must be greater than zero and no greater than 04:00:00.",
        ]);
    }

    private static StartupOptions CreateInvalidOptions()
    {
        return new StartupOptions
        {
            OperationalMode = OperationalMode.AutonomousTrusted,
            ExternalPluginsEnabled = true,
            PluginDirectories = [" "],
            DefaultMaxResults = 0,
            CodeActionReferenceLifetime = TimeSpan.Zero,
            MaxTransactionRevisions = 0,
            MaxConcurrentQueries = 0,
            ToolOutputSchemaMode = (ToolOutputSchemaMode)999,
            StateDirectory = " ",
        };
    }
}
