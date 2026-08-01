using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Tools;

internal sealed class SubmitErrorReportTool :
    ServerOwnedToolBase<SubmitErrorReportRequest, SubmittedErrorReportData>
{
    private const string _choiceProperty = "choice";
    private const string _submitOnce = "submit-once";
    private const string _allowWorkspace = "allow-workspace";
    private const string _allowSession = "allow-session";
    private const string _decline = "decline";
    private const string _suppressSession = "suppress-session";

    private readonly IPreparedSubmissionStore _store;
    private readonly IErrorReportingConsentService _consentService;
    private readonly IErrorReportDispatcher _dispatcher;

    public SubmitErrorReportTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IPreparedSubmissionStore store,
        IErrorReportingConsentService consentService,
        IErrorReportDispatcher dispatcher)
        : base(
            startupOptions,
            protocolFactory,
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

    protected override ValueTask<ToolResult<SubmittedErrorReportData>> ExecuteAsync(
        SubmitErrorReportRequest request,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Submission requires the active MCP request context.");
    }

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
        if (!_store.TryGet(request.SubmissionHandle, out var pending))
        {
            return CreateFailure(
                "PreparedReportUnavailable",
                "The submission handle is unknown or its temporary prepared payload has expired.");
        }

        var consentState = _consentService.GetState(pending.WorkspaceId, pending.WorkspaceEpoch);
        if (consentState == ErrorReportingConsentState.SuppressedForSession)
        {
            return CreateFailure(
                "ErrorReportingSuppressed",
                "Error reporting has been suppressed for this server session.");
        }

        if (consentState == ErrorReportingConsentState.PromptRequired)
        {
            var consentResult = await RequestConsentAsync(server, pending, cancellationToken);
            if (consentResult is not null)
            {
                return consentResult;
            }
        }

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
            var dispatchResult = await _dispatcher.DispatchAsync(
                submission.Payload,
                cancellationToken);
            if (dispatchResult.Outcome != ErrorDispatchOutcome.Accepted)
            {
                _store.ReleaseForRetry(request.SubmissionHandle);
                return CreateFailure(
                    dispatchResult.ErrorCode ?? "ErrorReportDispatchFailed",
                    dispatchResult.ErrorMessage ?? "The error report could not be submitted.");
            }

            var digest = Convert.ToHexStringLower(SHA256.HashData(submission.Payload.PreviewBytes.AsSpan()));
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

    private async ValueTask<ToolResult<SubmittedErrorReportData>?> RequestConsentAsync(
        McpServer server,
        PreparedSubmission submission,
        CancellationToken cancellationToken)
    {
        if (server.ClientCapabilities?.Elicitation is null)
        {
            return CreateFailure(
                "ApprovalUnavailable",
                "The connected MCP client does not advertise elicitation support, so user approval cannot be obtained.");
        }

        ElicitResult result;
        try
        {
            result = await server.ElicitAsync(CreateElicitation(submission), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return CreateFailure(
                "ApprovalUnavailable",
                "The connected MCP client cannot perform the required consent elicitation.");
        }
        catch (McpException)
        {
            return CreateFailure(
                "ApprovalUnavailable",
                "The MCP client failed to complete the required consent elicitation.");
        }

        if (string.Equals(result.Action, "decline", StringComparison.Ordinal))
        {
            _store.Discard(submission.Handle);
            return CreateFailure("ErrorReportDeclined", "The user declined the error report.");
        }

        if (!result.IsAccepted
            || result.Content is null
            || !result.Content.TryGetValue(_choiceProperty, out var choiceElement)
            || choiceElement.ValueKind != JsonValueKind.String)
        {
            return CreateFailure(
                "ErrorReportApprovalCancelled",
                "The approval request was cancelled; the prepared report remains available until it expires.");
        }

        var choice = choiceElement.GetString();
        switch (choice)
        {
            case _submitOnce:
                return null;

            case _allowWorkspace when submission.WorkspaceId is not null
                && submission.WorkspaceEpoch is not null:
                _consentService.AllowWorkspace(
                    submission.WorkspaceId.Value,
                    submission.WorkspaceEpoch.Value);
                return null;

            case _allowSession:
                _consentService.AllowSession();
                return null;

            case _decline:
                _store.Discard(submission.Handle);
                return CreateFailure("ErrorReportDeclined", "The user declined the error report.");

            case _suppressSession:
                _store.Discard(submission.Handle);
                _consentService.SuppressSession();
                return CreateFailure(
                    "ErrorReportingSuppressed",
                    "The user declined the report and suppressed error reporting for this server session.");

            default:
                return CreateFailure(
                    "InvalidApprovalResponse",
                    "The client returned an unsupported consent choice; nothing was submitted.");
        }
    }

    private static ElicitRequestParams CreateElicitation(PreparedSubmission submission)
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData(submission.Payload.PreviewBytes.AsSpan()));
        var choices = new List<ElicitRequestParams.EnumSchemaOption>
        {
            new()
            {
                Const = _submitOnce,
                Title = "Yes, submit this report",
            },
        };
        if (submission.WorkspaceId is not null && submission.WorkspaceEpoch is not null)
        {
            choices.Add(new ElicitRequestParams.EnumSchemaOption
            {
                Const = _allowWorkspace,
                Title = "Yes, allow for this workspace",
            });
        }

        choices.AddRange(
        [
            new ElicitRequestParams.EnumSchemaOption
            {
                Const = _allowSession,
                Title = "Yes, allow for this server session",
            },
            new ElicitRequestParams.EnumSchemaOption
            {
                Const = _decline,
                Title = "No",
            },
            new ElicitRequestParams.EnumSchemaOption
            {
                Const = _suppressSession,
                Title = "No, and don't ask again",
            },
        ]);

        var choice = new ElicitRequestParams.TitledSingleSelectEnumSchema
        {
            Title = "Error report consent",
            Description = "Choose whether to submit the reviewed error report.",
            OneOf = choices,
        };

        return new ElicitRequestParams
        {
            Message = $"Submit the reviewed error report to {submission.Payload.Destination}? Payload SHA-256: {digest}.",
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
}
