namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal sealed record CapturedStackFrame
{
    public string? Assembly { get; init; }

    public string? Type { get; init; }

    public string? Method { get; init; }

    public string? File { get; init; }

    public int? Line { get; init; }
}
