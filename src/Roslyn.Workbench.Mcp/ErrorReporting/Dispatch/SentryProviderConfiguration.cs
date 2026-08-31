namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Holds the validated Sentry connection value and the destination disclosed during approval.
/// </summary>
internal sealed record SentryProviderConfiguration
{
    /// <summary>
    /// Gets the Sentry data-source name used by the SDK.
    /// </summary>
    public string Dsn { get; }

    /// <summary>
    /// Gets the user-facing Sentry project description shown before submission.
    /// </summary>
    public string Destination { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SentryProviderConfiguration"/> class.
    /// </summary>
    /// <param name="dsn">The parsed Sentry data-source name used to configure submissions.</param>
    /// <param name="destination">The user-facing destination shown during error-report approval.</param>
    public SentryProviderConfiguration(string dsn, string destination)
    {
        Dsn = dsn;
        Destination = destination;
    }
}
