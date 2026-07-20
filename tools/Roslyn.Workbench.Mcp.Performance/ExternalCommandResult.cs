namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record ExternalCommandResult
{
    public required int ExitCode { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }
}
