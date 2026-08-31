namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Retrieves the MSBuild global properties associated with a loaded Workspace session.
/// </summary>
internal interface IWorkspaceMsBuildPropertiesProvider
{
    /// <summary>
    /// Gets the properties used to evaluate a loaded Workspace.
    /// </summary>
    /// <param name="workspaceId">The Workspace identifier.</param>
    /// <returns>The evaluation properties, or <see langword="null"/> when the Workspace is not loaded or used defaults.</returns>
    WorkspaceMsBuildProperties? Get(Guid workspaceId);
}
