namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

internal sealed record CodeActionReferenceObservation
{
    public required int Count { get; init; }

    public required int MaximumBytes { get; init; }

    public required long TotalBytes { get; init; }
}
