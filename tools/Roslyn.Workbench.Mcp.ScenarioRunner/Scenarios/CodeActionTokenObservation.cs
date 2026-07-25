namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

internal sealed record CodeActionTokenObservation
{
    public required int Count { get; init; }

    public required int MaximumBytes { get; init; }

    public required long TotalBytes { get; init; }
}
