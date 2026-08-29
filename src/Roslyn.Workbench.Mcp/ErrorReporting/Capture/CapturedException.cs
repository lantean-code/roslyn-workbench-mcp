using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed record CapturedException
{
    /// <summary>
    /// Gets the Component.
    /// </summary>
    [Description("Roslyn Workbench component in which the exception originated.")]
    public ErrorReportComponent Component { get; init; }

    /// <summary>
    /// Gets the Type.
    /// </summary>
    [Description("Exception type name.")]
    public required string Type { get; init; }

    /// <summary>
    /// Gets the Message.
    /// </summary>
    [Description("Exception message included with the user's consent.")]
    public required string Message { get; init; }

    /// <summary>
    /// Gets the Stack Frames.
    /// </summary>
    [Description("First-party stack frames retained for diagnosis.")]
    public ImmutableArray<CapturedStackFrame> StackFrames { get; init; } = [];
}
