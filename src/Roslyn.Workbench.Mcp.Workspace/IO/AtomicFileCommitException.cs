using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.IO;

/// <summary>
/// Reports a native atomic replacement failure and whether retrying may succeed.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "This internal exception requires an explicit retry classification and is created only at the native commit boundary.")]
internal sealed class AtomicFileCommitException : IOException
{
    /// <summary>
    /// Gets a value indicating whether the failed commit may be retried.
    /// </summary>
    public bool IsRetryable { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AtomicFileCommitException"/> class.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <param name="isRetryable">Whether retrying the failed atomic file operation may succeed.</param>
    /// <param name="innerException">The native failure.</param>
    public AtomicFileCommitException(
        string message,
        bool isRetryable,
        Exception innerException)
        : base(message, innerException)
    {
        IsRetryable = isRetryable;
    }
}
