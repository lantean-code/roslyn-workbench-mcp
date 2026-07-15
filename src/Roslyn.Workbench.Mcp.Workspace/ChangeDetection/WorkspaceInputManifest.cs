namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed record WorkspaceInputManifest
{
    public IReadOnlyList<WorkspaceInputDirectoryFingerprint> Directories { get; init; } = [];

    public IReadOnlyList<WorkspaceProjectInputFailure> EvaluationFailures { get; init; } = [];

    public IReadOnlyList<WorkspaceInputFileFingerprint> Files { get; init; } = [];

    public bool IsComplete => EvaluationFailures.Count == 0;
}
