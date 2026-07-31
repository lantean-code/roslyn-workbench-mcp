using System.Net.Http;
using Sentry;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

internal sealed class RoslynWorkbenchSentryOptions : SentryOptions
{
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
