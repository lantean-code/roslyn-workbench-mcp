namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal interface IWorkspaceProjectCompatibilityInspector
{
    (bool IsSdkStyle, IReadOnlyList<DiagnosticInfo> Diagnostics) Inspect(string projectPath);
}
