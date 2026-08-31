using System.Diagnostics.CodeAnalysis;

using ModelContextProtocol;

namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Marks an intentional host rejection that must be returned as a specific MCP protocol error.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "This internal marker represents an intentional Host protocol failure and requires an explicit MCP error code; general-purpose constructors would permit invalid marker instances.")]
internal sealed class RoslynWorkbenchMcpProtocolException : McpProtocolException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RoslynWorkbenchMcpProtocolException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <param name="errorCode">The MCP protocol error code exposed by the exception.</param>
    public RoslynWorkbenchMcpProtocolException(
        string message,
        McpErrorCode errorCode)
        : base(message, errorCode)
    {
    }
}
