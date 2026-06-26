namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceInputManifest
{
    public IReadOnlyList<WorkspaceInputFileFingerprint> Files { get; init; } = [];
}
