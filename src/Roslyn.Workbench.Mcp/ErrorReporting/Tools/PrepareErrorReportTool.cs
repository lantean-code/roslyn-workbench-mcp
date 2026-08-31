using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Tools;

/// <summary>
/// Projects a captured failure into an immutable provider payload that the user can review before submission.
/// </summary>
internal sealed class PrepareErrorReportTool :
    ServerOwnedToolBase<PrepareErrorReportRequest, PreparedErrorReportData>
{
    private static readonly IReadOnlyList<string> _excludedCategories =
    [
        "dedicated source text and document content fields",
        "dedicated user-authored identifier and path fields",
        "dedicated repository, solution and project identity fields",
        "dedicated user, machine and stable installation identity fields",
        "dedicated environment variable and process command-line fields",
        "dedicated credential, token and secret fields",
        "dedicated agent prompt and conversation content fields",
        "dedicated raw log fields",
    ];

    private static readonly IReadOnlyList<string> _reviewWarnings =
    [
        "Exception messages are bounded but otherwise unfiltered reviewed content and may contain source text, paths, identifiers, credentials, tokens or secrets.",
    ];

    private readonly ErrorReportingOptions _options;
    private readonly ICapturedErrorStore _capturedErrorStore;
    private readonly IPreparedSubmissionStore _preparedSubmissionStore;
    private readonly IExternalErrorReportProjector _projector;
    private readonly IErrorReportDispatcher _dispatcher;
    private readonly IErrorReportingAvailabilityService _availabilityService;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrepareErrorReportTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="errorReportingOptions">The options that configure error reporting.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="capturedErrorStore">The store containing captured failures available for preparation.</param>
    /// <param name="preparedSubmissionStore">The store that retains immutable payloads awaiting approval.</param>
    /// <param name="projector">The component that creates the external error-report projection.</param>
    /// <param name="dispatcher">The dispatcher that prepares the immutable submission preview.</param>
    /// <param name="availabilityService">The service that determines whether preparation is currently permitted.</param>
    /// <param name="timeProvider">The time source used for expiry and timestamp calculations.</param>
    public PrepareErrorReportTool(
        IOptions<StartupOptions> startupOptions,
        IOptions<ErrorReportingOptions> errorReportingOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ICapturedErrorStore capturedErrorStore,
        IPreparedSubmissionStore preparedSubmissionStore,
        IExternalErrorReportProjector projector,
        IErrorReportDispatcher dispatcher,
        IErrorReportingAvailabilityService availabilityService,
        TimeProvider timeProvider)
        : base(
            startupOptions,
            protocolFactory,
            requestBinder,
            ServerOwnedToolRegistration.PrepareErrorReportName,
            "Prepare Error Report",
            "Creates the complete external payload without network activity. Exception messages may contain Workspace data. Present the returned destination, payload JSON string and digest to the user before calling submit-error-report when approval is required; the user can choose to remove exception messages during submission.",
            readOnly: true,
            destructive: false,
            idempotent: false)
    {
        _options = errorReportingOptions.Value;
        _capturedErrorStore = capturedErrorStore;
        _preparedSubmissionStore = preparedSubmissionStore;
        _projector = projector;
        _dispatcher = dispatcher;
        _availabilityService = availabilityService;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    protected override ValueTask<ToolResult<PreparedErrorReportData>> ExecuteAsync(
        PrepareErrorReportRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_capturedErrorStore.TryGet(request.CorrelationId, out var record))
        {
            return ValueTask.FromResult(CreateFailure(
                "ErrorDetailsUnavailable",
                "The correlation ID is unknown or its temporary diagnostic record has expired."));
        }

        var availability = _availabilityService.GetAvailability(
            record.Workspace?.WorkspaceId,
            record.Workspace?.WorkspaceEpoch,
            supportsElicitation: null);
        if (!availability.CanPrepare)
        {
            return ValueTask.FromResult(CreateFailure(
                "ErrorReportingUnavailable",
                $"Error reporting is unavailable because its current state is {availability.State}."));
        }

        var reportId = Guid.NewGuid().ToString("n");
        var externalReport = _projector.Project(record, reportId);
        var payload = _dispatcher.CreatePayload(externalReport);
        if (payload.PreviewBytes.Length > _options.MaximumPayloadBytes)
        {
            return ValueTask.FromResult(CreateFailure(
                "ErrorReportPayloadTooLarge",
                "The sanitised error report exceeds the configured payload limit."));
        }

        var now = _timeProvider.GetUtcNow();
        var handle = $"submission_{Guid.NewGuid():n}";
        var digest = Convert.ToHexStringLower(SHA256.HashData(payload.PreviewBytes.AsSpan()));
        var submission = new PreparedSubmission
        {
            Handle = handle,
            CorrelationId = record.CorrelationId,
            CreatedAt = now,
            ExpiresAt = now + _options.PreparedSubmissionLifetime,
            Payload = payload,
            WorkspaceId = record.Workspace?.WorkspaceId,
            WorkspaceEpoch = record.Workspace?.WorkspaceEpoch,
            State = PreparedSubmissionState.Prepared,
        };

        if (!_preparedSubmissionStore.TryAdd(submission))
        {
            return ValueTask.FromResult(CreateFailure(
                "ErrorReportCapacityReached",
                "The temporary prepared-report capacity is full; retry after an existing report expires or is discarded."));
        }

        var data = new PreparedErrorReportData
        {
            SubmissionHandle = handle,
            Dispatcher = payload.DispatcherName,
            Destination = payload.Destination,
            PayloadDigest = digest,
            ExpiresAt = submission.ExpiresAt,
            PayloadJson = payload.PreviewJson,
            ExcludedCategories = _excludedCategories,
            ReviewWarnings = _reviewWarnings,
        };

        return ValueTask.FromResult(ToolResult.Succeeded(data));
    }

    private static ToolResult<PreparedErrorReportData> CreateFailure(string code, string message)
    {
        var error = new ToolError
        {
            Code = code,
            Message = message,
        };

        return ToolResult.Rejected<PreparedErrorReportData>(error);
    }
}
