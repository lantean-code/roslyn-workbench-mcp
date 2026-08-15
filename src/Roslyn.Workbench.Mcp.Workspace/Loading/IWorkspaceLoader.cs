namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal interface IWorkspaceLoader
{
    string? NormalizeOpenPath(string path);

    string? NormalizeAlias(string? alias);

    (bool IsSdkStyle, IReadOnlyList<DiagnosticInfo> Diagnostics) InspectCompatibility(
        string projectPath,
        WorkspaceMsBuildProperties? msBuildProperties);

    ValueTask<WorkspaceLoadResult> LoadAsync(
        string path,
        WorkspaceMsBuildProperties? msBuildProperties,
        CancellationToken cancellationToken);
}
