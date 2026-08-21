using Microsoft.CodeAnalysis.MSBuild;

namespace Roslyn.Workbench.Mcp.Hosting;

internal sealed class HostConfiguredMsBuildWorkspaceFactory : IMsBuildWorkspaceFactory
{
    private readonly ICodeActionComposition _composition;

    public HostConfiguredMsBuildWorkspaceFactory(ICodeActionComposition composition)
    {
        _composition = composition;
    }

    public MSBuildWorkspace Create(IReadOnlyDictionary<string, string>? globalProperties)
    {
        var hostServices = _composition.WorkspaceHostServices;
        var effectiveGlobalProperties = WorkspaceDesignTimeGlobalProperties.Create(globalProperties);
        MSBuildWorkspace workspace;
        if (hostServices is null)
        {
            workspace = MSBuildWorkspace.Create(effectiveGlobalProperties);
        }
        else
        {
            workspace = MSBuildWorkspace.Create(effectiveGlobalProperties, hostServices);
        }

        workspace.SkipUnrecognizedProjects = true;
        return workspace;
    }
}
