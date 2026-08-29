using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
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
        store
            .Setup(item => item.TryBeginSubmission("Handle"))
            .Returns(new SubmissionAcquisition
            {
                Outcome = SubmissionAcquisitionOutcome.Acquired,
                Submission = submission,
            });
        consentService
            .Setup(item => item.GetState())
            .Returns(ErrorReportingConsentState.AlwaysApproved);
        dispatcher
            .Setup(item => item.DispatchAsync(
                submission.Payload,
                ExceptionMessageHandling.Include,
                CancellationToken.None))
            .ReturnsAsync(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Accepted,
                ReportReference = "ReportReference",
                PayloadDigest = "PayloadDigest",
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
                ExceptionMessageHandling.Include,
                CancellationToken.None),
            Times.Once);
        store.Verify(
            item => item.Complete(
                "Handle",
                It.Is<ErrorSubmissionReceipt>(receipt =>
                    receipt.ReportReference == "ReportReference"
                    && receipt.PayloadDigest == "PayloadDigest")),
            Times.Once);
    }

    [Fact]
    public async Task GIVEN_UnknownPreparedHandle_WHEN_Submitting_THEN_ShouldReturnUnavailable()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.UnknownOrExpired,
        });
        var target = CreateTarget(store, consentService, dispatcher);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            CreateArguments(),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("PreparedReportUnavailable");
        consentService.Verify(item => item.GetState(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_DisabledConsentState_WHEN_Submitting_THEN_ShouldFailClosedWithoutDispatch()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.Disabled);
        var target = CreateTarget(store, consentService, dispatcher);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            CreateArguments(),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("ErrorReportingUnavailable");
        store.Verify(item => item.ReleaseForRetry("Handle"), Times.Once);
        dispatcher.Verify(item => item.DispatchAsync(
            It.IsAny<PreparedDispatchPayload>(),
            It.IsAny<ExceptionMessageHandling>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredWithoutElicitationCapability_WHEN_Submitting_THEN_ShouldFailClosedWithoutDispatch()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService
            .Setup(item => item.GetState())
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
                It.IsAny<ExceptionMessageHandling>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        store.Verify(item => item.ReleaseForRetry("Handle"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredWithoutClientCapabilities_WHEN_Submitting_THEN_ShouldFailClosedWithoutDispatch()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.PromptRequired);
        var target = CreateTarget(store, consentService, dispatcher);
        await using var server = ServerOwnedToolTestSupport.CreateServer();
        Mock.Get(server).SetupGet(item => item.ClientCapabilities).Returns((ClientCapabilities?)null);

        var result = await target.InvokeAsync(CreateRequestContext(server), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("ApprovalUnavailable");
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<ExceptionMessageHandling>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        store.Verify(item => item.ReleaseForRetry("Handle"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredAndSendApproval_WHEN_Submitting_THEN_ShouldDispatch()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store
            .Setup(item => item.TryBeginSubmission("Handle"))
            .Returns(new SubmissionAcquisition
            {
                Outcome = SubmissionAcquisitionOutcome.Acquired,
                Submission = submission,
            });
        consentService
            .Setup(item => item.GetState())
            .Returns(ErrorReportingConsentState.PromptRequired);
        store.Setup(item => item.TryConfirmSubmission("Handle")).Returns(true);
        dispatcher
            .Setup(item => item.DispatchAsync(
                submission.Payload,
                ExceptionMessageHandling.Include,
                CancellationToken.None))
            .ReturnsAsync(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Accepted,
                ReportReference = "ReportReference",
                PayloadDigest = "PayloadDigest",
            });
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            _requestBinder.Object,
            store.Object,
            consentService.Object,
            dispatcher.Object);
        var response = CreateAcceptedResponse("send");
        await using var server = CreateElicitationServer(response);
        var requestContext = CreateRequestContext(server);

        var result = await target.InvokeAsync(
            requestContext,
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        dispatcher.Verify(
            item => item.DispatchAsync(
                submission.Payload,
                ExceptionMessageHandling.Include,
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredAndRedactedApproval_WHEN_Submitting_THEN_ShouldDispatchRedactedPayloadAndReturnItsDigest()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        var expectedDigest = Convert.ToHexStringLower(
            SHA256.HashData(ImmutableArray.Create<byte>(4, 5, 6).AsSpan()));
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.PromptRequired);
        store.Setup(item => item.TryConfirmSubmission("Handle")).Returns(true);
        dispatcher
            .Setup(item => item.DispatchAsync(
                submission.Payload,
                ExceptionMessageHandling.Remove,
                CancellationToken.None))
            .ReturnsAsync(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Accepted,
                ReportReference = "ReportReference",
                PayloadDigest = expectedDigest,
            });
        var target = new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            McpToolProtocolFactoryMockFactory.Create().Object,
            _requestBinder.Object,
            store.Object,
            consentService.Object,
            dispatcher.Object);
        await using var server = CreateElicitationServer(CreateAcceptedResponse("send-without-exception-messages"));

        var result = await target.InvokeAsync(CreateRequestContext(server), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("payloadDigest").GetString()
            .Should().Be(expectedDigest);
        dispatcher.Verify(item => item.DispatchAsync(
            submission.Payload,
            ExceptionMessageHandling.Remove,
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PreparedReportExpiresDuringElicitation_WHEN_UserApproves_THEN_ShouldNotDispatch()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        store.Setup(item => item.TryConfirmSubmission("Handle")).Returns(false);
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.PromptRequired);
        var target = CreateTarget(store, consentService, dispatcher);
        await using var server = CreateElicitationServer(CreateAcceptedResponse("send"));

        var result = await target.InvokeAsync(CreateRequestContext(server), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("PreparedReportUnavailable");
        dispatcher.Verify(item => item.DispatchAsync(
            It.IsAny<PreparedDispatchPayload>(),
            It.IsAny<ExceptionMessageHandling>(),
            It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(item => item.Complete(It.IsAny<string>(), It.IsAny<ErrorSubmissionReceipt>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredAndClientDeclinesElicitation_WHEN_Submitting_THEN_ShouldReportNotApproved()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.PromptRequired);
        var target = new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            McpToolProtocolFactoryMockFactory.Create().Object,
            _requestBinder.Object,
            store.Object,
            consentService.Object,
            dispatcher.Object);
        var response = new JsonRpcResponse
        {
            Result = new JsonObject { ["action"] = "decline" },
        };
        await using var server = CreateElicitationServer(response);

        var result = await target.InvokeAsync(CreateRequestContext(server), CancellationToken.None);

        result.IsError.Should().BeTrue();
        var error = result.StructuredContent!.Value.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("ErrorReportNotApproved");
        error.GetProperty("message").GetString().Should().Contain("client may have blocked MCP elicitation");
        store.Verify(item => item.Discard("Handle"), Times.Once);
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<ExceptionMessageHandling>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredAndUnsupportedChoice_WHEN_Submitting_THEN_ShouldRejectApprovalResponse()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.PromptRequired);
        var target = CreateTarget(store, consentService, dispatcher);
        await using var server = CreateElicitationServer(CreateAcceptedResponse("unsupported"));

        var result = await target.InvokeAsync(CreateRequestContext(server), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("InvalidApprovalResponse");
        store.Verify(item => item.Discard(It.IsAny<string>()), Times.Never);
        store.Verify(item => item.ReleaseForRetry("Handle"), Times.Once);
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<ExceptionMessageHandling>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GIVEN_ClientFailsElicitation_WHEN_Submitting_THEN_ShouldFailClosed(bool protocolFailure)
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.PromptRequired);
        var target = CreateTarget(store, consentService, dispatcher);
        Exception exception = protocolFailure
            ? new McpException("Elicitation failed.")
            : new InvalidOperationException("Elicitation is unavailable.");
        await using var server = CreateElicitationServer(new JsonRpcResponse
        {
            Result = new JsonObject(),
        });
        Mock.Get(server)
            .Setup(item => item.SendRequestAsync(It.IsAny<JsonRpcRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var result = await target.InvokeAsync(CreateRequestContext(server), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("ApprovalUnavailable");
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<ExceptionMessageHandling>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        store.Verify(item => item.ReleaseForRetry("Handle"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredAndDoNotSendChoice_WHEN_Submitting_THEN_ShouldDiscardAndNotDispatch()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService
            .Setup(item => item.GetState())
            .Returns(ErrorReportingConsentState.PromptRequired);
        var protocolFactory = McpToolProtocolFactoryMockFactory.Create();
        var target = new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            protocolFactory.Object,
            _requestBinder.Object,
            store.Object,
            consentService.Object,
            dispatcher.Object);
        var response = CreateAcceptedResponse("do-not-send");
        await using var server = CreateElicitationServer(response);
        var requestContext = CreateRequestContext(server);

        var result = await target.InvokeAsync(
            requestContext,
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("ErrorReportNotApproved");
        store.Verify(item => item.Discard("Handle"), Times.Once);
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<ExceptionMessageHandling>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredAndClientCancellation_WHEN_Submitting_THEN_ShouldDiscardWithoutDispatch()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService
            .Setup(item => item.GetState())
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
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("ErrorReportNotApproved");
        store.Verify(item => item.Discard("Handle"), Times.Once);
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<ExceptionMessageHandling>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData((int)SubmissionAcquisitionOutcome.UnknownOrExpired, "PreparedReportUnavailable")]
    [InlineData((int)SubmissionAcquisitionOutcome.InProgress, "ErrorReportSubmissionInProgress")]
    public async Task GIVEN_SubmissionCannotBeAcquired_WHEN_Submitting_THEN_ShouldReturnAcquisitionFailure(
        int outcomeValue,
        string expectedCode)
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = (SubmissionAcquisitionOutcome)outcomeValue,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.PromptRequired);
        var target = CreateTarget(store, consentService, dispatcher);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            CreateArguments(),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be(expectedCode);
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<ExceptionMessageHandling>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        consentService.Verify(item => item.GetState(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PromptRequiredPreviouslySentSubmission_WHEN_Submitting_THEN_ShouldReturnExistingReceiptWithoutElicitation()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission() with
        {
            State = PreparedSubmissionState.Sent,
            Receipt = new ErrorSubmissionReceipt
            {
                Dispatcher = "Dispatcher",
                ReportReference = "ExistingReference",
                PayloadDigest = "ExistingDigest",
            },
        };
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.AlreadySent,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.PromptRequired);
        var target = CreateTarget(store, consentService, dispatcher);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            CreateArguments(),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("reportReference").GetString()
            .Should().Be("ExistingReference");
        dispatcher.Verify(
            item => item.DispatchAsync(
                It.IsAny<PreparedDispatchPayload>(),
                It.IsAny<ExceptionMessageHandling>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        consentService.Verify(item => item.GetState(), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PreviouslySentAcquisitionWithoutSubmission_WHEN_Submitting_THEN_ShouldRejectInvalidStoreState()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.AlreadySent,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.AlwaysApproved);
        var target = CreateTarget(store, consentService, dispatcher);

        var action = async () => await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            CreateArguments(),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GIVEN_AcquisitionWithoutSubmission_WHEN_Submitting_THEN_ShouldRejectInvalidStoreState()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.AlwaysApproved);
        var target = CreateTarget(store, consentService, dispatcher);

        var action = async () => await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            CreateArguments(),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GIVEN_DispatcherRejectsSubmission_WHEN_Submitting_THEN_ShouldReleasePreparedReportForRetry()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.AlwaysApproved);
        dispatcher.Setup(item => item.DispatchAsync(
            submission.Payload,
            ExceptionMessageHandling.Include,
            CancellationToken.None)).ReturnsAsync(
            new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Rejected,
                ErrorCode = "ProviderRejected",
                ErrorMessage = "Provider rejected the report.",
            });
        var target = CreateTarget(store, consentService, dispatcher);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            CreateArguments(),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString()
            .Should().Be("ProviderRejected");
        store.Verify(item => item.ReleaseForRetry("Handle"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DispatcherRejectsWithoutDetails_WHEN_Submitting_THEN_ShouldReturnDefaultFailure()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.AlwaysApproved);
        dispatcher.Setup(item => item.DispatchAsync(
            submission.Payload,
            ExceptionMessageHandling.Include,
            CancellationToken.None)).ReturnsAsync(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Rejected,
            });
        var target = CreateTarget(store, consentService, dispatcher);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            CreateArguments(),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        var error = result.StructuredContent!.Value.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("ErrorReportDispatchFailed");
        error.GetProperty("message").GetString().Should().Be("The error report could not be submitted.");
        store.Verify(item => item.ReleaseForRetry("Handle"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_AcceptedDispatchWithoutReference_WHEN_Submitting_THEN_ShouldUseReportIdentifier()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.AlwaysApproved);
        dispatcher.Setup(item => item.DispatchAsync(
            submission.Payload,
            ExceptionMessageHandling.Include,
            CancellationToken.None)).ReturnsAsync(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Accepted,
                PayloadDigest = "PayloadDigest",
            });
        var target = CreateTarget(store, consentService, dispatcher);

        var result = await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            CreateArguments(),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.StructuredContent!.Value.GetProperty("data").GetProperty("reportReference").GetString()
            .Should().Be(submission.Payload.ReportId);
    }

    [Fact]
    public async Task GIVEN_AcceptedDispatchWithoutDigest_WHEN_Submitting_THEN_ShouldReleasePreparedReportForRetry()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.AlwaysApproved);
        dispatcher.Setup(item => item.DispatchAsync(
            submission.Payload,
            ExceptionMessageHandling.Include,
            CancellationToken.None)).ReturnsAsync(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Accepted,
                ReportReference = "ReportReference",
            });
        var target = CreateTarget(store, consentService, dispatcher);

        var action = async () => await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            CreateArguments(),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        store.Verify(item => item.ReleaseForRetry("Handle"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_MessageRemovalDispatchThrows_WHEN_Submitting_THEN_ShouldReleasePreparedReportForRetry()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.PromptRequired);
        store.Setup(item => item.TryConfirmSubmission("Handle")).Returns(true);
        dispatcher.Setup(item => item.DispatchAsync(
            submission.Payload,
            ExceptionMessageHandling.Remove,
            CancellationToken.None))
            .Throws(new InvalidOperationException("Redaction failed."));
        var target = CreateTarget(store, consentService, dispatcher);
        await using var server = CreateElicitationServer(CreateAcceptedResponse("send-without-exception-messages"));

        var action = async () => await target.InvokeAsync(CreateRequestContext(server), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        store.Verify(item => item.ReleaseForRetry("Handle"), Times.Once);
    }

    [Fact]
    public async Task GIVEN_DispatcherThrows_WHEN_Submitting_THEN_ShouldReleasePreparedReportForRetry()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store
            .Setup(item => item.TryBeginSubmission("Handle"))
            .Returns(new SubmissionAcquisition
            {
                Outcome = SubmissionAcquisitionOutcome.Acquired,
                Submission = submission,
            });
        consentService
            .Setup(item => item.GetState())
            .Returns(ErrorReportingConsentState.AlwaysApproved);
        dispatcher
            .Setup(item => item.DispatchAsync(
                submission.Payload,
                ExceptionMessageHandling.Include,
                CancellationToken.None))
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

    [Fact]
    public async Task GIVEN_DispatcherCancels_WHEN_Submitting_THEN_ShouldReleasePreparedReportForRetry()
    {
        var store = new Mock<IPreparedSubmissionStore>();
        var consentService = new Mock<IErrorReportingConsentService>();
        var dispatcher = new Mock<IErrorReportDispatcher>();
        var submission = CreateSubmission();
        store.Setup(item => item.TryBeginSubmission("Handle")).Returns(new SubmissionAcquisition
        {
            Outcome = SubmissionAcquisitionOutcome.Acquired,
            Submission = submission,
        });
        consentService.Setup(item => item.GetState()).Returns(ErrorReportingConsentState.AlwaysApproved);
        dispatcher.Setup(item => item.DispatchAsync(
            submission.Payload,
            ExceptionMessageHandling.Include,
            CancellationToken.None))
            .ThrowsAsync(new OperationCanceledException());
        var target = CreateTarget(store, consentService, dispatcher);

        var action = async () => await ServerOwnedToolTestSupport.InvokeAsync(
            target,
            "submit-error-report",
            CreateArguments(),
            CancellationToken.None);

        await action.Should().ThrowAsync<OperationCanceledException>();
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

    private SubmitErrorReportTool CreateTarget(
        Mock<IPreparedSubmissionStore> store,
        Mock<IErrorReportingConsentService> consentService,
        Mock<IErrorReportDispatcher> dispatcher)
    {
        return new SubmitErrorReportTool(
            Options.Create(new StartupOptions()),
            McpToolProtocolFactoryMockFactory.Create().Object,
            _requestBinder.Object,
            store.Object,
            consentService.Object,
            dispatcher.Object);
    }

    private static Dictionary<string, JsonElement> CreateArguments()
    {
        return new Dictionary<string, JsonElement>
        {
            ["submissionHandle"] = JsonSerializer.SerializeToElement("Handle"),
        };
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
