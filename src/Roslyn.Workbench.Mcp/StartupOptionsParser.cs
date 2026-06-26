namespace Roslyn.Workbench.Mcp;

internal static class StartupOptionsParser
{
    public static StartupOptions Parse(string[] args)
    {
        var optionMap = ParseArguments(args);
        var pluginDirectories = new List<string>();

        pluginDirectories.AddRange(ReadList(optionMap, "plugin-directory"));
        pluginDirectories.AddRange(ReadListFromEnvironment("ROSLYN_WORKBENCH_MCP_PLUGIN_DIRECTORY"));

        return new StartupOptions
        {
            PluginDirectories = pluginDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            DefaultMaxResults = ReadInt(optionMap, "default-max-results", "ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS", 100),
            MaxResponseBytes = ReadInt(optionMap, "max-response-bytes", "ROSLYN_WORKBENCH_MCP_MAX_RESPONSE_BYTES", 4 * 1024 * 1024),
            CodeActionTokenLifetime = ReadTimeSpan(optionMap, "code-action-token-lifetime", "ROSLYN_WORKBENCH_MCP_CODE_ACTION_TOKEN_LIFETIME", TimeSpan.FromMinutes(5)),
            MaxTransactionRevisions = ReadInt(optionMap, "max-transaction-revisions", "ROSLYN_WORKBENCH_MCP_MAX_TRANSACTION_REVISIONS", 20),
            MaxConcurrentQueries = ReadInt(optionMap, "max-concurrent-queries", "ROSLYN_WORKBENCH_MCP_MAX_CONCURRENT_QUERIES", 2),
            StateDirectory = ReadString(optionMap, "state-directory", "ROSLYN_WORKBENCH_MCP_STATE_DIRECTORY")
                ?? Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-state"),
        };
    }

    private static Dictionary<string, List<string>> ParseArguments(string[] args)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var content = argument[2..];
            var separatorIndex = content.IndexOf('=');
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

            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return map;
    }

    private static IReadOnlyList<string> ReadList(Dictionary<string, List<string>> optionMap, string key)
    {
        return optionMap.TryGetValue(key, out var values) ? values : [];
    }

    private static IReadOnlyList<string> ReadListFromEnvironment(string environmentVariable)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);

        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int ReadInt(Dictionary<string, List<string>> optionMap, string key, string environmentVariable, int defaultValue)
    {
        var value = ReadString(optionMap, key, environmentVariable);
        return int.TryParse(value, out var parsedValue) ? parsedValue : defaultValue;
    }

    private static TimeSpan ReadTimeSpan(Dictionary<string, List<string>> optionMap, string key, string environmentVariable, TimeSpan defaultValue)
    {
        var value = ReadString(optionMap, key, environmentVariable);
        return TimeSpan.TryParse(value, out var parsedValue) ? parsedValue : defaultValue;
    }

    private static string? ReadString(Dictionary<string, List<string>> optionMap, string key, string environmentVariable)
    {
        if (optionMap.TryGetValue(key, out var values) && values.Count > 0)
        {
            return values[^1];
        }

        return Environment.GetEnvironmentVariable(environmentVariable);
    }
}
