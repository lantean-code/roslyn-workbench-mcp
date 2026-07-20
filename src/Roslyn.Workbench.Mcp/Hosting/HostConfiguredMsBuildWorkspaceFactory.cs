using Microsoft.CodeAnalysis.MSBuild;

namespace Roslyn.Workbench.Mcp.Hosting;

internal sealed class HostConfiguredMsBuildWorkspaceFactory : IMsBuildWorkspaceFactory
{
    private readonly ICodeActionProviderCatalog _providerCatalog;

    public HostConfiguredMsBuildWorkspaceFactory(ICodeActionProviderCatalog providerCatalog)
    {
        _providerCatalog = providerCatalog;
    }

    public MSBuildWorkspace Create()
    {
        var hostServices = _providerCatalog.WorkspaceHostServices;
        var workspace = hostServices is null
            ? MSBuildWorkspace.Create()
            : MSBuildWorkspace.Create(hostServices);

        workspace.SkipUnrecognizedProjects = true;
        return workspace;
    }
}
