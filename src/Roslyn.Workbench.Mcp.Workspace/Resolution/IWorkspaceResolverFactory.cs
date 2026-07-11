namespace Roslyn.Workbench.Mcp.Workspace.Resolution;

internal interface IWorkspaceResolverFactory
{
    IWorkspaceResolver Create(
        Solution solution,
        WorkspaceIdentity? workspaceIdentity,
        int? transactionRevision);
}
