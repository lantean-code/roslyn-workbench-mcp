using Sentry;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Restricts the Sentry SDK to explicit, allow-listed Roslyn Workbench error submissions.
/// </summary>
internal sealed class RoslynWorkbenchSentryOptions : SentryOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RoslynWorkbenchSentryOptions"/> class.
    /// </summary>
    /// <param name="configuration">The validated Sentry endpoint configuration.</param>
    public RoslynWorkbenchSentryOptions(SentryProviderConfiguration configuration)
    {
        Dsn = configuration.Dsn;
        AttachStacktrace = false;
        AutoSessionTracking = false;
        DisableSentryHttpMessageHandler = true;
        EnableLogs = false;
        EnableMetrics = false;
        IsGlobalModeEnabled = false;
        ReportAssembliesMode = ReportAssembliesMode.None;
        SendClientReports = false;
        SendDefaultPii = false;
        ShutdownTimeout = SentrySdkPolicy.ShutdownTimeout;
        CreateHttpMessageHandler = CreateIsolatedHttpMessageHandler;
        SetBeforeSend(SentryEventAllowList.CreateAllowedCopy);
    }

    private static HttpClientHandler CreateIsolatedHttpMessageHandler()
    {
        return new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CheckCertificateRevocationList = true,
        };
    }
}
