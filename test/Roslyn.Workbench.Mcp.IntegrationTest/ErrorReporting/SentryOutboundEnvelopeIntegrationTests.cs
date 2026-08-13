using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Roslyn.Workbench.Mcp.ErrorReporting.Projection;
using Sentry.Protocol.Envelopes;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class SentryOutboundEnvelopeIntegrationTests
{
    private const string _destination = "Sentry project 1000000000000000 at o100000.ingest.us.sentry.io";
    private const string _dsn = "https://0123456789abcdef0123456789abcdef@o100000.ingest.us.sentry.io/1000000000000000";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ReviewedPreparedEvent_WHEN_RealSentryClientSerialisesEnvelope_THEN_ShouldSendOnlyReviewedJson()
    {
        var configuration = new SentryProviderConfiguration(_dsn, _destination);
        var options = new RoslynWorkbenchSentryOptions(configuration);
        var transport = new Mock<Sentry.Extensibility.ITransport>();
        var envelopeBytes = ImmutableArray<byte>.Empty;
        transport
            .Setup(item => item.SendEnvelopeAsync(
                It.IsAny<Envelope>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (Envelope envelope, CancellationToken cancellationToken) =>
            {
                await using var stream = new MemoryStream();
                await envelope.SerializeAsync(stream, options.DiagnosticLogger, cancellationToken);
                envelopeBytes = stream.ToArray().ToImmutableArray();
            });
        options.Transport = transport.Object;
        using var client = new SentryClient(options);
        var target = new SentryErrorReportDispatcher(client, configuration);
        var payload = target.CreatePayload(CreateReport());

        var result = await target.DispatchAsync(payload, TestContext.Current.CancellationToken);
        await client.FlushAsync(TimeSpan.FromSeconds(5));

        result.Outcome.Should().Be(ErrorDispatchOutcome.Accepted);
        envelopeBytes.Should().NotBeEmpty();
        var envelopeJson = Encoding.UTF8.GetString(envelopeBytes.AsSpan());
        var envelopeLines = envelopeJson.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        envelopeLines.Should().HaveCount(3);
        using var eventDocument = JsonDocument.Parse(envelopeLines[2]);
        eventDocument.RootElement.GetRawText().Should().Be(payload.PreviewJson);
        envelopeJson.Should().NotContain("threads");
        envelopeJson.Should().NotContain("modules");
        envelopeJson.Should().NotContain("runtime");
        envelopeJson.Should().NotContain("Roslyn.Workbench.Mcp.IntegrationTest");
        envelopeJson.Should().NotContain(nameof(SentryOutboundEnvelopeIntegrationTests) + ".cs");
        transport.Verify(item => item.SendEnvelopeAsync(
            It.IsAny<Envelope>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ExternalErrorReport CreateReport()
    {
        return new ExternalErrorReport
        {
            ReportId = "0123456789abcdef0123456789abcdef",
            FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            Tool = "server-status",
            ExecutionFamily = "ServerOwned",
            PluginClassification = "Host",
            DurationMilliseconds = 25,
            ExceptionClassification = "System.InvalidOperationException",
            StackFrames =
            [
                new ExternalStackFrame { Component = "RoslynWorkbench" },
                new ExternalStackFrame { Component = "Roslyn" },
            ],
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "Linux",
            ProcessorArchitecture = "X64",
        };
    }
}
