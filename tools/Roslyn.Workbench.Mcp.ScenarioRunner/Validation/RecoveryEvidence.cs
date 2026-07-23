namespace Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

internal sealed record RecoveryEvidence
{
    public string? State { get; init; }

    public int ArtifactCount { get; init; }
}
