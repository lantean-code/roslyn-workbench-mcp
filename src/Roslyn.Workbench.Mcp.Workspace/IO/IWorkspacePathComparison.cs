namespace Roslyn.Workbench.Mcp.Workspace.IO;

/// <summary>
/// Provides file-system-aware path comparison semantics, including Windows mounts under WSL.
/// </summary>
internal interface IWorkspacePathComparison
{
    /// <summary>
    /// Gets the ordinal comparison appropriate for a path's file system.
    /// </summary>
    /// <param name="path">The path whose file system determines comparison semantics.</param>
    /// <returns>The ordinal comparison appropriate for the path.</returns>
    StringComparison GetComparison(string path);

    /// <summary>
    /// Creates a stable path key using the path's effective case sensitivity.
    /// </summary>
    /// <param name="path">The path to normalize and key.</param>
    /// <returns>A path key carrying the effective case-sensitivity rules.</returns>
    FileSystemPathKey CreateKey(string path);

    /// <summary>
    /// Determines whether a path resides on a Windows file system accessed from Linux.
    /// </summary>
    /// <param name="path">The path to inspect.</param>
    /// <returns><see langword="true"/> for a Windows file-system path under Linux; otherwise, <see langword="false"/>.</returns>
    bool IsWindowsFileSystemPath(string path);
}
