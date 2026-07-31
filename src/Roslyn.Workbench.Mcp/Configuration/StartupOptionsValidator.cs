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

        if (!StartupOptionsRules.IsSupportedCodeActionReferenceLifetime(options.CodeActionReferenceLifetime))
        {
            failures.Add(
                $"{nameof(StartupOptions.CodeActionReferenceLifetime)} must be greater than zero and no greater than {StartupOptionsRules.MaximumCodeActionReferenceLifetime:c}.");
        }

        AddRangeFailure(
            failures,
            nameof(StartupOptions.WorkspaceQueryCacheSizeLimit),
            options.WorkspaceQueryCacheSizeLimit,
            StartupOptionsRules.MinimumWorkspaceQueryCacheSizeLimit,
            StartupOptionsRules.MaximumWorkspaceQueryCacheSizeLimit);

        AddRangeFailure(
            failures,
            nameof(StartupOptions.PluginQueryCacheEntryLimit),
            options.PluginQueryCacheEntryLimit,
            StartupOptionsRules.MinimumPluginQueryCacheEntryLimit,
            StartupOptionsRules.MaximumPluginQueryCacheEntryLimit);

        AddRangeFailure(
            failures,
            nameof(StartupOptions.CodeActionReferenceCacheSizeLimit),
            options.CodeActionReferenceCacheSizeLimit,
            StartupOptionsRules.MinimumCodeActionReferenceCacheSizeLimit,
            StartupOptionsRules.MaximumCodeActionReferenceCacheSizeLimit);

        if (!StartupOptionsRules.IsSupportedQueryCacheSlidingExpiration(options.WorkspaceQueryCacheSlidingExpiration))
        {
            failures.Add($"{nameof(StartupOptions.WorkspaceQueryCacheSlidingExpiration)} must be greater than zero and no greater than {StartupOptionsRules.MaximumQueryCacheSlidingExpiration:c}.");
        }

        if (!StartupOptionsRules.IsSupportedQueryCacheSlidingExpiration(options.PluginQueryCacheSlidingExpiration))
        {
            failures.Add($"{nameof(StartupOptions.PluginQueryCacheSlidingExpiration)} must be greater than zero and no greater than {StartupOptionsRules.MaximumQueryCacheSlidingExpiration:c}.");
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

        AddErrorReportingFailures(failures, options.ErrorReporting);

        return failures;
    }

    private static void AddErrorReportingFailures(
        List<string> failures,
        ErrorReportingOptions options)
    {
        if (!Enum.IsDefined(options.ConsentMode))
        {
            failures.Add($"{nameof(ErrorReportingOptions.ConsentMode)} must be a supported value.");
        }

        AddErrorReportingRangeFailure(
            failures,
            nameof(ErrorReportingOptions.CapturedErrorCapacity),
            options.CapturedErrorCapacity,
            ErrorReportingOptionsRules.MinimumCapturedErrorCapacity,
            ErrorReportingOptionsRules.MaximumCapturedErrorCapacity);
        AddErrorReportingRangeFailure(
            failures,
            nameof(ErrorReportingOptions.MaximumCapturedErrorBytes),
            options.MaximumCapturedErrorBytes,
            ErrorReportingOptionsRules.MinimumCapturedErrorBytes,
            ErrorReportingOptionsRules.MaximumCapturedErrorBytes);
        AddErrorReportingRangeFailure(
            failures,
            nameof(ErrorReportingOptions.PreparedSubmissionCapacity),
            options.PreparedSubmissionCapacity,
            ErrorReportingOptionsRules.MinimumPreparedSubmissionCapacity,
            ErrorReportingOptionsRules.MaximumPreparedSubmissionCapacity);
        AddErrorReportingRangeFailure(
            failures,
            nameof(ErrorReportingOptions.MaximumPayloadBytes),
            options.MaximumPayloadBytes,
            ErrorReportingOptionsRules.MinimumPayloadBytes,
            ErrorReportingOptionsRules.MaximumPayloadBytes);
        AddErrorReportingLifetimeFailure(
            failures,
            nameof(ErrorReportingOptions.CapturedErrorLifetime),
            options.CapturedErrorLifetime,
            ErrorReportingOptionsRules.MaximumCapturedErrorLifetime);
        AddErrorReportingLifetimeFailure(
            failures,
            nameof(ErrorReportingOptions.PreparedSubmissionLifetime),
            options.PreparedSubmissionLifetime,
            ErrorReportingOptionsRules.MaximumPreparedSubmissionLifetime);
    }

    private static void AddErrorReportingRangeFailure(
        List<string> failures,
        string name,
        int value,
        int minimum,
        int maximum)
    {
        if (!ErrorReportingOptionsRules.IsWithinRange(value, minimum, maximum))
        {
            failures.Add($"{name} must be between {minimum} and {maximum}, inclusive.");
        }
    }

    private static void AddErrorReportingLifetimeFailure(
        List<string> failures,
        string name,
        TimeSpan value,
        TimeSpan maximum)
    {
        if (!ErrorReportingOptionsRules.IsWithinLifetime(value, maximum))
        {
            failures.Add($"{name} must be greater than zero and no greater than {maximum:c}.");
        }
    }

    private static void AddRangeFailure(
        List<string> failures,
        string name,
        long value,
        long minimum,
        long maximum)
    {
        if (!StartupOptionsRules.IsWithinRange(value, minimum, maximum))
        {
            failures.Add($"{name} must be between {minimum} and {maximum}, inclusive.");
        }
    }
}
