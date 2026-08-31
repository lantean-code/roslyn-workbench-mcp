namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Validates and normalises caller-supplied MSBuild properties before workspace loading.
/// </summary>
internal interface IWorkspaceMsBuildPropertiesResolver
{
    /// <summary>
    /// Resolves the effective MSBuild properties for a workspace load operation.
    /// </summary>
    /// <param name="properties">The optional properties supplied by the caller.</param>
    /// <returns>The normalised properties or a validation error.</returns>
    WorkspaceMsBuildPropertiesResolution Resolve(WorkspaceMsBuildProperties? properties);
}
