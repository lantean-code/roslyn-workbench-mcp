namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed record WorkspaceInputFileFingerprint
{
    public string Path { get; init; } = string.Empty;

    public DateTime LastWriteTimeUtc { get; init; }

    public long Length { get; init; }

}
