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
        var effectiveGlobalProperties = globalProperties is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(globalProperties, StringComparer.OrdinalIgnoreCase);
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
