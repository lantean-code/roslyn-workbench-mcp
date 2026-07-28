using Microsoft.CodeAnalysis.MSBuild;

namespace Roslyn.Workbench.Mcp.Hosting;

internal sealed class HostConfiguredMsBuildWorkspaceFactory : IMsBuildWorkspaceFactory
{
    private readonly ICodeActionComposition _composition;

    public HostConfiguredMsBuildWorkspaceFactory(ICodeActionComposition composition)
    {
        _composition = composition;
    }

    public MSBuildWorkspace Create()
    {
        var hostServices = _composition.WorkspaceHostServices;
        MSBuildWorkspace workspace;
        if (hostServices is null)
        {
            workspace = MSBuildWorkspace.Create();
        }
        else
        {
            workspace = MSBuildWorkspace.Create(hostServices);
        }

        workspace.SkipUnrecognizedProjects = true;
        return workspace;
    }
}
