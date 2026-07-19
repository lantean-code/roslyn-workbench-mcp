using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Configuration;

internal sealed class StartupOptionsValidator : IValidateOptions<StartupOptions>
{
    public ValidateOptionsResult Validate(string? name, StartupOptions options)
    {
        var failures = GetFailures(options);
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    public void EnsureValid(StartupOptions options)
    {
        var failures = GetFailures(options);
        if (failures.Count == 0)
        {
            return;
        }

        throw new OptionsValidationException(nameof(StartupOptions), typeof(StartupOptions), failures);
    }

    private static List<string> GetFailures(StartupOptions options)
    {
        var failures = new List<string>();

        if (!StartupOptionsRules.IsPositive(options.DefaultMaxResults))
        {
            failures.Add($"{nameof(StartupOptions.DefaultMaxResults)} must be greater than zero.");
        }

        if (!StartupOptionsRules.IsPositive(options.CodeActionTokenLifetime))
        {
            failures.Add($"{nameof(StartupOptions.CodeActionTokenLifetime)} must be greater than zero.");
        }

        if (!StartupOptionsRules.IsPositive(options.MaxTransactionRevisions))
        {
            failures.Add($"{nameof(StartupOptions.MaxTransactionRevisions)} must be greater than zero.");
        }

        if (!StartupOptionsRules.IsPositive(options.MaxConcurrentQueries))
        {
            failures.Add($"{nameof(StartupOptions.MaxConcurrentQueries)} must be greater than zero.");
        }

        if (!StartupOptionsRules.IsSupported(options.ToolOutputSchemaMode))
        {
            failures.Add($"{nameof(StartupOptions.ToolOutputSchemaMode)} must be a supported value.");
        }

        if (!StartupOptionsRules.IsValidStateDirectory(options.StateDirectory))
        {
            failures.Add($"{nameof(StartupOptions.StateDirectory)} must be a valid non-blank path.");
        }

        if (!StartupOptionsRules.AreValidPluginDirectories(options.PluginDirectories))
        {
            failures.Add($"{nameof(StartupOptions.PluginDirectories)} must not contain blank paths.");
        }

        return failures;
    }
}
