namespace Roslyn.Workbench.Mcp.Test;

[Collection("EnvironmentVariables")]
public sealed class StartupOptionsParserTests
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

    [Fact]
    public void GIVEN_NoArgumentsOrEnvironment_WHEN_Parsing_THEN_ShouldReturnDefaults()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = StartupOptionsParser.Parse([]);

            result.PluginDirectories.Should().BeEmpty();
            result.DefaultMaxResults.Should().Be(100);
            result.CodeActionTokenLifetime.Should().Be(TimeSpan.FromMinutes(5));
            result.MaxTransactionRevisions.Should().Be(20);
            result.MaxConcurrentQueries.Should().Be(2);
            result.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Omit);
            result.StateDirectory.Should().Be(Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-state"));
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_CommandLineValuesInSupportedForms_WHEN_Parsing_THEN_ShouldProjectTypedOptions()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = StartupOptionsParser.Parse(
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

            result.PluginDirectories.Should().Equal("/plugins/one", "/plugins/two");
            result.DefaultMaxResults.Should().Be(25);
            result.CodeActionTokenLifetime.Should().Be(TimeSpan.FromMinutes(10));
            result.MaxTransactionRevisions.Should().Be(30);
            result.MaxConcurrentQueries.Should().Be(4);
            result.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Full);
            result.StateDirectory.Should().Be("/state");
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_DuplicateCommandLineValues_WHEN_Parsing_THEN_ShouldUseLastScalarAndDistinctDirectoryValues()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = StartupOptionsParser.Parse(
            [
                "--plugin-directory=/plugins/one",
                "--plugin-directory=/plugins/two",
                "--plugin-directory=/PLUGINS/ONE",
                "--default-max-results=10",
                "--default-max-results=25",
            ]);

            result.PluginDirectories.Should().Equal("/plugins/one", "/plugins/two");
            result.DefaultMaxResults.Should().Be(25);
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_UnrelatedBlankAndMissingArguments_WHEN_Parsing_THEN_ShouldIgnoreThem()
    {
        var previousValues = ClearEnvironment();

        try
        {
            var result = StartupOptionsParser.Parse(
            [
                "ignored",
                "--missing-value",
                "--blank=   ",
                "--last-missing",
            ]);

            result.PluginDirectories.Should().BeEmpty();
            result.DefaultMaxResults.Should().Be(100);
            result.StateDirectory.Should().Be(Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-state"));
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_EnvironmentValues_WHEN_Parsing_THEN_ShouldUseTypedEnvironmentConfiguration()
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

            var result = StartupOptionsParser.Parse([]);

            result.PluginDirectories.Should().Equal("/plugins/one", "/plugins/two");
            result.DefaultMaxResults.Should().Be(50);
            result.CodeActionTokenLifetime.Should().Be(TimeSpan.FromMinutes(15));
            result.MaxTransactionRevisions.Should().Be(40);
            result.MaxConcurrentQueries.Should().Be(6);
            result.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Full);
            result.StateDirectory.Should().Be("/environment-state");
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_InvalidTypedValues_WHEN_Parsing_THEN_ShouldUseDefaults()
    {
        var previousValues = ClearEnvironment();

        try
        {
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS", "invalid");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_CODE_ACTION_TOKEN_LIFETIME", "invalid");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_MAX_TRANSACTION_REVISIONS", "invalid");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_MAX_CONCURRENT_QUERIES", "invalid");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_TOOL_OUTPUT_SCHEMA_MODE", "invalid");

            var result = StartupOptionsParser.Parse([]);

            result.DefaultMaxResults.Should().Be(100);
            result.CodeActionTokenLifetime.Should().Be(TimeSpan.FromMinutes(5));
            result.MaxTransactionRevisions.Should().Be(20);
            result.MaxConcurrentQueries.Should().Be(2);
            result.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Omit);
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
    }

    [Fact]
    public void GIVEN_CommandLineAndEnvironmentValues_WHEN_Parsing_THEN_ShouldPreferCommandLine()
    {
        var previousValues = ClearEnvironment();

        try
        {
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS", "10");
            Environment.SetEnvironmentVariable("ROSLYN_WORKBENCH_MCP_STATE_DIRECTORY", "/environment-state");

            var result = StartupOptionsParser.Parse(
            [
                "--default-max-results=20",
                "--state-directory=/argument-state",
            ]);

            result.DefaultMaxResults.Should().Be(20);
            result.StateDirectory.Should().Be("/argument-state");
        }
        finally
        {
            RestoreEnvironment(previousValues);
        }
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
