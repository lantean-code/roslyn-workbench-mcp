using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Dispatch;

internal sealed partial class LoggingErrorReportDispatcher : IErrorReportDispatcher
{
    private const string _destination = "standard error (stderr)";
    private const string _level = "error";
    private const string _logger = "roslyn-workbench-mcp";

    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ILogger<LoggingErrorReportDispatcher> _loggerInstance;

    public string Name => "Logging";

    public string Destination => _destination;

    public LoggingErrorReportDispatcher(ILogger<LoggingErrorReportDispatcher> logger)
    {
        _loggerInstance = logger;
    }

    public PreparedDispatchPayload CreatePayload(ExternalErrorReport report)
    {
        var loggingPayload = CreateLoggingPayload(report);
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

    public ValueTask<ErrorDispatchResult> DispatchAsync(
        PreparedDispatchPayload payload,
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

        LogApprovedErrorReport(_loggerInstance, report.ReportId, preparedPayload.DispatchState);

        return ValueTask.FromResult(new ErrorDispatchResult
        {
            Outcome = ErrorDispatchOutcome.Accepted,
            ReportReference = report.ReportId,
        });
    }

    private static LoggingPayload CreateLoggingPayload(ExternalErrorReport report)
    {
        return new LoggingPayload
        {
            Level = _level,
            Logger = _logger,
            Message = $"Roslyn Workbench reported {report.ExceptionClassification} in {report.Tool}.",
            Report = report,
        };
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
