using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Tools;

internal sealed class GetErrorDetailsTool :
    ServerOwnedToolBase<GetErrorDetailsRequest, ErrorDetailsData>
{
    private readonly ICapturedErrorStore _store;

    public GetErrorDetailsTool(
        IOptions<StartupOptions> startupOptions,
        IMcpToolProtocolFactory protocolFactory,
        ICapturedErrorStore store)
        : base(
            startupOptions,
            protocolFactory,
            ServerOwnedToolRegistration.GetErrorDetailsName,
            "Get Error Details",
            "Returns temporary local diagnostic details for an unexpected tool failure. The result may contain paths and user-authored identifiers, is intended only for the trusted local agent, and must never be submitted externally.",
            readOnly: true,
            destructive: false)
    {
        _store = store;
    }

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
