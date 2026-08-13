using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sentry;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

internal sealed class SentryErrorReportDispatcher : IErrorReportDispatcher
{
    private const string _platform = "csharp";
    private const string _logger = "roslyn-workbench-mcp";
    private const string _messageTemplate = "Roslyn Workbench reported {0} in {1}.";
    private static readonly CompositeFormat _messageFormat = CompositeFormat.Parse(_messageTemplate);

    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ISentryClient _client;
    private readonly SentryProviderConfiguration _configuration;

    public SentryErrorReportDispatcher(
        ISentryClient client,
        SentryProviderConfiguration configuration)
    {
        _client = client;
        _configuration = configuration;
    }

    public string Name => "Sentry";

    public string Destination => _configuration.Destination;

    public PreparedDispatchPayload CreatePayload(ExternalErrorReport report)
    {
        var sentryEvent = CreateSentryEvent(report);
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

    public ValueTask<ErrorDispatchResult> DispatchAsync(
        PreparedDispatchPayload payload,
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

        var sentryEvent = SentryEventAllowList.CreateAllowedCopy(preparedPayload.DispatchState);

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
        });
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
        var fingerprint = ImmutableArray.CreateBuilder<string>(4 + report.StackFrames.Length);
        fingerprint.Add("roslyn-workbench");
        fingerprint.Add(report.Tool);
        fingerprint.Add(report.ExceptionClassification);
        fingerprint.Add(report.ExecutionFamily);
        foreach (var frame in report.StackFrames)
        {
            fingerprint.Add(frame.Component);
        }

        return fingerprint.ToImmutable();
    }
}
