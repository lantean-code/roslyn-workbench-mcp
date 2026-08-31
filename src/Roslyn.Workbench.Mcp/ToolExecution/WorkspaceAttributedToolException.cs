using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ToolExecution;

/// <summary>
/// Carries immutable workspace context alongside a tool failure as it crosses the MCP execution boundary.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "This internal transport marker requires both the original failure and an immutable execution-time workspace context; general-purpose constructors would permit invalid marker instances.")]
internal sealed class WorkspaceAttributedToolException : Exception
{
    private const string _message = "A workspace-scoped tool execution failed.";

    /// <summary>
    /// Gets the workspace identity and snapshot active when the failure occurred.
    /// </summary>
    public CapturedWorkspaceContext WorkspaceContext { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceAttributedToolException"/> class.
    /// </summary>
    /// <param name="workspaceContext">The workspace context in which the operation executes.</param>
    /// <param name="innerException">The underlying exception that caused this operation to fail.</param>
    public WorkspaceAttributedToolException(
        CapturedWorkspaceContext workspaceContext,
        Exception innerException)
        : base(_message, innerException)
    {
        WorkspaceContext = workspaceContext;
    }
}
