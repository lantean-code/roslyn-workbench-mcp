namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal interface IWorkspaceLoader
{
    string? NormalizeOpenPath(string path);

    string? NormalizeAlias(string? alias);

    (bool IsSdkStyle, IReadOnlyList<DiagnosticInfo> Diagnostics) InspectCompatibility(string projectPath);

    ValueTask<WorkspaceLoadResult> LoadAsync(string path, CancellationToken cancellationToken);
}
