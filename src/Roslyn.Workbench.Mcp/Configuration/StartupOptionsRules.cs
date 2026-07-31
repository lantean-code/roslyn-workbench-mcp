namespace Roslyn.Workbench.Mcp.Configuration;

internal static class StartupOptionsRules
{
    public static TimeSpan MaximumCodeActionReferenceLifetime { get; } = TimeSpan.FromDays(1);

    public static TimeSpan MaximumQueryCacheSlidingExpiration { get; } = TimeSpan.FromDays(1);

    public const long MinimumWorkspaceQueryCacheSizeLimit = 5_000;

    public const long MaximumWorkspaceQueryCacheSizeLimit = 100_000;

    public const long MinimumPluginQueryCacheEntryLimit = 7_500;

    public const long MaximumPluginQueryCacheEntryLimit = 50_000;

    public const long MinimumCodeActionReferenceCacheSizeLimit = 40_000;

    public const long MaximumCodeActionReferenceCacheSizeLimit = 250_000;

    public static bool IsPositive(int value)
    {
        return value > 0;
    }

    public static bool IsPositive(TimeSpan value)
    {
        return value > TimeSpan.Zero;
    }

    public static bool IsSupportedQueryCacheSlidingExpiration(TimeSpan value)
    {
        return IsPositive(value) && value <= MaximumQueryCacheSlidingExpiration;
    }

    public static bool IsWithinRange(long value, long minimum, long maximum)
    {
        return value >= minimum && value <= maximum;
    }

    public static bool IsSupportedCodeActionReferenceLifetime(TimeSpan value)
    {
        return IsPositive(value) && value <= MaximumCodeActionReferenceLifetime;
    }

    public static bool IsSupported(ToolOutputSchemaMode value)
    {
        return Enum.IsDefined(value);
    }

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

    public static bool AreValidPluginDirectories(IReadOnlyList<string> values)
    {
        return values.All(static value => !string.IsNullOrWhiteSpace(value));
    }
}
