using Roslyn.Workbench.Mcp.Workspace.IO;

namespace Roslyn.Workbench.Mcp.Test.Configuration;

[Collection("EnvironmentVariables")]
public sealed class StartupOptionsResolverTests
{
    private static readonly string[] _environmentVariables =
    [
        "ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY",
        "ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS",
        "ROSLYN_WORKBENCH_MCP_CODE_ACTION_REFERENCE_LIFETIME",
        "ROSLYN_WORKBENCH_MCP_WORKSPACE_QUERY_CACHE_SIZE_LIMIT",
        "ROSLYN_WORKBENCH_MCP_PLUGIN_QUERY_CACHE_ENTRY_LIMIT",
        "ROSLYN_WORKBENCH_MCP_CODE_ACTION_REFERENCE_CACHE_SIZE_LIMIT",
        "ROSLYN_WORKBENCH_MCP_WORKSPACE_QUERY_CACHE_SLIDING_EXPIRATION",
        "ROSLYN_WORKBENCH_MCP_PLUGIN_QUERY_CACHE_SLIDING_EXPIRATION",
        "ROSLYN_WORKBENCH_MCP_MAX_TRANSACTION_REVISIONS",
        "ROSLYN_WORKBENCH_MCP_MAX_CONCURRENT_QUERIES",
        "ROSLYN_WORKBENCH_MCP_TOOL_OUTPUT_SCHEMA_MODE",
        "ROSLYN_WORKBENCH_MCP_STATE_DIRECTORY",
        "ROSLYN_WORKBENCH_MCP_ERROR_REPORTING_CONSENT",
        "ROSLYN_WORKBENCH_MCP_ERROR_RECORD_CAPACITY",
        "ROSLYN_WORKBENCH_MCP_ERROR_RECORD_LIFETIME",
        "ROSLYN_WORKBENCH_MCP_ERROR_RECORD_MAX_BYTES",
        "ROSLYN_WORKBENCH_MCP_ERROR_SUBMISSION_CAPACITY",
        "ROSLYN_WORKBENCH_MCP_ERROR_SUBMISSION_LIFETIME",
        "ROSLYN_WORKBENCH_MCP_ERROR_REPORT_MAX_BYTES",
        "XDG_STATE_HOME",
    ];

    private readonly Mock<IWorkspacePathComparison> _pathComparison;

