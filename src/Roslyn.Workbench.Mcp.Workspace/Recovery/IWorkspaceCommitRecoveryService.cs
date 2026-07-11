namespace Roslyn.Workbench.Mcp.Workspace.Recovery;

internal interface IWorkspaceCommitRecoveryService
{
    ValueTask RecoverAsync(CancellationToken cancellationToken);
}
