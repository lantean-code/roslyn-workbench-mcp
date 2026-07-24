namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.StateSequences;

internal sealed record WorkspaceExternalChangeMeasurement
{
    public required string DetectionSource { get; init; }

    public string? ErrorCode { get; init; }

    public required string Kind { get; init; }

    public string? Path { get; init; }

    public string? PreviousPath { get; init; }
}
