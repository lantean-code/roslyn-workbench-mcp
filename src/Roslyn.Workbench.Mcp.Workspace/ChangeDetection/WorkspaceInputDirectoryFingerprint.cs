namespace Roslyn.Workbench.Mcp.Workspace.ChangeDetection;

/// <summary>
/// Records a directory whose existence contributes to the certified Workspace input set.
/// </summary>
internal sealed record WorkspaceInputDirectoryFingerprint
{
    /// <summary>
    /// Gets the normalized absolute directory path.
    /// </summary>
    public string Path { get; init; } = string.Empty;
}
