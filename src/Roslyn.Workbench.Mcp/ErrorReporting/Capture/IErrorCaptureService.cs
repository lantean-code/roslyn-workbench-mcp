using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

/// <summary>
/// Captures bounded diagnostic records from failed tool invocations and resource reads.
/// </summary>
internal interface IErrorCaptureService
{
    /// <summary>
    /// Captures a bounded resource-read failure without retaining the requested URI or unrelated workspace state.
    /// </summary>
    /// <param name="correlationId">The identifier returned to the client for this failure.</param>
    /// <param name="duration">The elapsed resource-read duration.</param>
    /// <param name="cancellationRequested">Whether cancellation was requested when the failure was captured.</param>
    /// <param name="exception">The unexpected resource-read exception.</param>
    /// <returns>The bounded process-local diagnostic record.</returns>
    CapturedErrorRecord CaptureResourceRead(
        Guid correlationId,
        TimeSpan duration,
        bool cancellationRequested,
        Exception exception);

    /// <summary>
    /// Captures a bounded diagnostic record for a failed tool invocation.
    /// </summary>
    /// <param name="correlationId">The identifier used to correlate the tool error with the retained record.</param>
    /// <param name="toolName">The published name of the tool associated with the captured error.</param>
    /// <param name="arguments">The arguments supplied to the tool invocation.</param>
    /// <param name="duration">The elapsed duration of the captured tool operation.</param>
    /// <param name="cancellationRequested">Whether cancellation had been requested when the error was captured.</param>
    /// <param name="workspaceContext">The workspace context already acquired by the invocation, when available.</param>
    /// <param name="exception">The unhandled exception raised by the tool invocation.</param>
    /// <returns>A size-bounded record containing diagnostic exception, environment and workspace metadata.</returns>
    CapturedErrorRecord Capture(
        Guid correlationId,
        string toolName,
        IDictionary<string, JsonElement>? arguments,
        TimeSpan duration,
        bool cancellationRequested,
        CapturedWorkspaceContext? workspaceContext,
        Exception exception);
}
