using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceSessionSnapshot
{
    public WorkspaceLifecycleState State { get; init; }

    public WorkspaceIdentity Workspace { get; init; } = new();

    public MSBuildWorkspace? LoadedWorkspace { get; init; }

    public Solution? CurrentSolution { get; init; }

    public WorkspaceTransaction? Transaction { get; init; }

    public WorkspaceInputManifest? InputManifest { get; init; }

    public WorkspaceOperationGate OperationGate { get; init; } = new(2);

    public int ProjectCount { get; init; }

    public int DocumentCount { get; init; }

    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
