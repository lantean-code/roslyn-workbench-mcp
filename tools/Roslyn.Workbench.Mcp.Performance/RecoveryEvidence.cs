namespace Roslyn.Workbench.Mcp.Performance;

internal sealed record RecoveryEvidence
{
    public string? State { get; init; }

    public int ArtifactCount { get; init; }
}
