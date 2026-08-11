using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.IO;

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "This internal exception requires an explicit retry classification and is created only at the native commit boundary.")]
internal sealed class AtomicFileCommitException : IOException
{
    public bool IsRetryable { get; }

    public AtomicFileCommitException(
        string message,
        bool isRetryable,
        Exception innerException)
        : base(message, innerException)
    {
        IsRetryable = isRetryable;
    }
}
