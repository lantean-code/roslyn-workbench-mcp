using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed record CapturedException
{
    public ErrorReportComponent Component { get; init; }

    public required string Type { get; init; }

    public required string Message { get; init; }

    public ImmutableArray<CapturedStackFrame> StackFrames { get; init; } = [];
}
