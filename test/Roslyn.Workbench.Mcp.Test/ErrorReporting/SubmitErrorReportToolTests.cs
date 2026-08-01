using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Test.Tools;

namespace Roslyn.Workbench.Mcp.Test.ErrorReporting;

public sealed class SubmitErrorReportToolTests
{
    private readonly Mock<IToolRequestBinder> _requestBinder;

    public SubmitErrorReportToolTests()
    {
        _requestBinder = new Mock<IToolRequestBinder>();
        var request = new SubmitErrorReportRequest { SubmissionHandle = "Handle" };
        string? errorMessage = null;
        _requestBinder
            .Setup(item => item.TryBind(
                It.IsAny<IDictionary<string, JsonElement>>(),
                out request,
                out errorMessage))
            .Returns(true);
    }

    [Fact]
    public async Task GIVEN_AlwaysApprovedPreparedReport_WHEN_Submitting_THEN_ShouldDispatchStoredPayloadAndPersistReceipt()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        PreparedSubmission? storedSubmission = submission;
        store
            .Setup(item => item.TryGet("Handle", out storedSubmission))
            .Returns(true);
        store
            .Setup(item => item.TryBeginSubmission("Handle"))
            .Returns(new SubmissionAcquisition
            {
                Outcome = SubmissionAcquisitionOutcome.Acquired,
                Submission = submission,
            });
        consentService
            .Setup(item => item.GetState(null, null))
            .Returns(ErrorReportingConsentState.AlwaysApproved);
        dispatcher
            .Setup(item => item.DispatchAsync(submission.Payload, CancellationToken.None))
            .ReturnsAsync(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Accepted,
                ReportReference = "ReportReference",
            });
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            _requestBinder.Object,
            store.Object,
            consentService.Object,
            dispatcher.Object);
        var arguments = new Dictionary<string, JsonElement>
        {
            ["submissionHandle"] = JsonSerializer.SerializeToElement("Handle"),
        };

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value
            .GetProperty("data")
            .GetProperty("reportReference")
            .GetString()
            .Should()
            .Be("ReportReference");
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.Is<PreparedDispatchPayload>(payload =>
                    payload.Report == submission.Payload.Report
                    && payload.PreviewBytes.SequenceEqual(submission.Payload.PreviewBytes)),
                CancellationToken.None),
            Times.Once);
        store.Verify(
            item => item.Complete(
                "Handle",
                It.Is<ErrorSubmissionReceipt>(receipt =>
                    receipt.ReportReference == "ReportReference")),
            Times.Once);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredWithoutElicitationCapability_WHEN_Submitting_THEN_ShouldFailClosedWithoutDispatch()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        PreparedSubmission? storedSubmission = submission;
        store
            .Setup(item => item.TryGet("Handle", out storedSubmission))
            .Returns(true);
        consentService
            .Setup(item => item.GetState(null, null))
            .Returns(ErrorReportingConsentState.PromptRequired);
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            _requestBinder.Object,
            store.Object,
            consentService.Object,
            dispatcher.Object);
        var arguments = new Dictionary<string, JsonElement>
        {
            ["submissionHandle"] = JsonSerializer.SerializeToElement("Handle"),
        };

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            arguments,
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value
            .GetProperty("error")
            .GetProperty("code")
            .GetString()
            .Should()
            .Be("ApprovalUnavailable");
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredAndSessionApproval_WHEN_Submitting_THEN_ShouldGrantSessionAndDispatch()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        PreparedSubmission? storedSubmission = submission;
        store.Setup(item => item.TryGet("Handle", out storedSubmission)).Returns(true);
        store
            .Setup(item => item.TryBeginSubmission("Handle"))
            .Returns(new SubmissionAcquisition
            {
                Outcome = SubmissionAcquisitionOutcome.Acquired,
                Submission = submission,
            });
        consentService
            .Setup(item => item.GetState(null, null))
            .Returns(ErrorReportingConsentState.PromptRequired);
        dispatcher
            .Setup(item => item.DispatchAsync(submission.Payload, CancellationToken.None))
            .ReturnsAsync(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Accepted,
                ReportReference = "ReportReference",
            });
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            _requestBinder.Object,
            store.Object,
            consentService.Object,
            dispatcher.Object);
        var response = CreateAcceptedResponse("allow-session");
        await using var server = CreateElicitationServer(response);
        var requestContext = CreateRequestContext(server);

        var result = await target.InvokeAsync(
            requestContext,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        consentService.Verify(item => item.AllowSession(), Times.Once);
        dispatcher.Verify(
            item => item.DispatchAsync(submission.Payload, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredAndSessionSuppression_WHEN_Submitting_THEN_ShouldDiscardAndNotDispatch()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        PreparedSubmission? storedSubmission = submission;
        store.Setup(item => item.TryGet("Handle", out storedSubmission)).Returns(true);
        consentService
            .Setup(item => item.GetState(null, null))
            .Returns(ErrorReportingConsentState.PromptRequired);
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            _requestBinder.Object,
            store.Object,
            consentService.Object,
            dispatcher.Object);
        var response = CreateAcceptedResponse("suppress-session");
        await using var server = CreateElicitationServer(response);
        var requestContext = CreateRequestContext(server);

        var result = await target.InvokeAsync(
            requestContext,
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        store.Verify(item => item.Discard("Handle"), Times.Once);
        consentService.Verify(item => item.SuppressSession(), Times.Once);
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredAndClientCancellation_WHEN_Submitting_THEN_ShouldRetainWithoutDispatch()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        PreparedSubmission? storedSubmission = submission;
        store.Setup(item => item.TryGet("Handle", out storedSubmission)).Returns(true);
        consentService
            .Setup(item => item.GetState(null, null))
            .Returns(ErrorReportingConsentState.PromptRequired);
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            _requestBinder.Object,
            store.Object,
            consentService.Object,
            dispatcher.Object);
        var response = new JsonRpcResponse
        {
            Result = new JsonObject
            {
                ["action"] = "cancel",
            },
        };
        await using var server = CreateElicitationServer(response);
        var requestContext = CreateRequestContext(server);

        var result = await target.InvokeAsync(
            requestContext,
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        store.Verify(item => item.Discard(It.IsAny<string>()), Times.Never);
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_DispatcherThrows_WHEN_Submitting_THEN_ShouldReleasePreparedReportForRetry()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        PreparedSubmission? storedSubmission = submission;
        store.Setup(item => item.TryGet("Handle", out storedSubmission)).Returns(true);
        store
            .Setup(item => item.TryBeginSubmission("Handle"))
            .Returns(new SubmissionAcquisition
            {
                Outcome = SubmissionAcquisitionOutcome.Acquired,
                Submission = submission,
            });
        consentService
            .Setup(item => item.GetState(null, null))
            .Returns(ErrorReportingConsentState.AlwaysApproved);
        dispatcher
            .Setup(item => item.DispatchAsync(submission.Payload, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Dispatch failed."));
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            _requestBinder.Object,
            store.Object,
            consentService.Object,
            dispatcher.Object);
        var arguments = new Dictionary<string, JsonElement>
        {
            ["submissionHandle"] = JsonSerializer.SerializeToElement("Handle"),
        };

        var action = async () => await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            arguments,
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        store.Verify(item => item.ReleaseForRetry("Handle"), Times.Once);
    }

    private static RequestContext<CallToolRequestParams> CreateRequestContext(McpServer server)
    {
        return new RequestContext<CallToolRequestParams>(
            server,
            new JsonRpcRequest
            {
                Method = RequestMethods.ToolsCall,
            },
            new CallToolRequestParams
            {
                Name = "submit-error-report",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["submissionHandle"] = JsonSerializer.SerializeToElement("Handle"),
                },
            });
    }

    private static JsonRpcResponse CreateAcceptedResponse(string choice)
    {
        return new JsonRpcResponse
        {
            Result = new JsonObject
            {
                ["action"] = "accept",
                ["content"] = new JsonObject
                {
                    ["choice"] = choice,
                },
            },
        };
    }

    private static McpServer CreateElicitationServer(JsonRpcResponse response)
    {
        var capabilities = new ClientCapabilities
        {
            Elicitation = new ElicitationCapability
            {
                Form = new FormElicitationCapability(),
            },
        };

        return ServerOwnedToolTestSupport.CreateServer(capabilities, response);
    }

    private static PreparedSubmission CreateSubmission()
    {
        return new PreparedSubmission
        {
            Handle = "Handle",
            CorrelationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreatedAt = DateTimeOffset.Parse("2000-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            ExpiresAt = DateTimeOffset.Parse("2000-01-01T00:30:00Z", CultureInfo.InvariantCulture),
            State = PreparedSubmissionState.Prepared,
            Payload = new PreparedDispatchPayload<string>
            {
                DispatcherName = "Dispatcher",
                Destination = "Destination",
                ReportId = "ReportId",
                Report = CreateExternalReport(),
                PreviewBytes = ImmutableArray.Create<byte>(1, 2, 3),
                PreviewJson = "PreviewJson",
                DispatchState = "DispatchState",
            },
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
            ExceptionClassification = "DotNetException",
            ServerVersion = "ServerVersion",
            RoslynVersion = "RoslynVersion",
            DotNetVersion = "DotNetVersion",
            OperatingSystem = "Linux",
            ProcessorArchitecture = "X64",
        };
    }

}
