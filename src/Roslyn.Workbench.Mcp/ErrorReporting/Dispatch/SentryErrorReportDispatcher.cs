using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sentry;
using Sentry.Protocol;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Converts approved error reports into allow-listed Sentry events and submits them through an isolated client.
/// </summary>
internal sealed class SentryErrorReportDispatcher : IErrorReportDispatcher
{
    private const string _platform = "csharp";
    private const string _logger = "roslyn-workbench-mcp";
    private const string _messageTemplate = "Roslyn Workbench reported {0} in {1}.";
    private static readonly CompositeFormat _messageFormat = CompositeFormat.Parse(_messageTemplate);

    private static readonly JsonSerializerOptions _serializerOptions = CreateSerializerOptions();

    private readonly ISentryClient _client;
    private readonly SentryProviderConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="SentryErrorReportDispatcher"/> class.
    /// </summary>
    /// <param name="client">The Sentry client used to submit approved error reports.</param>
    /// <param name="configuration">The validated Sentry endpoint and user-facing destination.</param>
    public SentryErrorReportDispatcher(
        ISentryClient client,
        SentryProviderConfiguration configuration)
    {
        _client = client;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the provider name shown during review and in submission results.
    /// </summary>
    public string Name => "Sentry";

    /// <summary>
    /// Gets the Sentry project shown to the user before approval.
    /// </summary>
    public string Destination => _configuration.Destination;

    /// <summary>
    /// Creates the allow-listed Sentry event presented for review.
    /// </summary>
    /// <param name="report">The projected error report to encode as a Sentry event.</param>
    /// <returns>The event, UTF-8 JSON preview and provider dispatch state.</returns>
    public PreparedDispatchPayload CreatePayload(ExternalErrorReport report)
    {
        var sentryEvent = CreateSentryEvent(report);
        return CreatePayload(report, sentryEvent);
    }

    private PreparedDispatchPayload<SentryEvent> CreatePayload(ExternalErrorReport report, SentryEvent sentryEvent)
    {
        var allowedEvent = SentryEventAllowList.CreateAllowedCopy(sentryEvent);
        var bytes = SentryEventJsonSerializer.Serialize(allowedEvent);
        var previewJson = Encoding.UTF8.GetString(bytes.AsSpan());

        return new PreparedDispatchPayload<SentryEvent>
        {
            DispatcherName = Name,
            Destination = Destination,
            ReportId = report.ReportId,
            Report = report,
            PreviewBytes = bytes,
            PreviewJson = previewJson,
            DispatchState = allowedEvent,
        };
    }

    /// <summary>
    /// Applies the approved exception-message policy and sends the prepared event to Sentry.
    /// </summary>
    /// <param name="payload">The prepared payload approved for dispatch.</param>
    /// <param name="messageHandling">Whether captured exception messages are included or removed before dispatch.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the submission outcome or a validation failure.</returns>
    public ValueTask<ErrorDispatchResult> DispatchAsync(
        PreparedDispatchPayload payload,
        ExceptionMessageHandling messageHandling,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var report = payload.Report;
        if (payload is not PreparedDispatchPayload<SentryEvent> preparedPayload
            || !string.Equals(report.ReportId, payload.ReportId, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Rejected,
                ErrorCode = "InvalidPreparedErrorReport",
                ErrorMessage = "The immutable error report identifier does not match its prepared submission.",
            });
        }

        var dispatchPayload = messageHandling switch
        {
            ExceptionMessageHandling.Include => preparedPayload,
            ExceptionMessageHandling.Remove => RemoveExceptionMessages(preparedPayload),
            _ => null,
        };
        if (dispatchPayload is null)
        {
            return ValueTask.FromResult(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Rejected,
                ErrorCode = "InvalidExceptionMessageHandling",
                ErrorMessage = "The requested exception-message handling mode is not supported.",
            });
        }

        var sentryEvent = SentryEventAllowList.CreateAllowedCopy(dispatchPayload.DispatchState);

