using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Tools;

/// <summary>
/// Returns a locally retained captured error without projecting it for external submission.
/// </summary>
internal sealed class GetErrorDetailsTool :
    ServerOwnedToolBase<GetErrorDetailsRequest, ErrorDetailsData>
{
    private readonly ICapturedErrorStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetErrorDetailsTool"/> class.
    /// </summary>
    /// <param name="startupOptions">The options that control server startup.</param>
    /// <param name="protocolFactory">The factory that creates protocol result payloads.</param>
    /// <param name="requestBinder">The binder that converts tool arguments into request values.</param>
    /// <param name="store">The store containing captured errors available for inspection.</param>
    public GetErrorDetailsTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        IToolRequestBinder requestBinder,
        ICapturedErrorStore store)
        : base(
            startupOptions,
            protocolFactory,
            requestBinder,
            ServerOwnedToolRegistration.GetErrorDetailsName,
            "Get Error Details",
            "Returns temporary local diagnostic details for an unexpected tool failure. The result may contain paths and user-authored identifiers, is intended only for the trusted local agent, and must never be submitted externally.",
            readOnly: true,
            destructive: false)
    {
        _store = store;
    }

    /// <inheritdoc/>
    protected override ValueTask<ToolResult<ErrorDetailsData>> ExecuteAsync(
        GetErrorDetailsRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_store.TryGet(request.CorrelationId, out var record))
        {
            return ValueTask.FromResult(CreateFailure(
                "ErrorDetailsUnavailable",
                "The correlation ID is unknown or its temporary diagnostic record has expired."));
        }

        var data = new ErrorDetailsData
        {
            Error = record,
        };

        return ValueTask.FromResult(ToolResult.Succeeded(data));
    }

    private static ToolResult<ErrorDetailsData> CreateFailure(string code, string message)
    {
        var error = new ToolError
        {
            Code = code,
            Message = message,
        };

        return ToolResult.Rejected<ErrorDetailsData>(error);
    }
}
