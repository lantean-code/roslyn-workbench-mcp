namespace Roslyn.Workbench.Mcp.ErrorReporting.Configuration;

internal static class ErrorReportingOptionsRules
{
    public const int MinimumCapturedErrorCapacity = 10;
    public const int MaximumCapturedErrorCapacity = 1_000;
    public const int MinimumCapturedErrorBytes = 16 * 1024;
    public const int MaximumCapturedErrorBytes = 256 * 1024;
    public const int MinimumPreparedSubmissionCapacity = 5;
    public const int MaximumPreparedSubmissionCapacity = 500;
    public const int MinimumPayloadBytes = 8 * 1024;
    public const int MaximumPayloadBytes = 256 * 1024;
    public static TimeSpan MaximumCapturedErrorLifetime { get; } = TimeSpan.FromDays(1);

    public static TimeSpan MaximumPreparedSubmissionLifetime { get; } = TimeSpan.FromHours(4);

    public static bool IsWithinRange(int value, int minimum, int maximum)
    {
        return value >= minimum && value <= maximum;
    }

    public static bool IsWithinLifetime(TimeSpan value, TimeSpan maximum)
    {
        return value > TimeSpan.Zero && value <= maximum;
    }
}
