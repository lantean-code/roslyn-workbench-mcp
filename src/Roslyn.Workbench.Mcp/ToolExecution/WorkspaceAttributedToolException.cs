using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ToolExecution;

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "This internal transport marker requires both the original failure and an immutable execution-time workspace context; general-purpose constructors would permit invalid marker instances.")]
internal sealed class WorkspaceAttributedToolException : Exception
{
    private const string _message = "A workspace-scoped tool execution failed.";

    public CapturedWorkspaceContext WorkspaceContext { get; }

    public WorkspaceAttributedToolException(
        CapturedWorkspaceContext workspaceContext,
        Exception innerException)
        : base(_message, innerException)
    {
        WorkspaceContext = workspaceContext;
    }
}
