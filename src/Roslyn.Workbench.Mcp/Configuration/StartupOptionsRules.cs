namespace Roslyn.Workbench.Mcp.Configuration;

/// <summary>
/// Defines the supported ranges and validation rules for startup configuration.
/// </summary>
internal static class StartupOptionsRules
{
    /// <summary>
    /// Gets the maximum Code Action reference lifetime.
    /// </summary>
    public static TimeSpan MaximumCodeActionReferenceLifetime { get; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Gets the maximum query cache sliding expiration.
    /// </summary>
    public static TimeSpan MaximumQueryCacheSlidingExpiration { get; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Defines the smallest supported workspace query-cache charge limit.
    /// </summary>
    public const long MinimumWorkspaceQueryCacheSizeLimit = 5_000;

    /// <summary>
    /// Defines the largest supported workspace query-cache charge limit.
    /// </summary>
    public const long MaximumWorkspaceQueryCacheSizeLimit = 100_000;

    /// <summary>
    /// Defines the smallest supported plugin query-cache entry limit.
    /// </summary>
    public const long MinimumPluginQueryCacheEntryLimit = 7_500;

    /// <summary>
    /// Defines the largest supported plugin query-cache entry limit.
    /// </summary>
    public const long MaximumPluginQueryCacheEntryLimit = 50_000;

    /// <summary>
    /// Defines the smallest supported Code Action reference-cache charge limit.
    /// </summary>
    public const long MinimumCodeActionReferenceCacheSizeLimit = 40_000;

    /// <summary>
    /// Defines the largest supported Code Action reference-cache charge limit.
    /// </summary>
    public const long MaximumCodeActionReferenceCacheSizeLimit = 250_000;

    /// <summary>
    /// Determines whether an integer is positive.
    /// </summary>
    /// <param name="value">The integer to validate.</param>
    /// <returns><see langword="true"/> when the value is positive; otherwise, <see langword="false"/>.</returns>
    public static bool IsPositive(int value)
    {
        return value > 0;
    }

    /// <summary>
    /// Determines whether a duration is positive.
    /// </summary>
    /// <param name="value">The duration to validate.</param>
    /// <returns><see langword="true"/> when the value is positive; otherwise, <see langword="false"/>.</returns>
    public static bool IsPositive(TimeSpan value)
    {
        return value > TimeSpan.Zero;
    }

    /// <summary>
    /// Determines whether a query-cache sliding expiration is within the supported range.
    /// </summary>
    /// <param name="value">The sliding expiration to validate.</param>
    /// <returns><see langword="true"/> when the expiration is positive and no longer than the supported maximum; otherwise, <see langword="false"/>.</returns>
    public static bool IsSupportedQueryCacheSlidingExpiration(TimeSpan value)
    {
        return IsPositive(value) && value <= MaximumQueryCacheSlidingExpiration;
    }

    /// <summary>
    /// Determines whether a value lies within an inclusive range.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="minimum">The inclusive lower bound for the value.</param>
    /// <param name="maximum">The inclusive upper bound for the value.</param>
    /// <returns><see langword="true"/> when the value is within range; otherwise, <see langword="false"/>.</returns>
    public static bool IsWithinRange(long value, long minimum, long maximum)
    {
        return value >= minimum && value <= maximum;
    }

    /// <summary>
    /// Determines whether a Code Action reference lifetime is within the supported range.
    /// </summary>
    /// <param name="value">The reference lifetime to validate.</param>
    /// <returns><see langword="true"/> when the lifetime is positive and no longer than the supported maximum; otherwise, <see langword="false"/>.</returns>
    public static bool IsSupportedCodeActionReferenceLifetime(TimeSpan value)
    {
        return IsPositive(value) && value <= MaximumCodeActionReferenceLifetime;
    }

    /// <summary>
    /// Determines whether a tool-output schema mode is supported.
    /// </summary>
    /// <param name="value">The schema mode to validate.</param>
    /// <returns><see langword="true"/> when the value is supported; otherwise, <see langword="false"/>.</returns>
    public static bool IsSupported(ToolOutputSchemaMode value)
    {
        return Enum.IsDefined(value);
    }

    /// <summary>
    /// Determines whether a state-directory value can be resolved to a full path.
    /// </summary>
    /// <param name="value">The state-directory path to validate.</param>
    /// <returns><see langword="true"/> when the value is non-blank and can be resolved to a full path; otherwise, <see langword="false"/>.</returns>
    public static bool IsValidStateDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            Path.GetFullPath(value);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether every configured plugin directory is non-blank.
    /// </summary>
    /// <param name="values">The configured plugin directory values to validate.</param>
    /// <returns><see langword="true"/> when every configured directory is non-blank; otherwise, <see langword="false"/>.</returns>
    public static bool AreValidPluginDirectories(IReadOnlyList<string> values)
    {
        return values.All(static value => !string.IsNullOrWhiteSpace(value));
    }
}
