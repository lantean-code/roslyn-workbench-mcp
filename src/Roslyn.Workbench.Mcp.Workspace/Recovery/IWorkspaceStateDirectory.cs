namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal interface IWorkspaceStateDirectory
{
    string RootDirectory { get; }

    string RecoveryDirectory { get; }

    void Initialize();
}
