using System.Reflection;
using Sentry;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

internal static class SentrySdkPolicy
{
    public static SentryProviderConfiguration? EmbeddedConfiguration { get; } = CreateEmbeddedConfiguration();

    public static TimeSpan ShutdownTimeout { get; } = TimeSpan.FromSeconds(10);

    internal static SentryProviderConfiguration CreateConfiguration(string dsn)
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
