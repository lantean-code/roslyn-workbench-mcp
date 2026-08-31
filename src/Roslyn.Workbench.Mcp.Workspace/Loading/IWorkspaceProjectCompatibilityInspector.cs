namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Inspects whether an MSBuild project uses a supported SDK-style project format.
/// </summary>
internal interface IWorkspaceProjectCompatibilityInspector
{
    /// <summary>
    /// Evaluates a project and determines whether it is SDK-style.
    /// </summary>
    /// <param name="projectPath">The project file to inspect.</param>
    /// <param name="msBuildProperties">The optional MSBuild properties used during evaluation.</param>
    /// <returns>The compatibility result and any evaluation diagnostics.</returns>
    (bool IsSdkStyle, IReadOnlyList<DiagnosticInfo> Diagnostics) Inspect(
        string projectPath,
        WorkspaceMsBuildProperties? msBuildProperties);
}
