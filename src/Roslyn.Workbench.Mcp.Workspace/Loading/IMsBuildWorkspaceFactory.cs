namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Creates Roslyn MSBuild workspaces with the effective global properties for a load operation.
/// </summary>
internal interface IMsBuildWorkspaceFactory
{
    /// <summary>
    /// Creates an MSBuild workspace configured with the supplied global properties.
    /// </summary>
    /// <param name="globalProperties">The optional global properties used to evaluate projects.</param>
    /// <returns>The configured workspace.</returns>
    MSBuildWorkspace Create(IReadOnlyDictionary<string, string>? globalProperties);
}