    public StartupOptionsResolverTests()
    {
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _pathComparison
            .Setup(comparison => comparison.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: true));
    }

    [Fact]
    public void GIVEN_NoArgumentsOrEnvironment_WHEN_Resolving_THEN_ShouldReturnDefaultsWithoutWarnings()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve([]);

            result.Options.PluginDirectories.Should().BeEmpty();
            result.Options.DefaultMaxResults.Should().Be(100);
            result.Options.CodeActionReferenceLifetime.Should().Be(TimeSpan.FromMinutes(5));
            result.Options.WorkspaceQueryCacheSizeLimit.Should().Be(10_000);
            result.Options.PluginQueryCacheEntryLimit.Should().Be(10_000);
            result.Options.CodeActionReferenceCacheSizeLimit.Should().Be(75_000);
            result.Options.WorkspaceQueryCacheSlidingExpiration.Should().Be(TimeSpan.FromHours(1));
            result.Options.PluginQueryCacheSlidingExpiration.Should().Be(TimeSpan.FromHours(1));
            result.Options.MaxTransactionRevisions.Should().Be(20);
            result.Options.MaxConcurrentQueries.Should().Be(2);
            result.Options.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Omit);
            result.Options.StateDirectory.Should().Be(new StartupOptions().StateDirectory);
            result.Options.ErrorReporting.ConsentMode.Should().Be(ErrorReportingConsentMode.Prompt);
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_ExplicitAlwaysCommandLineConsent_WHEN_Resolving_THEN_ShouldEnablePermanentApproval()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(
            [
                "--error-reporting-consent=always",
            ]);

            result.Options.ErrorReporting.ConsentMode.Should().Be(ErrorReportingConsentMode.Always);
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Theory]
    [InlineData("never", "Never")]
    [InlineData("prompt", "Prompt")]
    public void GIVEN_ExplicitCommandLineConsent_WHEN_Resolving_THEN_ShouldUseRequestedMode(
        string configuredValue,
        string expectedMode)
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve([$"--error-reporting-consent={configuredValue}"]);

            result.Options.ErrorReporting.ConsentMode.ToString().Should().Be(expectedMode);
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_AmbientAlwaysConsent_WHEN_Resolving_THEN_ShouldIgnoreItAndWarn()
    {
        var previousValues = ClearEnvironment();

        try
        {
            Environment.SetEnvironmentVariable(
                "ROSLYN_WORKBENCH_MCP_ERROR_REPORTING_CONSENT",
                "always");

            var result = Resolve([]);

            result.Options.ErrorReporting.ConsentMode.Should().Be(ErrorReportingConsentMode.Prompt);
            result.Warnings.Should().ContainSingle();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_MalformedConsentChoice_WHEN_Resolving_THEN_ShouldFailClosed()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(
            [
                "--error-reporting-consent=Always",
            ]);

            result.Options.ErrorReporting.ConsentMode.Should().Be(ErrorReportingConsentMode.Never);
            result.Warnings.Should().ContainSingle();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_MinimumCodeActionReferenceCacheSize_WHEN_Resolving_THEN_ShouldRetainConfiguredValue()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(["--code-action-reference-cache-size-limit=40000"]);

            result.Options.CodeActionReferenceCacheSizeLimit.Should().Be(40_000);
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_CodeActionReferenceCacheSizeBelowMinimum_WHEN_Resolving_THEN_ShouldUseDefaultAndReportWarning()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(["--code-action-reference-cache-size-limit=39999"]);

            result.Options.CodeActionReferenceCacheSizeLimit.Should().Be(75_000);
            var expectedWarning = new WarningInfo
            {
                Code = "StartupConfigurationFallback",
                Message = "Configuration '--code-action-reference-cache-size-limit' is invalid; using default '75000'.",
            };

            result.Warnings.Should().ContainSingle().Which.Should().BeEquivalentTo(expectedWarning);
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_AbsoluteXdgStateHomeOnLinux_WHEN_Resolving_THEN_ShouldUseItForDefaultStateDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var previousValues = ClearEnvironment();

        try
        {
            Environment.SetEnvironmentVariable("XDG_STATE_HOME", "/state-home");

            var result = Resolve([]);

            result.Options.StateDirectory.Should().Be("/state-home/roslyn-workbench-mcp");
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_CommandLineValuesInSupportedForms_WHEN_Resolving_THEN_ShouldProjectTypedOptions()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(
            [
                "ignored",
                "--plugin-directory=/plugins/one",
                "--plugin-directory",
                "/plugins/two",
                "--default-max-results",
                "25",
                "--code-action-reference-lifetime=00:10:00",
                "--max-transaction-revisions",
                "30",
                "--max-concurrent-queries=4",
                "--tool-output-schema-mode=full",
                "--state-directory",
                "/state",
            ]);

            result.Options.PluginDirectories.Should().Equal("/plugins/one", "/plugins/two");
            result.Options.DefaultMaxResults.Should().Be(25);
            result.Options.CodeActionReferenceLifetime.Should().Be(TimeSpan.FromMinutes(10));
            result.Options.MaxTransactionRevisions.Should().Be(30);
            result.Options.MaxConcurrentQueries.Should().Be(4);
            result.Options.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Full);
            result.Options.StateDirectory.Should().Be("/state");
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_CaseSensitivePluginRoots_WHEN_Resolving_THEN_ShouldRetainCaseDistinctDirectories()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(
            [
                "--plugin-directory=/plugins/one",
                "--plugin-directory=/plugins/two",
                "--plugin-directory=/PLUGINS/ONE",
                "--default-max-results=10",
                "--default-max-results=25",
            ]);

            result.Options.PluginDirectories.Should().Equal("/plugins/one", "/plugins/two", "/PLUGINS/ONE");
            result.Options.DefaultMaxResults.Should().Be(25);
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_UnrelatedBlankAndMissingArguments_WHEN_Resolving_THEN_ShouldIgnoreThem()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(
            [
                "ignored",
                "--missing-value",
                "--blank=   ",
                "--last-missing",
            ]);

            result.Options.DefaultMaxResults.Should().Be(100);
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_EnvironmentValues_WHEN_Resolving_THEN_ShouldUseTypedEnvironmentConfiguration()
    {
        var previousValues = ClearEnvironment();

        try
        {
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY", $"/plugins/one{Path.PathSeparator} /plugins/two ");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS", "50");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_CODE_ACTION_REFERENCE_LIFETIME", "00:15:00");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_MAX_TRANSACTION_REVISIONS", "40");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_MAX_CONCURRENT_QUERIES", "6");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_TOOL_OUTPUT_SCHEMA_MODE", "Full");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_STATE_DIRECTORY", "/environment-state");

            var result = Resolve([]);

            result.Options.PluginDirectories.Should().Equal("/plugins/one", "/plugins/two");
            result.Options.DefaultMaxResults.Should().Be(50);
            result.Options.CodeActionReferenceLifetime.Should().Be(TimeSpan.FromMinutes(15));
            result.Options.MaxTransactionRevisions.Should().Be(40);
            result.Options.MaxConcurrentQueries.Should().Be(6);
            result.Options.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Full);
            result.Options.StateDirectory.Should().Be("/environment-state");
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_ValidCacheAndErrorReportingBounds_WHEN_Resolving_THEN_ShouldUseConfiguredValues()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(
            [
                "--workspace-query-cache-sliding-expiration=00:30:00",
                "--plugin-query-cache-sliding-expiration=00:45:00",
                "--error-record-capacity=10",
                "--error-record-lifetime=01:00:00",
                "--error-record-max-bytes=16384",
                "--error-submission-capacity=5",
                "--error-submission-lifetime=00:15:00",
                "--error-report-max-bytes=8192",
            ]);

            result.Options.WorkspaceQueryCacheSlidingExpiration.Should().Be(TimeSpan.FromMinutes(30));
            result.Options.PluginQueryCacheSlidingExpiration.Should().Be(TimeSpan.FromMinutes(45));
            result.Options.ErrorReporting.CapturedErrorCapacity.Should().Be(10);
            result.Options.ErrorReporting.CapturedErrorLifetime.Should().Be(TimeSpan.FromHours(1));
            result.Options.ErrorReporting.MaximumCapturedErrorBytes.Should().Be(16_384);
            result.Options.ErrorReporting.PreparedSubmissionCapacity.Should().Be(5);
            result.Options.ErrorReporting.PreparedSubmissionLifetime.Should().Be(TimeSpan.FromMinutes(15));
            result.Options.ErrorReporting.MaximumPayloadBytes.Should().Be(8_192);
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_InvalidCacheAndErrorReportingBounds_WHEN_Resolving_THEN_ShouldUseDefaultsAndWarn()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(
            [
                "--workspace-query-cache-sliding-expiration=00:00:00",
                "--plugin-query-cache-sliding-expiration=1.00:00:00.0000001",
                "--error-record-capacity=9",
                "--error-record-lifetime=00:00:00",
                "--error-record-max-bytes=16383",
                "--error-submission-capacity=4",
                "--error-submission-lifetime=04:00:00.0000001",
                "--error-report-max-bytes=8191",
            ]);

            var defaults = new StartupOptions();
            result.Options.WorkspaceQueryCacheSlidingExpiration.Should().Be(defaults.WorkspaceQueryCacheSlidingExpiration);
            result.Options.PluginQueryCacheSlidingExpiration.Should().Be(defaults.PluginQueryCacheSlidingExpiration);
            result.Options.ErrorReporting.Should().BeEquivalentTo(defaults.ErrorReporting);
            result.Warnings.Should().HaveCount(8);
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Theory]
    [InlineData("ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS", "100")]
    [InlineData("ROSLYN_WORKBENCH_MCP_CODE_ACTION_REFERENCE_LIFETIME", "00:05:00")]
    [InlineData("ROSLYN_WORKBENCH_MCP_MAX_TRANSACTION_REVISIONS", "20")]
    [InlineData("ROSLYN_WORKBENCH_MCP_MAX_CONCURRENT_QUERIES", "2")]
    [InlineData("ROSLYN_WORKBENCH_MCP_TOOL_OUTPUT_SCHEMA_MODE", "Omit")]
    public void GIVEN_InvalidTypedEnvironmentValue_WHEN_Resolving_THEN_ShouldUseDefaultAndReportWarning(
        string environmentVariable,
        string defaultValue)
    {
        var previousValues = ClearEnvironment();

        try
        {
            Environment.SetEnvironmentVariable(environmentVariable, "invalid");

            var result = Resolve([]);

            result.Options.Should().BeEquivalentTo(new StartupOptions());
            result.Warnings.Should().ContainSingle().Which.Should().BeEquivalentTo(new WarningInfo
            {
                Code = "StartupConfigurationFallback",
                Message = $"Configuration '{environmentVariable}' is invalid; using default '{defaultValue}'.",
            });
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Theory]
    [InlineData("ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS")]
    [InlineData("ROSLYN_WORKBENCH_MCP_CODE_ACTION_REFERENCE_LIFETIME")]
    [InlineData("ROSLYN_WORKBENCH_MCP_MAX_TRANSACTION_REVISIONS")]
    [InlineData("ROSLYN_WORKBENCH_MCP_MAX_CONCURRENT_QUERIES")]
    public void GIVEN_NonPositiveTypedEnvironmentValue_WHEN_Resolving_THEN_ShouldUseDefaultAndReportWarning(string environmentVariable)
    {
        var previousValues = ClearEnvironment();

        try
        {
            Environment.SetEnvironmentVariable(environmentVariable, "0");

            var result = Resolve([]);

            result.Options.Should().BeEquivalentTo(new StartupOptions());
            result.Warnings.Should().ContainSingle();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_CaseInsensitivePluginRoots_WHEN_Resolving_THEN_ShouldRemoveCaseDuplicateDirectories()
    {
        var previousValues = ClearEnvironment();
        _pathComparison
            .Setup(comparison => comparison.CreateKey(It.IsAny<string>()))
            .Returns((string path) => new FileSystemPathKey(path, isCaseSensitive: false));

        try
        {
            var result = Resolve(
            [
                "--plugin-directory=/plugins/one",
                "--plugin-directory=/plugins/two",
                "--plugin-directory=/PLUGINS/ONE",
            ]);

            result.Options.PluginDirectories.Should().Equal("/plugins/one", "/plugins/two");
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_MaximumCodeActionReferenceLifetime_WHEN_Resolving_THEN_ShouldRetainConfiguredValue()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(["--code-action-reference-lifetime=1.00:00:00"]);

            result.Options.CodeActionReferenceLifetime.Should().Be(TimeSpan.FromDays(1));
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_ExcessiveCodeActionReferenceLifetime_WHEN_Resolving_THEN_ShouldUseDefaultAndReportWarning()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(["--code-action-reference-lifetime=1.00:00:00.0000001"]);

            result.Options.CodeActionReferenceLifetime.Should().Be(TimeSpan.FromMinutes(5));
            result.Warnings.Should().ContainSingle().Which.Should().BeEquivalentTo(new WarningInfo
            {
                Code = "StartupConfigurationFallback",
                Message = "Configuration '--code-action-reference-lifetime' is invalid; using default '00:05:00'.",
            });
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_UndefinedNumericSchemaMode_WHEN_Resolving_THEN_ShouldUseDefaultAndReportWarning()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(["--tool-output-schema-mode=999"]);

            result.Options.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Omit);
            result.Warnings.Should().ContainSingle();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_KnownScalarArgumentWithoutValue_WHEN_Resolving_THEN_ShouldUseDefaultAndReportWarning()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(["--default-max-results"]);

            result.Options.DefaultMaxResults.Should().Be(100);
            result.Warnings.Should().ContainSingle().Which.Message.Should().Contain("--default-max-results");
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_ValidThenBlankScalarArguments_WHEN_Resolving_THEN_ShouldApplyLastValueFallback()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(
            [
                "--default-max-results=25",
                "--default-max-results=   ",
            ]);

            result.Options.DefaultMaxResults.Should().Be(100);
            result.Warnings.Should().ContainSingle();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_BlankPluginDirectorySources_WHEN_Resolving_THEN_ShouldIgnoreThemAndReportWarnings()
    {
        var previousValues = ClearEnvironment();

        try
        {
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY", " ");

            var result = Resolve(
            [
                "--plugin-directory=/plugins/valid",
                "--plugin-directory=   ",
            ]);

            result.Options.PluginDirectories.Should().Equal("/plugins/valid");
            result.Warnings.Should().HaveCount(2);
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_CommandLineAndEnvironmentPluginDirectories_WHEN_Resolving_THEN_ShouldRetainCommandLineFirst()
    {
        var previousValues = ClearEnvironment();

        try
        {
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY", "/plugins/environment");

            var result = Resolve(["--plugin-directory=/plugins/argument"]);

            result.Options.PluginDirectories.Should().Equal("/plugins/argument", "/plugins/environment");
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_InvalidCommandLineValueAndValidEnvironmentValue_WHEN_Resolving_THEN_ShouldUseDefaultForCommandLinePrecedence()
    {
        var previousValues = ClearEnvironment();

        try
        {
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS", "25");

            var result = Resolve(["--default-max-results=invalid"]);

            result.Options.DefaultMaxResults.Should().Be(100);
            result.Warnings.Should().ContainSingle().Which.Message.Should().Contain("--default-max-results");
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_InvalidStateDirectorySyntax_WHEN_Resolving_THEN_ShouldUseDefaultAndReportWarning()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(["--state-directory=\0"]);

            result.Options.StateDirectory.Should().Be(new StartupOptions().StateDirectory);
            result.Warnings.Should().ContainSingle();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    private StartupConfigurationSnapshot Resolve(string[] args)
    {
        return StartupOptionsResolver.Resolve(args, _pathComparison.Object);
    }

    private static Dictionary<string, string?> ClearEnvironment()
    {
        var previousValues = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var environmentVariable in _environmentVariables)
        {
            previousValues[environmentVariable] = Environment.GetEnvironmentVariable(environmentVariable);
            Environment.SetEnvironmentVariable(environmentVariable, null);
        }

        return previousValues;
    }

    private static void RestoreEnvironment(IReadOnlyDictionary<string, string?> previousValues)
    {
        foreach (var pair in previousValues)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }
}
