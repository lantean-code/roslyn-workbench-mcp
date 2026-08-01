using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Test.Tools;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class PrepareErrorReportToolTests
{
    [Fact]
    public async Task GIVEN_CapturedErrorAndAvailableDispatcher_WHEN_Preparing_THEN_ShouldStoreImmutableReportAndPreviewWithoutDispatching()
    {
        var correlationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var capturedErrorStore = new Mock<ICapturedErrorStore>();
        var preparedSubmissionStore = new Mock<IPreparedSubmissionStore>();
        var projector = new Mock<IExternalErrorReportProjector>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var availabilityService = new Mock<IErrorReportingAvailabilityService>();
        var timeProvider = new Mock<TimeProvider>();
        var record = CreateRecord(correlationId);
        var externalReport = CreateExternalReport();
        const string previewJson = "{\"value\":\"Value\"}";
        var payload = new PreparedDispatchPayload<string>
        {
            DispatcherName = "Dispatcher",
            Destination = "Destination",
            ReportId = "ReportId",
            Report = externalReport,
            PreviewBytes = Encoding.UTF8.GetBytes(previewJson).ToImmutableArray(),
            PreviewJson = previewJson,
            DispatchState = previewJson,
        };
        CapturedErrorRecord? storedRecord = record;
        capturedErrorStore
            .Setup(item => item.TryGet(correlationId, out storedRecord))
            .Returns(true);
        projector
            .Setup(item => item.Project(record, It.IsAny<string>()))
            .Returns(externalReport);
        dispatcher
            .Setup(item => item.CreatePayload(externalReport))
            .Returns(payload);
        availabilityService
            .Setup(item => item.GetAvailability(null, null, null))
            .Returns(new ErrorReportingAvailability
            {
                State = ErrorReportingState.Available,
                CanPrepare = true,
                PrepareTool = "prepare-error-report",
            });
        preparedSubmissionStore
            .Setup(item => item.TryAdd(It.IsAny<PreparedSubmission>()))
            .Returns(true);
        timeProvider
            .Setup(item => item.GetUtcNow())
            .Returns(DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture));
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var options = new ErrorReportingOptions
        {
            ConsentMode = ErrorReportingConsentMode.Prompt,
            MaximumPayloadBytes = 8 * 1024,
        };
        var boundRequest = new PrepareErrorReportRequest { CorrelationId = correlationId };
        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out boundRequest,
                out errorMessage))
            .Returns(true);
        var target = new PrepareErrorReportTool(
            Options.Create(new StartupOptions()),
            Options.Create(options),
            protocolFactory.Object,
            requestBinder.Object,
            capturedErrorStore.Object,
            preparedSubmissionStore.Object,
            projector.Object,
            dispatcher.Object,
            availabilityService.Object,
            timeProvider.Object);
        var arguments = new Dictionary<string, JsonElement>
        {
            ["correlationId"] = JsonSerializer.SerializeToElement(correlationId),
        };

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "prepare-error-report",
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var data = result.StructuredContent!.Value.GetProperty("data");
        data.GetProperty("destination").GetString().Should().Be("Destination");
        data.GetProperty("payloadJson").GetString().Should().Be(previewJson);
        preparedSubmissionStore.Verify(item => item.TryAdd(
            It.Is<PreparedSubmission>(submission =>
                submission.Payload.Report == externalReport
                && submission.Payload.PreviewBytes.SequenceEqual(payload.PreviewBytes)
                && submission.CorrelationId == correlationId)), Times.Once);
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CapturedErrorRecord CreateRecord(Guid correlationId)
    {
        return new CapturedErrorRecord
        {
            CorrelationId = correlationId,
            FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            ExpiresAt = DateTimeOffset.Parse("2000-01-01T01:00:00Z", CultureInfo.InvariantCulture),
            ToolName = "server-status",
            ExecutionFamily = "ServerOwned",
            PluginClassification = "Host",
            DurationMilliseconds = 25,
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "Linux",
            ProcessorArchitecture = "X64",
        };
    }

    private static ExternalErrorReport CreateExternalReport()
    {
        return new ExternalErrorReport
        {
            ReportId = "ReportId",
            FailureTime = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            Tool = "server-status",
            ExecutionFamily = "ServerOwned",
            PluginClassification = "Host",
            DurationMilliseconds = 25,
            ExceptionClassification = "System.InvalidOperationException",
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "Linux",
            ProcessorArchitecture = "X64",
        };
    }
}
