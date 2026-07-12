namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal sealed class WorkspacePathComparison : IWorkspacePathComparison
{
    public StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public StringComparer Comparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
