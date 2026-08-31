using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Operations;

/// <summary>
/// Carries an unexpected workspace failure together with the context captured at the operation boundary.
/// </summary>
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

    /// <summary>
    /// Gets the workspace state captured when the exception crossed the operation boundary.
    /// </summary>
    public WorkspaceFailureContext Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceOperationException"/> class.
    /// </summary>
    /// <param name="context">The workspace state captured for diagnostics.</param>
    /// <param name="innerException">The underlying exception that caused this operation to fail.</param>
    public WorkspaceOperationException(
        WorkspaceFailureContext context,
        Exception innerException)
        : base(_message, innerException)
    {
        Context = context;
    }
}
