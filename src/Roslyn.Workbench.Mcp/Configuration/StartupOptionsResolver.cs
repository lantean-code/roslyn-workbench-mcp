using System.Globalization;

namespace Roslyn.Workbench.Mcp.Configuration;

internal static class StartupOptionsResolver
{
    private const string _configurationFallbackCode = "StartupConfigurationFallback";

    public static StartupConfigurationSnapshot Resolve(string[] args)
    {
        var optionMap = ParseArguments(args);
        var defaults = new StartupOptions();
        var warnings = new List<WarningInfo>();

        var options = new StartupOptions
        {
            PluginDirectories = ResolvePluginDirectories(optionMap, warnings),
            DefaultMaxResults = ResolvePositiveInt(
                optionMap,
                "default-max-results",
                "ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS",
                defaults.DefaultMaxResults,
                warnings),
            CodeActionTokenLifetime = ResolvePositiveTimeSpan(
                optionMap,
                "code-action-token-lifetime",
                "ROSLYN_WORKBENCH_MCP_CODE_ACTION_TOKEN_LIFETIME",
                defaults.CodeActionTokenLifetime,
                warnings),
            MaxTransactionRevisions = ResolvePositiveInt(
                optionMap,
                "max-transaction-revisions",
                "ROSLYN_WORKBENCH_MCP_MAX_TRANSACTION_REVISIONS",
                defaults.MaxTransactionRevisions,
                warnings),
            MaxConcurrentQueries = ResolvePositiveInt(
                optionMap,
                "max-concurrent-queries",
                "ROSLYN_WORKBENCH_MCP_MAX_CONCURRENT_QUERIES",
                defaults.MaxConcurrentQueries,
                warnings),
            ToolOutputSchemaMode = ResolveToolOutputSchemaMode(
                optionMap,
                "tool-output-schema-mode",
                "ROSLYN_WORKBENCH_MCP_TOOL_OUTPUT_SCHEMA_MODE",
                defaults.ToolOutputSchemaMode,
                warnings),
            StateDirectory = ResolveStateDirectory(
                optionMap,
                "state-directory",
                "ROSLYN_WORKBENCH_MCP_STATE_DIRECTORY",
                defaults.StateDirectory,
                warnings),
        };

        return new StartupConfigurationSnapshot
        {
            Options = options,
            Warnings = warnings,
        };
    }

    private static Dictionary<string, List<string?>> ParseArguments(string[] args)
    {
        var map = new Dictionary<string, List<string?>>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var content = argument[2..];
            var separatorIndex = content.IndexOf('=', StringComparison.Ordinal);
            string key;
            string? value;

            if (separatorIndex >= 0)
            {
                key = content[..separatorIndex];
                value = content[(separatorIndex + 1)..];
            }
            else
            {
                key = content;
                value = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal)
                    ? args[++index]
                    : null;
            }

            if (!map.TryGetValue(key, out var values))
            {
                values = [];
                map[key] = values;
            }

            values.Add(value);
        }

        return map;
    }

    private static string[] ResolvePluginDirectories(
        Dictionary<string, List<string?>> optionMap,
        List<WarningInfo> warnings)
    {
        var pluginDirectories = new List<string>();
        if (optionMap.TryGetValue("plugin-directory", out var configuredDirectories))
        {
            foreach (var configuredDirectory in configuredDirectories)
            {
                if (string.IsNullOrWhiteSpace(configuredDirectory))
                {
                    AddFallbackWarning(warnings, "--plugin-directory", "no plugin directory");
                }
                else
                {
                    pluginDirectories.Add(configuredDirectory);
                }
            }
        }

        pluginDirectories.AddRange(ReadPluginDirectoriesFromEnvironment(
            "ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY",
            warnings));

        return pluginDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string[] ReadPluginDirectoriesFromEnvironment(
        string environmentVariable,
        List<WarningInfo> warnings)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (value is null)
        {
            return [];
        }

        var directories = value.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (directories.Length == 0)
        {
            AddFallbackWarning(warnings, environmentVariable, "no plugin directory");
        }

        return directories;
    }

    private static int ResolvePositiveInt(
        Dictionary<string, List<string?>> optionMap,
        string key,
        string environmentVariable,
        int defaultValue,
        List<WarningInfo> warnings)
    {
        var value = ReadScalarValue(optionMap, key, environmentVariable, out var source);
        if (value is null)
        {
            return defaultValue;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)
            && StartupOptionsRules.IsPositive(parsedValue))
        {
            return parsedValue;
        }

        AddFallbackWarning(warnings, source, $"default '{defaultValue.ToString(CultureInfo.InvariantCulture)}'");
        return defaultValue;
    }

    private static TimeSpan ResolvePositiveTimeSpan(
        Dictionary<string, List<string?>> optionMap,
        string key,
        string environmentVariable,
        TimeSpan defaultValue,
        List<WarningInfo> warnings)
    {
        var value = ReadScalarValue(optionMap, key, environmentVariable, out var source);
        if (value is null)
        {
            return defaultValue;
        }

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsedValue)
            && StartupOptionsRules.IsPositive(parsedValue))
        {
            return parsedValue;
        }

        AddFallbackWarning(warnings, source, $"default '{defaultValue.ToString("c", CultureInfo.InvariantCulture)}'");
        return defaultValue;
    }

    private static ToolOutputSchemaMode ResolveToolOutputSchemaMode(
        Dictionary<string, List<string?>> optionMap,
        string key,
        string environmentVariable,
        ToolOutputSchemaMode defaultValue,
        List<WarningInfo> warnings)
    {
        var value = ReadScalarValue(optionMap, key, environmentVariable, out var source);
        if (value is null)
        {
            return defaultValue;
        }

        if (Enum.TryParse<ToolOutputSchemaMode>(value, ignoreCase: true, out var parsedValue)
            && StartupOptionsRules.IsSupported(parsedValue))
        {
            return parsedValue;
        }

        AddFallbackWarning(warnings, source, $"default '{defaultValue}'");
        return defaultValue;
    }

    private static string ResolveStateDirectory(
        Dictionary<string, List<string?>> optionMap,
        string key,
        string environmentVariable,
        string defaultValue,
        List<WarningInfo> warnings)
    {
        var value = ReadScalarValue(optionMap, key, environmentVariable, out var source);
        if (value is null)
        {
            return defaultValue;
        }

        if (StartupOptionsRules.IsValidStateDirectory(value))
        {
            return value;
        }

        AddFallbackWarning(warnings, source, "the default state directory");
        return defaultValue;
    }

    private static string? ReadScalarValue(
        Dictionary<string, List<string?>> optionMap,
        string key,
        string environmentVariable,
        out string source)
    {
        if (optionMap.TryGetValue(key, out var values))
        {
            source = $"--{key}";
            return values[^1] ?? string.Empty;
        }

        source = environmentVariable;
        return Environment.GetEnvironmentVariable(environmentVariable);
    }

    private static void AddFallbackWarning(
        List<WarningInfo> warnings,
        string source,
        string fallbackDescription)
    {
        warnings.Add(new WarningInfo
        {
            Code = _configurationFallbackCode,
            Message = $"Configuration '{source}' is invalid; using {fallbackDescription}.",
        });
    }
}
