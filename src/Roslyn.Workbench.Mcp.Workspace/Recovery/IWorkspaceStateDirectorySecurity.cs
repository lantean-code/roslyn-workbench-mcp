namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

/// <summary>
/// Enforces access and redirection safeguards for durable Workspace state.
/// </summary>
internal interface IWorkspaceStateDirectorySecurity
{
    /// <summary>
    /// Creates a directory when necessary and validates its security.
    /// </summary>
    /// <param name="path">The path associated with the operation.</param>
    void EnsureDirectory(string path);

    /// <summary>
    /// Validates that a directory is private and is not redirected.
    /// </summary>
    /// <param name="path">The path associated with the operation.</param>
    void ValidateDirectory(string path);

    /// <summary>
    /// Validates that a file is private and is not redirected.
    /// </summary>
    /// <param name="path">The path associated with the operation.</param>
    void ValidateFile(string path);

    /// <summary>
    /// Validates that durable files can be created, flushed, and deleted in a directory.
    /// </summary>
    /// <param name="path">The path associated with the operation.</param>
    void ValidateWritableDirectory(string path);
}
