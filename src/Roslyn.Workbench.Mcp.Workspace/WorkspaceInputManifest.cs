namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceInputManifest
{
    public IReadOnlyList<WorkspaceInputDirectoryFingerprint> Directories { get; init; } = [];

    public IReadOnlyList<WorkspaceInputFileFingerprint> Files { get; init; } = [];
}
