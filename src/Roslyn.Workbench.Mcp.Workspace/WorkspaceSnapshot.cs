using Microsoft.CodeAnalysis.MSBuild;

using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed record WorkspaceSnapshot
{
    public WorkspaceLifecycleState State { get; init; }

    public WorkspaceIdentity? Workspace { get; init; }

    public MSBuildWorkspace? LoadedWorkspace { get; init; }

    public Microsoft.CodeAnalysis.Solution? CurrentSolution { get; init; }

    public WorkspaceTransaction? Transaction { get; init; }

    public int? ProjectCount { get; init; }

    public int? DocumentCount { get; init; }

    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];

    public WorkspaceInputManifest? InputManifest { get; init; }
}
