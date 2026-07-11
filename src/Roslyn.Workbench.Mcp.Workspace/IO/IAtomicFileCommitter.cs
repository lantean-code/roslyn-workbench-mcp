namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal interface IAtomicFileCommitter
{
    void Commit(string temporaryPath, string destinationPath);

    void Move(string sourcePath, string destinationPath);
}
