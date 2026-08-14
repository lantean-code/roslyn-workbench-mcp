using System.Diagnostics.CodeAnalysis;

using ModelContextProtocol;

namespace Roslyn.Workbench.Mcp.Hosting;

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "This internal marker represents an intentional Host protocol failure and requires an explicit MCP error code; general-purpose constructors would permit invalid marker instances.")]
internal sealed class RoslynWorkbenchMcpProtocolException : McpProtocolException
{
    public RoslynWorkbenchMcpProtocolException(
        string message,
        McpErrorCode errorCode)
        : base(message, errorCode)
    {
    }
}
