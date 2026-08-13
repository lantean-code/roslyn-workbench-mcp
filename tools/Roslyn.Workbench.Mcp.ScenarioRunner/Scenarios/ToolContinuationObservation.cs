namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios;

internal sealed record ToolContinuationObservation
{
    public required string Kind { get; init; }

    public string? Tool { get; init; }

    public IReadOnlyList<string>? Tools { get; init; }

    public required string Instruction { get; init; }
}
