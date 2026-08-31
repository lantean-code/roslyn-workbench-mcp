namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Reads per-Workspace MSBuild evaluation properties from the immutable session store.
/// </summary>
internal sealed class WorkspaceMsBuildPropertiesProvider : IWorkspaceMsBuildPropertiesProvider
{
    private readonly IWorkspaceSessionStore _sessionStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceMsBuildPropertiesProvider"/> class.
    /// </summary>
    /// <param name="sessionStore">The store containing loaded Workspace sessions.</param>
    public WorkspaceMsBuildPropertiesProvider(IWorkspaceSessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    /// <inheritdoc/>
    public WorkspaceMsBuildProperties? Get(Guid workspaceId)
    {
        return _sessionStore.ReadSession(workspaceId)?.MsBuildProperties;
    }
}
