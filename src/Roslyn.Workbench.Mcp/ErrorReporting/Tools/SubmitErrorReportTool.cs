using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Tools;

/// <summary>
/// Obtains configured consent and dispatches one previously reviewed payload at most once.
/// </summary>
internal sealed class SubmitErrorReportTool :
    ServerOwnedToolBase<SubmitErrorReportRequest, SubmittedErrorReportData>
{
    private const string _choiceProperty = "choice";
    private const string _send = "send";
    private const string _sendWithoutExceptionMessages = "send-without-exception-messages";
    private const string _doNotSend = "do-not-send";
    private const string _notApprovedMessage = "No error report was sent. If no consent prompt was displayed, the client may have blocked MCP elicitation. Enable manual MCP approvals, prepare a new report, and try again. If you selected 'No', no further action is required.";

    private readonly IPreparedSubmissionStore _store;
    private readonly IErrorReportingConsentService _consentService;
    private readonly IErrorReportDispatcher _dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitErrorReportTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="store">The store containing prepared submissions awaiting approval.</param>
    /// <param name="consentService">The service that determines whether submission is disabled, prompted or pre-approved.</param>
    /// <param name="dispatcher">The dispatcher that sends the approved error-report payload.</param>
    public SubmitErrorReportTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        IPreparedSubmissionStore store,
        IErrorReportingConsentService consentService,
        IErrorReportDispatcher dispatcher)
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
        var result = await ExecuteWithContextAsync(request, requestContext.Server, cancellationToken);
        var content = result.Outcome.IsError()
            ? ToolResultEnvelopeSerializer.CreateFailure(result.Error, result.RequiredAction)
            : ToolResultEnvelopeSerializer.CreateSuccess(result.Data);

        return CreateStructuredResult(content, result.Outcome.IsError());
    }

    private async ValueTask<ToolResult<SubmittedErrorReportData>> ExecuteWithContextAsync(
        SubmitErrorReportRequest request,
        McpServer server,
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
                    server,
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
            if (dispatchResult.Outcome != ErrorDispatchOutcome.Accepted)
            {
                _store.ReleaseForRetry(request.SubmissionHandle);
                return CreateFailure(
                    dispatchResult.ErrorCode ?? "ErrorReportDispatchFailed",
                    dispatchResult.ErrorMessage ?? "The error report could not be submitted.");
            }

            var digest = dispatchResult.PayloadDigest
                ?? throw new InvalidOperationException("An accepted error-report dispatch must include its payload digest.");
            var receipt = new ErrorSubmissionReceipt
            {
                Dispatcher = submission.Payload.DispatcherName,
                ReportReference = dispatchResult.ReportReference ?? submission.Payload.ReportId,
                PayloadDigest = digest,
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
        McpServer server,
        PreparedSubmission submission,
        CancellationToken cancellationToken)
    {
        if (server.ClientCapabilities?.Elicitation is null)
        {
            return new ConsentResult
            {
                Failure = CreateFailure(
                    "ApprovalUnavailable",
                    "The connected MCP client does not advertise elicitation support, so user approval cannot be obtained."),
            };
        }

        ElicitResult result;
        try
        {
            result = await server.ElicitAsync(CreateElicitation(submission), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return new ConsentResult
            {
                Failure = CreateFailure(
                    "ApprovalUnavailable",
                    "The connected MCP client cannot perform the required consent elicitation."),
            };
        }
        catch (McpException)
        {
            return new ConsentResult
            {
                Failure = CreateFailure(
                    "ApprovalUnavailable",
                    "The MCP client failed to complete the required consent elicitation."),
            };
        }

        if (string.Equals(result.Action, "decline", StringComparison.Ordinal))
        {
            return CreateNotApprovedResult();
        }

        if (!result.IsAccepted
            || result.Content is null
            || !result.Content.TryGetValue(_choiceProperty, out var choiceElement)
            || choiceElement.ValueKind != JsonValueKind.String)
        {
            return CreateNotApprovedResult();
        }

        var choice = choiceElement.GetString();
        switch (choice)
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
                return new ConsentResult
                {
                    Failure = CreateFailure(
                        "InvalidApprovalResponse",
                        "The client returned an unsupported consent choice; nothing was submitted."),
                };
        }
    }

    private static ConsentResult CreateNotApprovedResult()
    {
        return new ConsentResult
        {
            Failure = CreateFailure("ErrorReportNotApproved", _notApprovedMessage),
            DiscardSubmission = true,
        };
    }

    private static ElicitRequestParams CreateElicitation(PreparedSubmission submission)
    {
        var choices = new List<ElicitRequestParams.EnumSchemaOption>
        {
            new ElicitRequestParams.EnumSchemaOption
            {
                Const = _send,
                Title = "Yes, send it",
            },
            new ElicitRequestParams.EnumSchemaOption
            {
                Const = _sendWithoutExceptionMessages,
                Title = "Yes, without exception messages",
            },
            new ElicitRequestParams.EnumSchemaOption
            {
                Const = _doNotSend,
                Title = "No, don't send it",
            },
        };

        var choice = new ElicitRequestParams.TitledSingleSelectEnumSchema
        {
            Title = "Error report consent",
            Description = "Choose whether to submit the reviewed error report.",
            OneOf = choices,
        };

        return new ElicitRequestParams
        {
            Message = $"Send this error report to {submission.Payload.Destination}?",
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>(
                    StringComparer.Ordinal)
                {
                    [_choiceProperty] = choice,
                },
                Required = [_choiceProperty],
            },
        };
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
