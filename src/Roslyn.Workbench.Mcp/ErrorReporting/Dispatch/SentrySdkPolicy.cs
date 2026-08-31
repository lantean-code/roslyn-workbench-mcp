using System.Reflection;
using Sentry;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Validates build-time Sentry configuration and defines the SDK shutdown policy.
/// </summary>
internal static class SentrySdkPolicy
{
    /// <summary>
    /// Gets the validated Sentry destination embedded at build time, when configured.
    /// </summary>
    public static SentryProviderConfiguration? EmbeddedConfiguration { get; } = CreateEmbeddedConfiguration();

    /// <summary>
    /// Gets the maximum time allowed for Sentry to flush events during shutdown.
    /// </summary>
    public static TimeSpan ShutdownTimeout { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Validates a Sentry DSN and creates the corresponding submission configuration.
    /// </summary>
    /// <param name="dsn">The parsed Sentry data-source name used to configure submissions.</param>
    /// <returns>The validated Sentry configuration and user-facing destination.</returns>
    public static SentryProviderConfiguration CreateConfiguration(string dsn)
    {
        if (!Uri.TryCreate(dsn, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(uri.UserInfo)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException(
                "The build-embedded Sentry DSN must be an absolute HTTPS Sentry DSN with a public key and host.");
        }

        var projectId = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(projectId) || projectId.Contains('/', StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The build-embedded Sentry DSN must identify exactly one Sentry project.");
        }

        return new SentryProviderConfiguration(
            dsn,
            $"Sentry project {projectId} at {uri.Host}");
    }

    private static SentryProviderConfiguration? CreateEmbeddedConfiguration()
    {
        var dsn = typeof(SentrySdkPolicy).Assembly.GetCustomAttribute<DsnAttribute>()?.Dsn;
        return string.IsNullOrWhiteSpace(dsn) ? null : CreateConfiguration(dsn);
    }
}
