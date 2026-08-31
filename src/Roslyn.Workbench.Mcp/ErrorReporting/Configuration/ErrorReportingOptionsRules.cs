namespace Roslyn.Workbench.Mcp.ErrorReporting.Configuration;

/// <summary>
/// Defines the supported ranges and validation rules for error-reporting configuration.
/// </summary>
internal static class ErrorReportingOptionsRules
{
    /// <summary>
    /// Defines the smallest supported captured-error capacity.
    /// </summary>
    public const int MinimumCapturedErrorCapacity = 10;
    /// <summary>
    /// Defines the largest supported captured-error capacity.
    /// </summary>
    public const int MaximumCapturedErrorCapacity = 1_000;
    /// <summary>
    /// Defines the smallest supported per-error capture size in bytes.
    /// </summary>
    public const int MinimumCapturedErrorBytes = 16 * 1024;
    /// <summary>
    /// Defines the largest supported per-error capture size in bytes.
    /// </summary>
    public const int MaximumCapturedErrorBytes = 256 * 1024;
    /// <summary>
    /// Defines the smallest supported prepared-submission capacity.
    /// </summary>
    public const int MinimumPreparedSubmissionCapacity = 5;
    /// <summary>
    /// Defines the largest supported prepared-submission capacity.
    /// </summary>
    public const int MaximumPreparedSubmissionCapacity = 500;
    /// <summary>
    /// Defines the smallest supported outbound payload size in bytes.
    /// </summary>
    public const int MinimumPayloadBytes = 8 * 1024;
    /// <summary>
    /// Defines the largest supported outbound payload size in bytes.
    /// </summary>
    public const int MaximumPayloadBytes = 256 * 1024;
    /// <summary>
    /// Gets the longest supported lifetime for a captured error.
    /// </summary>
    public static TimeSpan MaximumCapturedErrorLifetime { get; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Gets the longest supported lifetime for a prepared submission.
    /// </summary>
    public static TimeSpan MaximumPreparedSubmissionLifetime { get; } = TimeSpan.FromHours(4);

    /// <summary>
    /// Determines whether a value lies within an inclusive range.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="minimum">The inclusive lower bound for the value.</param>
    /// <param name="maximum">The inclusive upper bound for the value.</param>
    /// <returns><see langword="true"/> when the value is within range; otherwise, <see langword="false"/>.</returns>
    public static bool IsWithinRange(int value, int minimum, int maximum)
    {
        return value >= minimum && value <= maximum;
    }

    /// <summary>
    /// Determines whether a positive lifetime does not exceed its configured maximum.
    /// </summary>
    /// <param name="value">The lifetime to validate.</param>
    /// <param name="maximum">The longest permitted lifetime.</param>
    /// <returns><see langword="true"/> when the lifetime is positive and no greater than the maximum; otherwise, <see langword="false"/>.</returns>
    public static bool IsWithinLifetime(TimeSpan value, TimeSpan maximum)
    {
        return value > TimeSpan.Zero && value <= maximum;
    }
}
