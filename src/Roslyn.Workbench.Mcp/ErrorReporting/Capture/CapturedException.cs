using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

/// <summary>
/// Captures one exception in a failure chain together with diagnostic stack frames classified by owning component.
/// </summary>
internal sealed record CapturedException
{
    /// <summary>
    /// Roslyn Workbench component in which the exception originated.
    /// </summary>
    [Description("Roslyn Workbench component in which the exception originated.")]
    public ErrorReportComponent Component { get; init; }

    /// <summary>
    /// Exception type name.
    /// </summary>
    [Description("Exception type name.")]
    public required string Type { get; init; }

    /// <summary>
    /// Exception message included with the user's consent.
    /// </summary>
    [Description("Exception message included with the user's consent.")]
    public required string Message { get; init; }

    /// <summary>
    /// Diagnostic stack frames retained from the exception and classified by owning component.
    /// </summary>
    [Description("Diagnostic stack frames retained from the exception and classified by owning component.")]
    public ImmutableArray<CapturedStackFrame> StackFrames { get; init; } = [];
}
