using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceLoadResult
{
    public ILoadedWorkspace? Workspace { get; init; }

    public Solution? Solution { get; init; }

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];
}
