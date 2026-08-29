using System.Collections.Immutable;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

internal sealed record ExternalException
{
    public required string Type { get; init; }

    public string? Message { get; init; }

    public ImmutableArray<ExternalStackFrame> StackFrames { get; init; } = [];
}
