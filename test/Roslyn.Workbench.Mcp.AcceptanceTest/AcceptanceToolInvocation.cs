using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal sealed record AcceptanceToolInvocation
{
    public RequestId RequestId { get; }

    public Task<CallToolResult> Completion { get; }

    public AcceptanceToolInvocation(RequestId requestId, Task<CallToolResult> completion)
    {
        RequestId = requestId;
        Completion = completion;
    }
}
