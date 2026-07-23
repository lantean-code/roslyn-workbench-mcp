namespace Roslyn.Workbench.Mcp.ScenarioRunner.Repositories;

internal sealed record ExternalCommandResult
{
    public required int ExitCode { get; init; }

    public required string StandardOutput { get; init; }

    public required string StandardError { get; init; }
}
