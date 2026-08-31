namespace Roslyn.Workbench.Mcp.Protocol.Results;

/// <summary>
/// Defines the supported tool continuation kind values.
/// </summary>
internal enum ToolContinuationKind
{
    /// <summary>
    /// Call one specified tool before continuing.
    /// </summary>
    CallTool,
    /// <summary>
    /// Choose one tool from a supplied set before continuing.
    /// </summary>
    ChooseTool,
    /// <summary>
    /// Retry the original request without changing it.
    /// </summary>
    RetryRequest,
    /// <summary>
    /// Change the original request as instructed before retrying it.
    /// </summary>
    ReviseRequest,
    /// <summary>
    /// Resolve a condition outside the available tool workflow.
    /// </summary>
    ResolveExternally,
}
