namespace Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

internal sealed record RecoveryEvidence
{
    public RecoveryEvidenceState? State { get; init; }

    public int ArtifactCount { get; init; }
}

internal enum RecoveryEvidenceState
{
    Prepared = 0,
    Applying = 1,
    Committed = 2,
    Restored = 3,
    RecoveryConflict = 4,
    RecoveryIncomplete = 5,
}