        var eventId = _client.CaptureEvent(sentryEvent, scope: null, hint: null);
        if (eventId == SentryId.Empty)
        {
            return ValueTask.FromResult(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Rejected,
                ErrorCode = "SentryCaptureRejected",
                ErrorMessage = "The Sentry SDK did not accept the prepared error report.",
            });
        }

        return ValueTask.FromResult(new ErrorDispatchResult
        {
            Outcome = ErrorDispatchOutcome.Accepted,
            ReportReference = eventId.ToString(),
            PayloadDigest = Convert.ToHexStringLower(
                SHA256.HashData(dispatchPayload.PreviewBytes.AsSpan())),
        });
    }

    private PreparedDispatchPayload<SentryEvent> RemoveExceptionMessages(
        PreparedDispatchPayload<SentryEvent> preparedPayload)
    {
        var redactedReport = ExternalErrorReportRedactor.RemoveExceptionMessages(preparedPayload.Report);
        var redactedEvent = SentryEventAllowList.CreateAllowedCopy(preparedPayload.DispatchState);
        redactedEvent.SentryExceptions = CreateSentryExceptions(redactedReport);
        redactedEvent.Contexts["roslyn_workbench"] = JsonSerializer.SerializeToElement(
            redactedReport,
            _serializerOptions);

        return CreatePayload(redactedReport, redactedEvent);
    }

    private static SentryEvent CreateSentryEvent(ExternalErrorReport report)
    {
        var messageParams = CreateMessageParams(report);
        var sentryEvent = new SentryEvent
        {
            Platform = _platform,
            Level = SentryLevel.Error,
            Logger = _logger,
            Fingerprint = CreateFingerprint(report),
            Message = new SentryMessage
            {
                Message = _messageTemplate,
                Params = messageParams,
                Formatted = CreateFormattedMessage(messageParams),
            },
            SentryExceptions = CreateSentryExceptions(report),
        };
        sentryEvent.Contexts["roslyn_workbench"] = JsonSerializer.SerializeToElement(report, _serializerOptions);
        return sentryEvent;
    }

    private static ImmutableArray<object> CreateMessageParams(ExternalErrorReport report)
    {
        return [report.ExceptionClassification, report.Tool];
    }

    private static string CreateFormattedMessage(ImmutableArray<object> messageParams)
    {
        return string.Format(CultureInfo.InvariantCulture, _messageFormat, messageParams.AsSpan());
    }

    private static ImmutableArray<string> CreateFingerprint(ExternalErrorReport report)
    {
        var fingerprint = ImmutableArray.CreateBuilder<string>();
        fingerprint.Add("roslyn-workbench");
        fingerprint.Add(report.Tool);
        fingerprint.Add(report.ExceptionClassification);
        fingerprint.Add(report.ExecutionFamily);
        foreach (var exception in report.Exceptions)
        {
            foreach (var frame in exception.StackFrames)
            {
                fingerprint.Add(frame.Component.ToString());
            }
        }

        return fingerprint.ToImmutable();
    }

    private static List<SentryException> CreateSentryExceptions(ExternalErrorReport report)
    {
        var exceptions = new List<SentryException>(report.Exceptions.Length);
        for (var index = report.Exceptions.Length - 1; index >= 0; index--)
        {
            var exception = report.Exceptions[index];
            exceptions.Add(new SentryException
            {
                Type = exception.Type,
                Value = exception.Message,
                Stacktrace = CreateSentryStackTrace(exception.StackFrames),
            });
        }

        return exceptions;
    }

    private static SentryStackTrace? CreateSentryStackTrace(ImmutableArray<ExternalStackFrame> frames)
    {
        if (frames.IsDefaultOrEmpty)
        {
            return null;
        }

        var sentryFrames = new List<SentryStackFrame>(frames.Length);
        for (var index = frames.Length - 1; index >= 0; index--)
        {
            var frame = frames[index];
            sentryFrames.Add(new SentryStackFrame
            {
                Package = frame.Assembly,
                Module = frame.Type,
                Function = frame.Method,
                FileName = frame.File,
                LineNumber = frame.Line,
                InApp = frame.Component == ErrorReportComponent.RoslynWorkbench,
            });
        }

        return new SentryStackTrace
        {
            Frames = sentryFrames,
        };
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));
        return options;
    }
}
