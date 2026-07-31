namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

internal sealed record ExternalStackFrame
{
    public required string Component { get; init; }
}
