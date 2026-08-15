namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal interface IMsBuildWorkspaceFactory
{
    MSBuildWorkspace Create(IReadOnlyDictionary<string, string>? globalProperties);
}
