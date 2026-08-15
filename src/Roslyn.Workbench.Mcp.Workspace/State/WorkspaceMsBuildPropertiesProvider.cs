namespace Roslyn.Workbench.Mcp.Workspace.State;

internal sealed class WorkspaceMsBuildPropertiesProvider : IWorkspaceMsBuildPropertiesProvider
{
    private readonly IWorkspaceSessionStore _sessionStore;

    public WorkspaceMsBuildPropertiesProvider(IWorkspaceSessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    public WorkspaceMsBuildProperties? Get(Guid workspaceId)
    {
        return _sessionStore.ReadSession(workspaceId)?.MsBuildProperties;
    }
}
