namespace Roslyn.Workbench.Mcp.Workspace.IO;

/// <summary>
/// Performs durable native file replacement and move operations.
/// </summary>
internal interface IAtomicFileCommitter
{
    /// <summary>
    /// Atomically replaces or creates a destination from a fully written temporary file.
    /// </summary>
    /// <param name="temporaryPath">The temporary file to commit.</param>
    /// <param name="destinationPath">The path to which the file will be written.</param>
    void Commit(string temporaryPath, string destinationPath);

    /// <summary>
    /// Durably moves a file without overwriting an existing destination.
    /// </summary>
    /// <param name="sourcePath">The source file.</param>
    /// <param name="destinationPath">The path to which the file will be written.</param>
    void Move(string sourcePath, string destinationPath);
}
