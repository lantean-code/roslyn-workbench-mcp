using System.Globalization;

namespace Roslyn.Workbench.Mcp.Configuration;

/// <summary>
/// Resolves command-line and environment configuration into validated startup values and fallback warnings.
/// </summary>
internal static class StartupOptionsResolver
{
    private const string _configurationFallbackCode = "StartupConfigurationFallback";

    /// <summary>
    /// Resolves the effective startup options, preferring command-line values over environment values and defaults.
    /// </summary>
    /// <param name="args">The command-line arguments used to resolve host configuration.</param>
    /// <param name="pathComparison">The comparison rules to apply to workspace paths.</param>
    /// <returns>The resolved options together with warnings for invalid values that used a safe fallback.</returns>
    public static StartupConfigurationSnapshot Resolve(string[] args, IWorkspacePathComparison pathComparison)
    {
        var optionMap = ParseArguments(args);
        var defaults = new StartupOptions();
        var warnings = new List<WarningInfo>();

        var options = new StartupOptions
        {
            PluginDirectories = ResolvePluginDirectories(optionMap, pathComparison, warnings),
            DefaultMaxResults = ResolvePositiveInt(
                optionMap,
                "default-max-results",
                "ROSLYN_WORKBENCH_MCP_DEFAULT_MAX_RESULTS",
                defaults.DefaultMaxResults,
                warnings),
            CodeActionReferenceLifetime = ResolveCodeActionReferenceLifetime(
                optionMap,
                "code-action-reference-lifetime",
                "ROSLYN_WORKBENCH_MCP_CODE_ACTION_REFERENCE_LIFETIME",
                defaults.CodeActionReferenceLifetime,
                warnings),
            WorkspaceQueryCacheSizeLimit = ResolveBoundedLong(
                optionMap,
                "workspace-query-cache-size-limit",
                "ROSLYN_WORKBENCH_MCP_WORKSPACE_QUERY_CACHE_SIZE_LIMIT",
                defaults.WorkspaceQueryCacheSizeLimit,
                StartupOptionsRules.MinimumWorkspaceQueryCacheSizeLimit,
                StartupOptionsRules.MaximumWorkspaceQueryCacheSizeLimit,
                warnings),
            PluginQueryCacheEntryLimit = ResolveBoundedLong(
                optionMap,
                "plugin-query-cache-entry-limit",
                "ROSLYN_WORKBENCH_MCP_PLUGIN_QUERY_CACHE_ENTRY_LIMIT",
                defaults.PluginQueryCacheEntryLimit,
                StartupOptionsRules.MinimumPluginQueryCacheEntryLimit,
                StartupOptionsRules.MaximumPluginQueryCacheEntryLimit,
                warnings),
            CodeActionReferenceCacheSizeLimit = ResolveBoundedLong(
                optionMap,
                "code-action-reference-cache-size-limit",
                "ROSLYN_WORKBENCH_MCP_CODE_ACTION_REFERENCE_CACHE_SIZE_LIMIT",
                defaults.CodeActionReferenceCacheSizeLimit,
                StartupOptionsRules.MinimumCodeActionReferenceCacheSizeLimit,
                StartupOptionsRules.MaximumCodeActionReferenceCacheSizeLimit,
                warnings),
            WorkspaceQueryCacheSlidingExpiration = ResolveQueryCacheSlidingExpiration(
                optionMap,
                "workspace-query-cache-sliding-expiration",
                "ROSLYN_WORKBENCH_MCP_WORKSPACE_QUERY_CACHE_SLIDING_EXPIRATION",
                defaults.WorkspaceQueryCacheSlidingExpiration,
                warnings),
            PluginQueryCacheSlidingExpiration = ResolveQueryCacheSlidingExpiration(
                optionMap,
                "plugin-query-cache-sliding-expiration",
                "ROSLYN_WORKBENCH_MCP_PLUGIN_QUERY_CACHE_SLIDING_EXPIRATION",
                defaults.PluginQueryCacheSlidingExpiration,
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
            ErrorReporting = ResolveErrorReportingOptions(optionMap, warnings),
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
        IWorkspacePathComparison pathComparison,
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

        var distinctDirectoryKeys = new HashSet<FileSystemPathKey>();
        var distinctDirectories = new List<string>(pluginDirectories.Count);
        foreach (var pluginDirectory in pluginDirectories)
        {
            var pluginDirectoryKey = pathComparison.CreateKey(pluginDirectory);
            if (distinctDirectoryKeys.Add(pluginDirectoryKey))
            {
                distinctDirectories.Add(pluginDirectory);
            }
        }

        return distinctDirectories.ToArray();
    }

    private static ErrorReportingOptions ResolveErrorReportingOptions(
        Dictionary<string, List<string?>> optionMap,
        List<WarningInfo> warnings)
    {
        var defaults = new ErrorReportingOptions();
        var consentMode = ResolveErrorReportingConsentMode(
            optionMap,
            defaults.ConsentMode,
            warnings);

        return new ErrorReportingOptions
        {
            ConsentMode = consentMode,
            CapturedErrorCapacity = ResolveBoundedInt(
                optionMap,
                "error-record-capacity",
                "ROSLYN_WORKBENCH_MCP_ERROR_RECORD_CAPACITY",
                defaults.CapturedErrorCapacity,
                ErrorReportingOptionsRules.MinimumCapturedErrorCapacity,
                ErrorReportingOptionsRules.MaximumCapturedErrorCapacity,
                warnings),
            CapturedErrorLifetime = ResolveBoundedTimeSpan(
                optionMap,
                "error-record-lifetime",
                "ROSLYN_WORKBENCH_MCP_ERROR_RECORD_LIFETIME",
                defaults.CapturedErrorLifetime,
                ErrorReportingOptionsRules.MaximumCapturedErrorLifetime,
                warnings),
            MaximumCapturedErrorBytes = ResolveBoundedInt(
                optionMap,
                "error-record-max-bytes",
                "ROSLYN_WORKBENCH_MCP_ERROR_RECORD_MAX_BYTES",
                defaults.MaximumCapturedErrorBytes,
                ErrorReportingOptionsRules.MinimumCapturedErrorBytes,
                ErrorReportingOptionsRules.MaximumCapturedErrorBytes,
                warnings),
            PreparedSubmissionCapacity = ResolveBoundedInt(
                optionMap,
                "error-submission-capacity",
                "ROSLYN_WORKBENCH_MCP_ERROR_SUBMISSION_CAPACITY",
                defaults.PreparedSubmissionCapacity,
                ErrorReportingOptionsRules.MinimumPreparedSubmissionCapacity,
                ErrorReportingOptionsRules.MaximumPreparedSubmissionCapacity,
                warnings),
            PreparedSubmissionLifetime = ResolveBoundedTimeSpan(
                optionMap,
                "error-submission-lifetime",
                "ROSLYN_WORKBENCH_MCP_ERROR_SUBMISSION_LIFETIME",
                defaults.PreparedSubmissionLifetime,
                ErrorReportingOptionsRules.MaximumPreparedSubmissionLifetime,
                warnings),
            MaximumPayloadBytes = ResolveBoundedInt(
                optionMap,
                "error-report-max-bytes",
                "ROSLYN_WORKBENCH_MCP_ERROR_REPORT_MAX_BYTES",
                defaults.MaximumPayloadBytes,
                ErrorReportingOptionsRules.MinimumPayloadBytes,
                ErrorReportingOptionsRules.MaximumPayloadBytes,
                warnings),
        };
    }

    private static ErrorReportingConsentMode ResolveErrorReportingConsentMode(
        Dictionary<string, List<string?>> optionMap,
        ErrorReportingConsentMode defaultValue,
        List<WarningInfo> warnings)
    {
        const string key = "error-reporting-consent";
        const string environmentVariable = "ROSLYN_WORKBENCH_MCP_ERROR_REPORTING_CONSENT";

        if (Environment.GetEnvironmentVariable(environmentVariable) is not null)
        {
            var defaultConsent = defaultValue switch
            {
                ErrorReportingConsentMode.Never => "never",
                ErrorReportingConsentMode.Prompt => "prompt",
                ErrorReportingConsentMode.Always => "always",
                _ => throw new ArgumentOutOfRangeException(nameof(defaultValue)),
            };
            AddFallbackWarning(
                warnings,
                environmentVariable,
                $"command-line/default consent '{defaultConsent}'");
        }

        if (!optionMap.TryGetValue(key, out var values))
        {
            return defaultValue;
        }

        var value = values[^1];
        return value switch
        {
            "never" => ErrorReportingConsentMode.Never,
            "prompt" => ErrorReportingConsentMode.Prompt,
            "always" => ErrorReportingConsentMode.Always,
            _ => AddInvalidConsentWarning(warnings),
        };
    }

    private static ErrorReportingConsentMode AddInvalidConsentWarning(List<WarningInfo> warnings)
    {
        AddFallbackWarning(
            warnings,
            "--error-reporting-consent",
            "fail-closed consent 'never'");
        return ErrorReportingConsentMode.Never;
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

    private static long ResolveBoundedLong(
        Dictionary<string, List<string?>> optionMap,
        string key,
        string environmentVariable,
        long defaultValue,
        long minimum,
        long maximum,
        List<WarningInfo> warnings)
    {
        var value = ReadScalarValue(optionMap, key, environmentVariable, out var source);
        if (value is null)
        {
            return defaultValue;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)
            && StartupOptionsRules.IsWithinRange(parsedValue, minimum, maximum))
        {
            return parsedValue;
        }

        AddFallbackWarning(warnings, source, $"default '{defaultValue.ToString(CultureInfo.InvariantCulture)}'");
        return defaultValue;
    }

    private static int ResolveBoundedInt(
        Dictionary<string, List<string?>> optionMap,
        string key,
        string environmentVariable,
        int defaultValue,
        int minimum,
        int maximum,
        List<WarningInfo> warnings)
    {
        var value = ReadScalarValue(optionMap, key, environmentVariable, out var source);
        if (value is null)
        {
            return defaultValue;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)
            && ErrorReportingOptionsRules.IsWithinRange(parsedValue, minimum, maximum))
        {
            return parsedValue;
        }

        AddFallbackWarning(warnings, source, $"default '{defaultValue.ToString(CultureInfo.InvariantCulture)}'");
        return defaultValue;
    }

    private static TimeSpan ResolveBoundedTimeSpan(
        Dictionary<string, List<string?>> optionMap,
        string key,
        string environmentVariable,
        TimeSpan defaultValue,
        TimeSpan maximum,
        List<WarningInfo> warnings)
    {
        var value = ReadScalarValue(optionMap, key, environmentVariable, out var source);
        if (value is null)
        {
            return defaultValue;
        }

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsedValue)
            && ErrorReportingOptionsRules.IsWithinLifetime(parsedValue, maximum))
        {
            return parsedValue;
        }

        AddFallbackWarning(warnings, source, $"default '{defaultValue.ToString("c", CultureInfo.InvariantCulture)}'");
        return defaultValue;
    }

    private static TimeSpan ResolveQueryCacheSlidingExpiration(
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
            && StartupOptionsRules.IsSupportedQueryCacheSlidingExpiration(parsedValue))
        {
            return parsedValue;
        }

        AddFallbackWarning(warnings, source, $"default '{defaultValue.ToString("c", CultureInfo.InvariantCulture)}'");
        return defaultValue;
    }

    private static TimeSpan ResolveCodeActionReferenceLifetime(
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
            && StartupOptionsRules.IsSupportedCodeActionReferenceLifetime(parsedValue))
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
