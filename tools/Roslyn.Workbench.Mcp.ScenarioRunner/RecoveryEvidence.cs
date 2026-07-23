namespace Roslyn.Workbench.Mcp.ScenarioRunner;

internal sealed record RecoveryEvidence
{
    public string? State { get; init; }

    public int ArtifactCount { get; init; }
}
