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
        var workspaceIdentity = new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            WorkspaceEpoch = 7,
        };
        var workspace = new CapturedWorkspaceContext(
            workspaceIdentity,
            WorkspaceLifecycleState.Ready,
            projectCount: 3,
            documentCount: 20,
            transactionRevision: null);
        var record = CreateRecord(correlationId) with { Workspace = workspace };
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
            .Setup(item => item.GetAvailability(
                workspaceIdentity.WorkspaceId,
                workspaceIdentity.WorkspaceEpoch,
                null))
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
        data.GetProperty("excludedCategories").EnumerateArray().Select(item => item.GetString()).Should().Equal(
            "dedicated source text and document content fields",
            "dedicated user-authored identifier and path fields",
            "dedicated repository, solution and project identity fields",
            "dedicated user, machine and stable installation identity fields",
            "dedicated environment variable and process command-line fields",
            "dedicated credential, token and secret fields",
            "dedicated agent prompt and conversation content fields",
            "dedicated raw log fields");
        data.GetProperty("reviewWarnings").EnumerateArray().Select(item => item.GetString()).Should().Equal(
            "Exception messages are bounded but otherwise unfiltered reviewed content and may contain source text, paths, identifiers, credentials, tokens or secrets.");
        preparedSubmissionStore.Verify(item => item.TryAdd(
            It.Is<PreparedSubmission>(submission =>
                submission.Payload.Report == externalReport
                && submission.Payload.PreviewBytes.SequenceEqual(payload.PreviewBytes)
                && submission.CorrelationId == correlationId)), Times.Once);
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<ExceptionMessageHandling>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_UnknownCapturedError_WHEN_Preparing_THEN_ShouldReturnUnavailable()
    {
        var correlationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var capturedErrorStore = new Mock<ICapturedErrorStore>();
        var preparedSubmissionStore = new Mock<IPreparedSubmissionStore>();
        var projector = new Mock<IExternalErrorReportProjector>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var availabilityService = new Mock<IErrorReportingAvailabilityService>();
        var timeProvider = new Mock<TimeProvider>();
        CapturedErrorRecord? storedRecord = null;
        capturedErrorStore.Setup(item => item.TryGet(correlationId, out storedRecord)).Returns(false);
        var boundRequest = new PrepareErrorReportRequest { CorrelationId = correlationId };
        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder.Setup(item => item.TryBind(
            It.IsAny<IDictionary<string, JsonElement>>(),
            out boundRequest,
            out errorMessage)).Returns(true);
        var target = CreateTarget(
            new ErrorReportingOptions(),
            requestBinder,
            capturedErrorStore,
            preparedSubmissionStore,
            projector,
            dispatcher,
            availabilityService,
            timeProvider);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "prepare-error-report",
            CreateArguments(correlationId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        GetErrorCode(result).Should().Be("ErrorDetailsUnavailable");
        availabilityService.Verify(
            item => item.GetAvailability(It.IsAny<Guid?>(), It.IsAny<long?>(), It.IsAny<bool?>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_ErrorReportingUnavailable_WHEN_Preparing_THEN_ShouldReturnUnavailable()
    {
        var correlationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var capturedErrorStore = new Mock<ICapturedErrorStore>();
        var preparedSubmissionStore = new Mock<IPreparedSubmissionStore>();
        var projector = new Mock<IExternalErrorReportProjector>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var availabilityService = new Mock<IErrorReportingAvailabilityService>();
        var timeProvider = new Mock<TimeProvider>();
        var record = CreateRecord(correlationId);
        CapturedErrorRecord? storedRecord = record;
        capturedErrorStore.Setup(item => item.TryGet(correlationId, out storedRecord)).Returns(true);
        availabilityService.Setup(item => item.GetAvailability(null, null, null)).Returns(
            new ErrorReportingAvailability
            {
                State = ErrorReportingState.DisabledByConfiguration,
                CanPrepare = false,
            });
        var boundRequest = new PrepareErrorReportRequest { CorrelationId = correlationId };
        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder.Setup(item => item.TryBind(
            It.IsAny<IDictionary<string, JsonElement>>(),
            out boundRequest,
            out errorMessage)).Returns(true);
        var target = CreateTarget(
            new ErrorReportingOptions(),
            requestBinder,
            capturedErrorStore,
            preparedSubmissionStore,
            projector,
            dispatcher,
            availabilityService,
            timeProvider);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "prepare-error-report",
            CreateArguments(correlationId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        GetErrorCode(result).Should().Be("ErrorReportingUnavailable");
        projector.Verify(item => item.Project(It.IsAny<CapturedErrorRecord>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ExternalPayloadExceedsLimit_WHEN_Preparing_THEN_ShouldRejectPayload()
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
        var payload = CreatePayload(externalReport, "PayloadTooLarge");
        CapturedErrorRecord? storedRecord = record;
        capturedErrorStore.Setup(item => item.TryGet(correlationId, out storedRecord)).Returns(true);
        availabilityService.Setup(item => item.GetAvailability(null, null, null)).Returns(
            new ErrorReportingAvailability
            {
                State = ErrorReportingState.Available,
                CanPrepare = true,
            });
        projector.Setup(item => item.Project(record, It.IsAny<string>())).Returns(externalReport);
        dispatcher.Setup(item => item.CreatePayload(externalReport)).Returns(payload);
        var boundRequest = new PrepareErrorReportRequest { CorrelationId = correlationId };
        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder.Setup(item => item.TryBind(
            It.IsAny<IDictionary<string, JsonElement>>(),
            out boundRequest,
            out errorMessage)).Returns(true);
        var options = new ErrorReportingOptions { MaximumPayloadBytes = 1 };
        var target = CreateTarget(
            options,
            requestBinder,
            capturedErrorStore,
            preparedSubmissionStore,
            projector,
            dispatcher,
            availabilityService,
            timeProvider);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "prepare-error-report",
            CreateArguments(correlationId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        GetErrorCode(result).Should().Be("ErrorReportPayloadTooLarge");
        preparedSubmissionStore.Verify(item => item.TryAdd(It.IsAny<PreparedSubmission>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PreparedReportCapacityReached_WHEN_Preparing_THEN_ShouldRejectReport()
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
        var payload = CreatePayload(externalReport, "{}");
        CapturedErrorRecord? storedRecord = record;
        capturedErrorStore.Setup(item => item.TryGet(correlationId, out storedRecord)).Returns(true);
        availabilityService.Setup(item => item.GetAvailability(null, null, null)).Returns(
            new ErrorReportingAvailability
            {
                State = ErrorReportingState.Available,
                CanPrepare = true,
            });
        projector.Setup(item => item.Project(record, It.IsAny<string>())).Returns(externalReport);
        dispatcher.Setup(item => item.CreatePayload(externalReport)).Returns(payload);
        preparedSubmissionStore.Setup(item => item.TryAdd(It.IsAny<PreparedSubmission>())).Returns(false);
        timeProvider.Setup(item => item.GetUtcNow()).Returns(
            DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture));
        var boundRequest = new PrepareErrorReportRequest { CorrelationId = correlationId };
        string? errorMessage = null;
        var requestBinder = new Mock<IToolRequestBinder>();
        requestBinder.Setup(item => item.TryBind(
            It.IsAny<IDictionary<string, JsonElement>>(),
            out boundRequest,
            out errorMessage)).Returns(true);
        var target = CreateTarget(
            new ErrorReportingOptions { MaximumPayloadBytes = 1024 },
            requestBinder,
            capturedErrorStore,
            preparedSubmissionStore,
            projector,
            dispatcher,
            availabilityService,
            timeProvider);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "prepare-error-report",
            CreateArguments(correlationId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        GetErrorCode(result).Should().Be("ErrorReportCapacityReached");
    }

    private static PrepareErrorReportTool CreateTarget(
        ErrorReportingOptions options,
        Mock<IToolRequestBinder> requestBinder,
        Mock<ICapturedErrorStore> capturedErrorStore,
        Mock<IPreparedSubmissionStore> preparedSubmissionStore,
        Mock<IExternalErrorReportProjector> projector,
        Mock<IErrorReportDispatcher> dispatcher,
        Mock<IErrorReportingAvailabilityService> availabilityService,
        Mock<TimeProvider> timeProvider)
    {
        return new PrepareErrorReportTool(
            Options.Create(new StartupOptions()),
            Options.Create(options),
            McpToolProtocolFactoryMockFactory.Create().Object,
            requestBinder.Object,
            capturedErrorStore.Object,
            preparedSubmissionStore.Object,
            projector.Object,
            dispatcher.Object,
            availabilityService.Object,
            timeProvider.Object);
    }

    private static Dictionary<string, JsonElement> CreateArguments(Guid correlationId)
    {
        return new Dictionary<string, JsonElement>
        {
            ["correlationId"] = JsonSerializer.SerializeToElement(correlationId),
        };
    }

    private static string? GetErrorCode(CallToolResult result)
    {
        return result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString();
    }

    private static PreparedDispatchPayload<string> CreatePayload(
        ExternalErrorReport externalReport,
        string previewJson)
    {
        return new PreparedDispatchPayload<string>
        {
            DispatcherName = "Dispatcher",
            Destination = "Destination",
            ReportId = "ReportId",
            Report = externalReport,
            PreviewBytes = Encoding.UTF8.GetBytes(previewJson).ToImmutableArray(),
            PreviewJson = previewJson,
            DispatchState = previewJson,
        };
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
