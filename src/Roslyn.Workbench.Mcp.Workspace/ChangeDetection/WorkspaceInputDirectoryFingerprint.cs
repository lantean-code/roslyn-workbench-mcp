namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

internal sealed record WorkspaceInputDirectoryFingerprint
{
    public string Path { get; init; } = string.Empty;
}
