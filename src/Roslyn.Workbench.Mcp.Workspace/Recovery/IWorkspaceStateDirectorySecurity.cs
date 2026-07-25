namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal interface IWorkspaceStateDirectorySecurity
{
    void EnsureDirectory(string path);

    void ValidateDirectory(string path);

    void ValidateFile(string path);
}
