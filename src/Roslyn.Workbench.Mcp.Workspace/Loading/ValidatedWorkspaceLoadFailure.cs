namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Classifies failures produced while validating a loaded workspace.
/// </summary>
internal enum ValidatedWorkspaceLoadFailure
{
    /// <summary>
    /// Loading or project compatibility evaluation failed.
    /// </summary>
    LoadFailed,

    /// <summary>
    /// The loaded workspace contains no supported C# SDK-style projects.
    /// </summary>
    NotSupported,

    /// <summary>
    /// A loaded project lies outside the selected workspace root.
    /// </summary>
    OutsideWorkspaceRoot,
}
