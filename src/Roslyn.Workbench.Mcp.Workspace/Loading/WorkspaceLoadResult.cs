using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceLoadResult
{
    public MSBuildWorkspace? Workspace { get; init; }

    public Solution? Solution { get; init; }

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; init; } = [];
}
