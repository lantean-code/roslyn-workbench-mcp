using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Operations;

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "This internal transport exception requires an authoritative Workspace context and an original failure; general-purpose constructors would permit invalid instances.")]
[SuppressMessage(
    "Design",
    "CA1064:Exceptions should be public",
    Justification = "This exception is an internal transport between the Workspace implementation and its Host adapter and is not part of the public API.")]
internal sealed class WorkspaceOperationException : Exception
{
    private const string _message = "A Workspace operation failed after resolving its target.";

    public WorkspaceFailureContext Context { get; }

    public WorkspaceOperationException(
        WorkspaceFailureContext context,
        Exception innerException)
        : base(_message, innerException)
    {
        Context = context;
    }
}
