using Microsoft.CodeAnalysis.MSBuild;

namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Creates host-configured MSBuild workspace instances.
/// </summary>
internal sealed class HostConfiguredMsBuildWorkspaceFactory : IMsBuildWorkspaceFactory
{
    private readonly ICodeActionComposition _composition;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostConfiguredMsBuildWorkspaceFactory"/> class.
    /// </summary>
    /// <param name="composition">The Code Action composition that may provide Roslyn host services.</param>
    public HostConfiguredMsBuildWorkspaceFactory(ICodeActionComposition composition)
    {
        _composition = composition;
    }

    /// <summary>
    /// Creates the host-configured MSBuild workspace.
    /// </summary>
    /// <param name="globalProperties">The global MSBuild properties applied when creating the workspace.</param>
    /// <returns>A workspace using design-time build properties and the composed host services when available.</returns>
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
