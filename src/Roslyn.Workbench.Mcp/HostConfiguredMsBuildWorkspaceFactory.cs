using Microsoft.CodeAnalysis.MSBuild;

namespace Roslyn.Workbench.Mcp;

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

        return hostServices is null
            ? MSBuildWorkspace.Create()
            : MSBuildWorkspace.Create(hostServices);
    }
}
