namespace Roslyn.Workbench.Mcp.Protocol.Results;

internal enum ToolContinuationKind
{
    CallTool,
    ChooseTool,
    RetryRequest,
    ReviseRequest,
    ResolveExternally,
}
