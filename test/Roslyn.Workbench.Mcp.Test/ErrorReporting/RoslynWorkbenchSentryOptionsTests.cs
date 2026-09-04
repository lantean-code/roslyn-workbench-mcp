using Sentry;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class RoslynWorkbenchSentryOptionsTests
{
    private const string _destination = "Sentry project 1000000000000000 at o100000.ingest.us.sentry.io";
    private const string _dsn = "https://0123456789abcdef0123456789abcdef@o100000.ingest.us.sentry.io/1000000000000000";

    [Fact]
    public void GIVEN_SentryConfiguration_WHEN_CreatingOptions_THEN_ShouldDisableUnreviewedEnrichmentAndIsolateTransport()
    {
        var configuration = new SentryProviderConfiguration(_dsn, _destination);
        var target = new RoslynWorkbenchSentryOptions(configuration);

        target.Dsn.Should().Be(_dsn);
        target.AttachStacktrace.Should().BeFalse();
        target.AutoSessionTracking.Should().BeFalse();
        target.DisableSentryHttpMessageHandler.Should().BeTrue();
        target.EnableLogs.Should().BeFalse();
        target.IsGlobalModeEnabled.Should().BeFalse();
        target.ReportAssembliesMode.Should().Be(ReportAssembliesMode.None);
        target.SendClientReports.Should().BeFalse();
        target.SendDefaultPii.Should().BeFalse();
        target.ShutdownTimeout.Should().Be(SentrySdkPolicy.ShutdownTimeout);
        var handlerFactory = target.CreateHttpMessageHandler
            ?? throw new InvalidOperationException("The isolated HTTP handler factory was not configured.");
        using var handler = handlerFactory();
        var httpHandler = handler.Should().BeOfType<HttpClientHandler>().Subject;
        httpHandler.AllowAutoRedirect.Should().BeFalse();
        httpHandler.CheckCertificateRevocationList.Should().BeTrue();
    }
}
