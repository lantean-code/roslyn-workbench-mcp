namespace Roslyn.Workbench.Mcp.ErrorReporting.Projection;

internal sealed record ExternalStackFrame
{
    public required ErrorReportComponent Component { get; init; }

    public string? Assembly { get; init; }

    public string? Type { get; init; }

    public string? Method { get; init; }

    public string? File { get; init; }

    public int? Line { get; init; }
}
