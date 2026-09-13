using System.Collections.ObjectModel;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Tools;

/// <summary>
/// Obtains configured consent and dispatches one previously reviewed payload at most once.
/// </summary>
internal sealed class SubmitErrorReportTool :
    ServerOwnedToolBase<SubmitErrorReportRequest, SubmittedErrorReportData>
{
    private const string _send = "send";
    private const string _sendWithoutExceptionMessages = "send-without-exception-messages";
    private const string _doNotSend = "do-not-send";
    private const string _notApprovedMessage = "No error report was sent. If no consent prompt was displayed, the client may have blocked MCP elicitation. Enable manual MCP approvals, prepare a new report, and try again. If you selected 'No', no further action is required.";
    private static readonly ReadOnlyCollection<UserInteractionChoice> _choices = CreateChoices();

    private readonly IPreparedSubmissionStore _store;
    private readonly IErrorReportingConsentService _consentService;
    private readonly IErrorReportDispatcher _dispatcher;
    private readonly IMcpUserInteractionServiceFactory _interactionServiceFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitErrorReportTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="store">The store containing prepared submissions awaiting approval.</param>
    /// <param name="consentService">The service that determines whether submission is disabled, prompted or pre-approved.</param>
    /// <param name="dispatcher">The dispatcher that sends the approved error-report payload.</param>
    /// <param name="interactionServiceFactory">The factory that isolates MCP elicitation behind a neutral interaction service.</param>
    public SubmitErrorReportTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IPreparedSubmissionStore store,
        IErrorReportingConsentService consentService,
        IErrorReportDispatcher dispatcher,
        IMcpUserInteractionServiceFactory interactionServiceFactory)
        : base(
            startupOptions,
            protocolFactory,
            requestBinder,
            ServerOwnedToolRegistration.SubmitErrorReportName,
            "Submit Error Report",
            "After applying the configured consent policy, submits one previously prepared immutable external error report to its reviewed destination.",
            readOnly: false,
            destructive: true,
            resultSummary: "Returns the dispatcher, immutable report reference and reviewed payload digest.",
            idempotent: true,
            openWorld: true)
    {
        _store = store;
        _consentService = consentService;
        _dispatcher = dispatcher;
        _interactionServiceFactory = interactionServiceFactory;
    }

    /// <inheritdoc/>
    protected override ValueTask<ToolResult<SubmittedErrorReportData>> ExecuteAsync(
        SubmitErrorReportRequest request,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Submission requires the active MCP request context.");
    }

    /// <inheritdoc/>
    protected override async ValueTask<CallToolResult> InvokeBoundRequestAsync(
        SubmitErrorReportRequest request,
        RequestContext<CallToolRequestParams> requestContext,
        CancellationToken cancellationToken)
    {
        var interactionService = _interactionServiceFactory.Create(requestContext.Server);
        var result = await ExecuteWithContextAsync(request, interactionService, cancellationToken);
        var content = result.Outcome.IsError()
            ? ToolResultEnvelopeSerializer.CreateFailure(result.Error, result.RequiredAction)
            : ToolResultEnvelopeSerializer.CreateSuccess(result.Data);

        return CreateStructuredResult(content, result.Outcome.IsError());
    }

    private async ValueTask<ToolResult<SubmittedErrorReportData>> ExecuteWithContextAsync(
        SubmitErrorReportRequest request,
        IUserInteractionService interactionService,
        CancellationToken cancellationToken)
    {
        var acquisition = _store.TryBeginSubmission(request.SubmissionHandle);
        if (acquisition.Outcome == SubmissionAcquisitionOutcome.UnknownOrExpired)
        {
            return CreateFailure(
                "PreparedReportUnavailable",
                "The submission handle is unknown or its temporary prepared payload has expired.");
        }

        if (acquisition.Outcome == SubmissionAcquisitionOutcome.InProgress)
        {
            return CreateFailure(
                "ErrorReportSubmissionInProgress",
                "This prepared report is already being submitted.");
        }

        if (acquisition.Outcome == SubmissionAcquisitionOutcome.AlreadySent)
        {
            return CreateSuccess(acquisition.Submission);
        }

        var submission = acquisition.Submission
            ?? throw new InvalidOperationException("An acquired error-report submission must include its prepared payload.");

        try
        {
            var messageHandling = ExceptionMessageHandling.Include;
            var consentState = _consentService.GetState();
            if (consentState == ErrorReportingConsentState.Disabled)
            {
                _store.ReleaseForRetry(request.SubmissionHandle);
                return CreateFailure(
                    "ErrorReportingUnavailable",
                    "Error reporting is disabled by configuration; nothing was submitted.");
            }

            if (consentState == ErrorReportingConsentState.PromptRequired)
            {
                var consentResult = await RequestConsentAsync(
                    interactionService,
                    submission,
                    cancellationToken);

                if (consentResult.Failure is not null)
                {
                    if (consentResult.DiscardSubmission)
                    {
                        _store.Discard(request.SubmissionHandle);
                    }
                    else
                    {
                        _store.ReleaseForRetry(request.SubmissionHandle);
                    }

                    return consentResult.Failure;
                }

                messageHandling = consentResult.MessageHandling;

                if (!_store.TryConfirmSubmission(request.SubmissionHandle))
                {
                    return CreateFailure(
                        "PreparedReportUnavailable",
                        "The submission handle is unknown or its temporary prepared payload has expired.");
                }
            }

            var dispatchResult = await _dispatcher.DispatchAsync(
                submission.Payload,
                messageHandling,
                cancellationToken);

            if (!dispatchResult.IsAccepted)
            {
                _store.ReleaseForRetry(request.SubmissionHandle);
                return CreateFailure(
                    dispatchResult.ErrorCode,
                    dispatchResult.ErrorMessage);
            }

            var receipt = new ErrorSubmissionReceipt
            {
                Dispatcher = submission.Payload.DispatcherName,
                ReportReference = dispatchResult.ReportReference,
                PayloadDigest = dispatchResult.PayloadDigest,
            };

            _store.Complete(request.SubmissionHandle, receipt);
            return CreateSuccess(submission with { Receipt = receipt });
        }
        catch (OperationCanceledException)
        {
            _store.ReleaseForRetry(request.SubmissionHandle);
            throw;
        }
        catch (Exception)
        {
            _store.ReleaseForRetry(request.SubmissionHandle);
            throw;
        }
    }

    private static async ValueTask<ConsentResult> RequestConsentAsync(
        IUserInteractionService interactionService,
        PreparedSubmission submission,
        CancellationToken cancellationToken)
    {
        var request = CreateInteractionRequest(submission);
        var result = await interactionService.RequestAsync(request, cancellationToken);
        if (!result.IsAccepted)
        {
            return result.Outcome switch
            {
                UserInteractionOutcome.Declined or UserInteractionOutcome.Cancelled => CreateNotApprovedResult(),
                UserInteractionOutcome.InvalidResponse => CreateNotApprovedResult(),
                UserInteractionOutcome.Unavailable or UserInteractionOutcome.Failed => CreateUnavailableResult(),
                _ => throw new InvalidOperationException("The interaction service returned an unsupported outcome."),
            };
        }

        switch (result.SelectedValue)
        {
            case _send:
                return new ConsentResult();

            case _sendWithoutExceptionMessages:
                return new ConsentResult
                {
                    MessageHandling = ExceptionMessageHandling.Remove,
                };

            case _doNotSend:
                return CreateNotApprovedResult();

            default:
                return CreateInvalidResponseResult();
        }
    }

    private static ConsentResult CreateUnavailableResult()
    {
        return new ConsentResult
        {
            Failure = CreateFailure(
                "ApprovalUnavailable",
                "The connected MCP client could not complete the required consent elicitation."),
        };
    }

    private static ConsentResult CreateInvalidResponseResult()
    {
        return new ConsentResult
        {
            Failure = CreateFailure(
                "InvalidApprovalResponse",
                "The client returned an unsupported consent choice; nothing was submitted."),
        };
    }

    private static ConsentResult CreateNotApprovedResult()
    {
        return new ConsentResult
        {
            Failure = CreateFailure("ErrorReportNotApproved", _notApprovedMessage),
            DiscardSubmission = true,
        };
    }

    private static UserInteractionRequest CreateInteractionRequest(PreparedSubmission submission)
    {
        return new UserInteractionRequest
        {
            Message = $"Send this error report to {submission.Payload.Destination}?",
            Title = "Error report consent",
            Description = "Choose whether to submit the reviewed error report.",
            Choices = _choices,
        };
    }

    private static ReadOnlyCollection<UserInteractionChoice> CreateChoices()
    {
        var choices = new UserInteractionChoice[]
        {
            new UserInteractionChoice
            {
                Value = _send,
                Title = "Yes, send it",
            },
            new UserInteractionChoice
            {
                Value = _sendWithoutExceptionMessages,
                Title = "Yes, without exception messages",
            },
            new UserInteractionChoice
            {
                Value = _doNotSend,
                Title = "No, don't send it",
            },
        };

        return Array.AsReadOnly(choices);
    }

    private static ToolResult<SubmittedErrorReportData> CreateSuccess(PreparedSubmission? submission)
    {
        var receipt = submission?.Receipt;
        if (receipt is null)
        {
            throw new InvalidOperationException("A sent error report must have a submission receipt.");
        }

        var data = new SubmittedErrorReportData
        {
            Dispatcher = receipt.Dispatcher,
            ReportReference = receipt.ReportReference,
            PayloadDigest = receipt.PayloadDigest,
        };

        return ToolResult.Succeeded(data);
    }

    private static ToolResult<SubmittedErrorReportData> CreateFailure(string code, string message)
    {
        var error = new ToolError
        {
            Code = code,
            Message = message,
        };

        return ToolResult.Rejected<SubmittedErrorReportData>(error);
    }

    private sealed record ConsentResult
    {
        public ToolResult<SubmittedErrorReportData>? Failure { get; init; }

        public ExceptionMessageHandling MessageHandling { get; init; } = ExceptionMessageHandling.Include;

        public bool DiscardSubmission { get; init; }
    }
}
