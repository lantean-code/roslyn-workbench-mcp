using Roslyn.Workbench.Mcp.Workspace.IO;

namespace Roslyn.Workbench.Mcp.Test.Configuration;

[Collection("EnvironmentVariables")]
public sealed class StartupOptionsResolverTests
{
    private static readonly string[] _environmentVariables =
    [
        "ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY",
        "ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS",
        "ROSLYN_WORKBENCH_MCP_CODE_ACTION_TOKEN_LIFETIME",
        "ROSLYN_WORKBENCH_MCP_MAX_TRANSACTION_REVISIONS",
        "ROSLYN_WORKBENCH_MCP_MAX_CONCURRENT_QUERIES",
        "ROSLYN_WORKBENCH_MCP_TOOL_OUTPUT_SCHEMA_MODE",
        "ROSLYN_WORKBENCH_MCP_STATE_DIRECTORY",
    ];

    private readonly Mock<IWorkspacePathComparison> _pathComparison;

    public StartupOptionsResolverTests()
    {
        _pathComparison = new Mock<IWorkspacePathComparison>();
        _pathComparison
            .Setup(comparison => comparison.GetComparer(It.IsAny<string>()))
            .Returns(StringComparer.Ordinal);
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
            result.Options.CodeActionTokenLifetime.Should().Be(TimeSpan.FromMinutes(5));
            result.Options.MaxTransactionRevisions.Should().Be(20);
            result.Options.MaxConcurrentQueries.Should().Be(2);
            result.Options.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Omit);
            result.Options.StateDirectory.Should().Be(Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-state"));
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
                "--code-action-token-lifetime=00:10:00",
                "--max-transaction-revisions",
                "30",
                "--max-concurrent-queries=4",
                "--tool-output-schema-mode=full",
                "--state-directory",
                "/state",
            ]);

            result.Options.PluginDirectories.Should().Equal("/plugins/one", "/plugins/two");
            result.Options.DefaultMaxResults.Should().Be(25);
            result.Options.CodeActionTokenLifetime.Should().Be(TimeSpan.FromMinutes(10));
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
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_CODE_ACTION_TOKEN_LIFETIME", "00:15:00");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_MAX_TRANSACTION_REVISIONS", "40");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_MAX_CONCURRENT_QUERIES", "6");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_TOOL_OUTPUT_SCHEMA_MODE", "Full");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_STATE_DIRECTORY", "/environment-state");

            var result = Resolve([]);

            result.Options.PluginDirectories.Should().Equal("/plugins/one", "/plugins/two");
            result.Options.DefaultMaxResults.Should().Be(50);
            result.Options.CodeActionTokenLifetime.Should().Be(TimeSpan.FromMinutes(15));
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

    [Theory]
    [InlineData("ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS", "100")]
    [InlineData("ROSLYN_WORKBENCH_MCP_CODE_ACTION_TOKEN_LIFETIME", "00:05:00")]
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
    [InlineData("ROSLYN_WORKBENCH_MCP_CODE_ACTION_TOKEN_LIFETIME")]
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
            .Setup(comparison => comparison.GetComparer(It.IsAny<string>()))
            .Returns(StringComparer.OrdinalIgnoreCase);

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
    public void GIVEN_MaximumCodeActionTokenLifetime_WHEN_Resolving_THEN_ShouldRetainConfiguredValue()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(["--code-action-token-lifetime=1.00:00:00"]);

            result.Options.CodeActionTokenLifetime.Should().Be(TimeSpan.FromDays(1));
            result.Warnings.Should().BeEmpty();
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_ExcessiveCodeActionTokenLifetime_WHEN_Resolving_THEN_ShouldUseDefaultAndReportWarning()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = Resolve(["--code-action-token-lifetime=1.00:00:00.0000001"]);

            result.Options.CodeActionTokenLifetime.Should().Be(TimeSpan.FromMinutes(5));
            result.Warnings.Should().ContainSingle().Which.Should().BeEquivalentTo(new WarningInfo
            {
                Code = "StartupConfigurationFallback",
                Message = "Configuration '--code-action-token-lifetime' is invalid; using default '00:05:00'.",
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

            result.Options.StateDirectory.Should().Be(Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-state"));
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
