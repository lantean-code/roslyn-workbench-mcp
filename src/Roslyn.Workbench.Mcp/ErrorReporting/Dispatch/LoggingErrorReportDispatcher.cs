using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

/// <summary>
/// Prepares error reports as structured JSON and writes approved submissions to standard error.
/// </summary>
internal sealed partial class LoggingErrorReportDispatcher : IErrorReportDispatcher
{
    private const string _destination = "standard error (stderr)";
    private const string _level = "error";
    private const string _logger = "roslyn-workbench-mcp";

    private static readonly JsonSerializerOptions _serializerOptions = CreateSerializerOptions();

    private readonly ILogger<LoggingErrorReportDispatcher> _loggerInstance;

    /// <summary>
    /// Gets the provider name shown during review and in submission results.
    /// </summary>
    public string Name => "Logging";

    /// <summary>
    /// Gets the standard-error destination shown before approval.
    /// </summary>
    public string Destination => _destination;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingErrorReportDispatcher"/> class.
    /// </summary>
    /// <param name="logger">The logger used to record diagnostic information.</param>
    public LoggingErrorReportDispatcher(ILogger<LoggingErrorReportDispatcher> logger)
    {
        _loggerInstance = logger;
    }

    /// <summary>
    /// Creates the structured log entry presented for review.
    /// </summary>
    /// <param name="report">The projected error report to encode as a log entry.</param>
    /// <returns>The UTF-8 JSON preview and dispatch state for the log entry.</returns>
    public PreparedDispatchPayload CreatePayload(ExternalErrorReport report)
    {
        return CreateLoggingPayload(report);
    }

    private PreparedDispatchPayload<string> CreateLoggingPayload(ExternalErrorReport report)
    {
        var loggingPayload = CreateLogEntry(report);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(loggingPayload, _serializerOptions);
        var previewJson = Encoding.UTF8.GetString(bytes);

        return new PreparedDispatchPayload<string>
        {
            DispatcherName = Name,
            Destination = Destination,
            ReportId = report.ReportId,
            Report = report,
            PreviewBytes = bytes.ToImmutableArray(),
            PreviewJson = previewJson,
            DispatchState = previewJson,
        };
    }

    /// <summary>
    /// Applies the approved exception-message policy and writes the prepared error report to the application log.
    /// </summary>
    /// <param name="payload">The prepared payload approved for dispatch.</param>
    /// <param name="messageHandling">Whether captured exception messages are included or removed before dispatch.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the logging outcome or a validation failure.</returns>
    public ValueTask<ErrorDispatchResult> DispatchAsync(
        PreparedDispatchPayload payload,
        ExceptionMessageHandling messageHandling,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var report = payload.Report;
        if (payload is not PreparedDispatchPayload<string> preparedPayload
            || !string.Equals(report.ReportId, payload.ReportId, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(new ErrorDispatchResult
            {
                Outcome = ErrorDispatchOutcome.Rejected,
                ErrorCode = "InvalidPreparedErrorReport",
                ErrorMessage = "The immutable error report identifier does not match its prepared submission.",
            });
        }

        PreparedDispatchPayload<string>? dispatchPayload = messageHandling switch
        {
            ExceptionMessageHandling.Include => preparedPayload,
            ExceptionMessageHandling.Remove => CreateLoggingPayload(
                ExternalErrorReportRedactor.RemoveExceptionMessages(report)),
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

        LogApprovedErrorReport(_loggerInstance, report.ReportId, dispatchPayload.DispatchState);
        var digest = Convert.ToHexStringLower(SHA256.HashData(dispatchPayload.PreviewBytes.AsSpan()));

        return ValueTask.FromResult(new ErrorDispatchResult
        {
            Outcome = ErrorDispatchOutcome.Accepted,
            ReportReference = report.ReportId,
            PayloadDigest = digest,
        });
    }

    private static LoggingPayload CreateLogEntry(ExternalErrorReport report)
    {
        return new LoggingPayload
        {
            Level = _level,
            Logger = _logger,
            Message = $"Roslyn Workbench reported {report.ExceptionClassification} in {report.Tool}.",
            Report = report,
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

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "User-approved error report {ReportId}: {ErrorReport}")]
    private static partial void LogApprovedErrorReport(
        ILogger logger,
        string reportId,
        string errorReport);

    private sealed record LoggingPayload
    {
        public required string Level { get; init; }

        public required string Logger { get; init; }

        public required string Message { get; init; }

        public required ExternalErrorReport Report { get; init; }
    }
}
